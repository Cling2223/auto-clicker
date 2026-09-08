using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LightweightAutoClicker
{
    internal static class BackgroundClickIntegration
    {
        [STAThread]
        private static int Main()
        {
            var ready = new ManualResetEvent(false);
            var clicked = new ManualResetEvent(false);
            IntPtr targetHandle = IntPtr.Zero;
            FixtureForm targetForm = null;

            var uiThread = new Thread(delegate()
            {
                targetForm = new FixtureForm(clicked)
                {
                    Text = "AutoClicker integration fixture",
                    ClientSize = new Size(240, 140),
                    ShowInTaskbar = false
                };
                targetForm.Shown += delegate
                {
                    targetHandle = targetForm.Handle;
                    targetForm.WindowState = FormWindowState.Minimized;
                    ready.Set();
                };
                Application.Run(targetForm);
            });
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.IsBackground = true;
            uiThread.Start();

            if (!ready.WaitOne(3000))
            {
                Console.Error.WriteLine("Fixture window did not start.");
                return 1;
            }

            var point = new ClickPointConfig
            {
                X = 35,
                Y = 35,
                Button = ClickButton.Left,
                IntervalMs = 100
            };
            bool messageSent = NativeMethods.PostClick(targetHandle, point);
            bool clickObserved = clicked.WaitOne(2000);

            if (targetForm != null && targetForm.IsHandleCreated)
                targetForm.BeginInvoke(new Action(targetForm.Close));
            uiThread.Join(2000);

            Console.WriteLine("Message sent: " + messageSent);
            Console.WriteLine("Minimized window mouse-up message observed: " + clickObserved);
            return messageSent && clickObserved ? 0 : 1;
        }

        private sealed class FixtureForm : Form
        {
            private readonly EventWaitHandle clicked;

            public FixtureForm(EventWaitHandle clicked)
            {
                this.clicked = clicked;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_LBUTTONUP)
                    clicked.Set();
                base.WndProc(ref m);
            }
        }
    }
}
