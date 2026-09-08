using System;
using System.Linq;

namespace LightweightAutoClicker
{
    internal static class MuMuDispatcherSmoke
    {
        private static int Main()
        {
            WindowInfo target = NativeMethods.GetTopLevelWindows(IntPtr.Zero)
                .FirstOrDefault(window => string.Equals(window.ProcessName, "MuMuNxDevice", StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                Console.Error.WriteLine("MuMu window was not found.");
                return 1;
            }

            IClickDispatcher dispatcher;
            string error;
            if (!ClickDispatcherFactory.TryCreate(target, out dispatcher, out error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            using (dispatcher)
                Console.WriteLine(dispatcher.ModeName);
            return 0;
        }
    }
}
