using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LightweightAutoClicker
{
    internal interface IClickDispatcher : IDisposable
    {
        string ModeName { get; }
        bool TryClick(ClickPointConfig point);
    }

    internal static class ClickDispatcherFactory
    {
        internal static bool TryCreate(WindowInfo target, out IClickDispatcher dispatcher, out string error)
        {
            dispatcher = null;
            error = string.Empty;

            if (target == null || !NativeMethods.IsWindow(target.Handle))
            {
                error = "Target window is no longer available.";
                return false;
            }

            if (string.Equals(target.ProcessName, "MuMuNxDevice", StringComparison.OrdinalIgnoreCase))
                return MuMuAdbTapDispatcher.TryCreate(target, out dispatcher, out error);

            dispatcher = new WindowMessageClickDispatcher(target.Handle);
            return true;
        }
    }

    internal sealed class WindowMessageClickDispatcher : IClickDispatcher
    {
        private readonly IntPtr target;

        internal WindowMessageClickDispatcher(IntPtr target)
        {
            this.target = target;
        }

        public string ModeName
        {
            get { return "Windows background message"; }
        }

        public bool TryClick(ClickPointConfig point)
        {
            return NativeMethods.PostClick(target, point);
        }

        public void Dispose()
        {
        }
    }

    internal sealed class MuMuAdbTapDispatcher : IClickDispatcher
    {
        private readonly object sync = new object();
        private readonly IntPtr targetWindow;
        private readonly string adbPath;
        private readonly string serial;
        private readonly int displayId;
        private readonly int androidWidth;
        private readonly int androidHeight;
        private readonly int clientWidth;
        private readonly int clientHeight;
        private Process shell;
        private bool disposed;

        private MuMuAdbTapDispatcher(
            IntPtr targetWindow,
            string adbPath,
            string serial,
            int displayId,
            int androidWidth,
            int androidHeight,
            int clientWidth,
            int clientHeight)
        {
            this.targetWindow = targetWindow;
            this.adbPath = adbPath;
            this.serial = serial;
            this.displayId = displayId;
            this.androidWidth = androidWidth;
            this.androidHeight = androidHeight;
            this.clientWidth = clientWidth;
            this.clientHeight = clientHeight;
        }

        public string ModeName
        {
            get { return "MuMu Android touch"; }
        }

        internal static bool TryCreate(WindowInfo target, out IClickDispatcher dispatcher, out string error)
        {
            dispatcher = null;
            error = string.Empty;

            string adbPath = FindAdb(target.ProcessId);
            if (adbPath == null)
            {
                error = "MuMu ADB was not found next to the selected emulator.";
                return false;
            }

            string devices;
            if (!TryRun(adbPath, "devices", out devices))
            {
                error = "MuMu ADB did not respond.";
                return false;
            }

            string serial = FindDeviceSerial(devices);
            if (serial == null)
            {
                error = "No online MuMu Android device was found.";
                return false;
            }

            string displays;
            if (!TryRun(adbPath, "-s \"" + serial + "\" shell dumpsys window displays", out displays))
            {
                error = "Could not read the MuMu Android display.";
                return false;
            }

            int displayId;
            int androidWidth;
            int androidHeight;
            if (!TryFindFocusedDisplay(displays, out displayId, out androidWidth, out androidHeight))
            {
                error = "Could not determine the active MuMu Android display.";
                return false;
            }

            int clientWidth;
            int clientHeight;
            if (!NativeMethods.TryGetClientSize(target.Handle, out clientWidth, out clientHeight))
            {
                error = "Could not read the MuMu window size.";
                return false;
            }

            var result = new MuMuAdbTapDispatcher(
                target.Handle,
                adbPath,
                serial,
                displayId,
                androidWidth,
                androidHeight,
                clientWidth,
                clientHeight);

            if (!result.StartShell(out error))
            {
                result.Dispose();
                return false;
            }

            dispatcher = result;
            return true;
        }

        public bool TryClick(ClickPointConfig point)
        {
            lock (sync)
            {
                if (disposed || shell == null || shell.HasExited || !NativeMethods.IsWindow(targetWindow))
                    return false;

                int x = Scale(point.X, clientWidth, androidWidth);
                int y = Scale(point.Y, clientHeight, androidHeight);
                string command = displayId == 0
                    ? string.Format(CultureInfo.InvariantCulture, "input tap {0} {1}", x, y)
                    : string.Format(CultureInfo.InvariantCulture, "input -d {0} tap {1} {2}", displayId, x, y);

                try
                {
                    shell.StandardInput.WriteLine(command);
                    shell.StandardInput.Flush();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;

                if (shell == null)
                    return;

                try
                {
                    shell.StandardInput.Close();
                    if (!shell.WaitForExit(250))
                        shell.Kill();
                }
                catch
                {
                }
                finally
                {
                    shell.Dispose();
                    shell = null;
                }
            }
        }

        private bool StartShell(out string error)
        {
            error = string.Empty;
            try
            {
                shell = Process.Start(new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "-s \"" + serial + "\" shell",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                });
                if (shell == null)
                {
                    error = "MuMu ADB shell did not start.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "MuMu ADB shell did not start: " + ex.Message;
                return false;
            }
        }

        private static int Scale(int value, int sourceSize, int targetSize)
        {
            int scaled = (int)Math.Round(value * targetSize / (double)sourceSize);
            return Math.Max(0, Math.Min(targetSize - 1, scaled));
        }

        private static string FindAdb(int processId)
        {
            try
            {
                string executable = Process.GetProcessById(processId).MainModule.FileName;
                DirectoryInfo directory = new FileInfo(executable).Directory;
                for (int i = 0; directory != null && i < 5; i++, directory = directory.Parent)
                {
                    string candidate = Path.Combine(directory.FullName, "nx_main", "adb.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
            }
            return null;
        }

        private static string FindDeviceSerial(string devices)
        {
            MatchCollection matches = Regex.Matches(devices, @"(?m)^(?<serial>\S+)\s+device(?:\s|$)");
            foreach (Match match in matches)
            {
                string serial = match.Groups["serial"].Value;
                if (string.Equals(serial, "127.0.0.1:5555", StringComparison.OrdinalIgnoreCase))
                    return serial;
            }
            foreach (Match match in matches)
            {
                string serial = match.Groups["serial"].Value;
                if (serial.StartsWith("127.0.0.1:", StringComparison.OrdinalIgnoreCase))
                    return serial;
            }
            return matches.Count == 0 ? null : matches[0].Groups["serial"].Value;
        }

        private static bool TryFindFocusedDisplay(string dumpsys, out int displayId, out int width, out int height)
        {
            displayId = 0;
            width = 0;
            height = 0;
            MatchCollection displays = Regex.Matches(
                dumpsys,
                @"(?ms)^\s*Display: mDisplayId=(?<id>\d+).*?(?=^\s*Display: mDisplayId=|\z)");

            foreach (Match display in displays)
            {
                if (display.Value.IndexOf("mCurrentFocus=Window", StringComparison.Ordinal) < 0)
                    continue;

                Match frame = Regex.Match(display.Value, @"DisplayFrames w=(?<width>\d+) h=(?<height>\d+)");
                int parsedId;
                int parsedWidth;
                int parsedHeight;
                if (!frame.Success ||
                    !int.TryParse(display.Groups["id"].Value, out parsedId) ||
                    !int.TryParse(frame.Groups["width"].Value, out parsedWidth) ||
                    !int.TryParse(frame.Groups["height"].Value, out parsedHeight))
                    continue;

                displayId = parsedId;
                width = parsedWidth;
                height = parsedHeight;
                return true;
            }
            return false;
        }

        private static bool TryRun(string adbPath, string arguments, out string output)
        {
            output = string.Empty;
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }))
                {
                    if (process == null)
                        return false;

                    output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill();
                        return false;
                    }
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
