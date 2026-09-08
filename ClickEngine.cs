using System;
using System.Collections.Generic;
using System.Threading;

namespace LightweightAutoClicker
{
    internal sealed class ClickEngine : IDisposable
    {
        private sealed class TimerState
        {
            public IClickDispatcher Dispatcher;
            public ClickPointConfig Point;
            public int IsTicking;
        }

        private readonly object sync = new object();
        private readonly List<Timer> timers = new List<Timer>();
        private IClickDispatcher dispatcher;
        private long totalClicks;
        private int lastClickX;
        private int lastClickY;
        private long lastClickId;
        private volatile bool running;

        public bool IsRunning
        {
            get { return running; }
        }

        public long TotalClicks
        {
            get { return Interlocked.Read(ref totalClicks); }
        }

        public bool TryGetLastClick(out int x, out int y, out long clickId)
        {
            clickId = Interlocked.Read(ref lastClickId);
            x = Volatile.Read(ref lastClickX);
            y = Volatile.Read(ref lastClickY);
            return clickId != 0;
        }

        public void Start(IClickDispatcher clickDispatcher, IEnumerable<ClickPointConfig> points)
        {
            if (clickDispatcher == null)
                throw new ArgumentNullException("clickDispatcher");

            lock (sync)
            {
                StopLocked();
                dispatcher = clickDispatcher;
                Interlocked.Exchange(ref totalClicks, 0);
                Interlocked.Exchange(ref lastClickId, 0);

                foreach (ClickPointConfig source in points)
                {
                    if (!source.Enabled)
                        continue;

                    ClickPointConfig point = source.Copy();
                    point.IntervalMs = Math.Max(10, point.IntervalMs);
                    var state = new TimerState { Dispatcher = dispatcher, Point = point };
                    var timer = new Timer(OnTimer, state, 0, point.IntervalMs);
                    timers.Add(timer);
                }

                running = timers.Count > 0;
                if (!running)
                {
                    dispatcher.Dispose();
                    dispatcher = null;
                }
            }
        }

        public void Stop()
        {
            lock (sync)
                StopLocked();
        }

        public void Dispose()
        {
            Stop();
        }

        private void StopLocked()
        {
            running = false;
            foreach (Timer timer in timers)
                timer.Dispose();
            timers.Clear();
            if (dispatcher != null)
            {
                dispatcher.Dispose();
                dispatcher = null;
            }
        }

        private void OnTimer(object value)
        {
            var state = (TimerState)value;
            if (!IsRunning)
                return;

            if (Interlocked.Exchange(ref state.IsTicking, 1) != 0)
                return;

            try
            {
                if (state.Dispatcher.TryClick(state.Point))
                {
                    Volatile.Write(ref lastClickX, state.Point.X);
                    Volatile.Write(ref lastClickY, state.Point.Y);
                    Interlocked.Increment(ref totalClicks);
                    Interlocked.Increment(ref lastClickId);
                }
            }
            finally
            {
                Volatile.Write(ref state.IsTicking, 0);
            }
        }
    }
}
