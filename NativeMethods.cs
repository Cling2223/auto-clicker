using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LightweightAutoClicker
{
    internal static class NativeMethods
    {
        internal const int WM_MOUSEMOVE = 0x0200;
        internal const int WM_LBUTTONDOWN = 0x0201;
        internal const int WM_LBUTTONUP = 0x0202;
        internal const int WM_RBUTTONDOWN = 0x0204;
        internal const int WM_RBUTTONUP = 0x0205;
        internal const int WM_MBUTTONDOWN = 0x0207;
        internal const int WM_MBUTTONUP = 0x0208;
        internal const int MK_LBUTTON = 0x0001;
        internal const int MK_RBUTTON = 0x0002;
        internal const int MK_MBUTTON = 0x0010;
        internal const uint CWP_SKIPINVISIBLE = 0x0001;
        internal const uint CWP_SKIPDISABLED = 0x0002;
        internal const uint CWP_SKIPTRANSPARENT = 0x0004;
        internal const int MOD_NOREPEAT = 0x4000;
        internal const int VK_F6 = 0x75;
        internal const int VK_F8 = 0x77;

        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        internal static extern IntPtr ChildWindowFromPointEx(IntPtr hWndParent, POINT point, uint flags);

        [DllImport("user32.dll")]
        internal static extern int MapWindowPoints(IntPtr from, IntPtr to, ref POINT point, uint count);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, int virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        internal static List<WindowInfo> GetTopLevelWindows(IntPtr excludedHandle)
        {
            var result = new List<WindowInfo>();
            EnumWindows(delegate(IntPtr hWnd, IntPtr unused)
            {
                if (hWnd == excludedHandle || !IsWindowVisible(hWnd))
                    return true;

                int length = GetWindowTextLength(hWnd);
                if (length <= 0)
                    return true;

                var titleBuilder = new StringBuilder(length + 1);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                string title = titleBuilder.ToString().Trim();
                if (title.Length == 0)
                    return true;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                string processName = "unknown";
                try
                {
                    processName = Process.GetProcessById((int)pid).ProcessName;
                }
                catch
                {
                }

                result.Add(new WindowInfo
                {
                    Handle = hWnd,
                    ProcessId = (int)pid,
                    ProcessName = processName,
                    Title = title
                });
                return true;
            }, IntPtr.Zero);

            result.Sort(delegate(WindowInfo left, WindowInfo right)
            {
                return string.Compare(left.Title, right.Title, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        internal static bool TryConvertScreenToClient(IntPtr target, int screenX, int screenY, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (!IsWindow(target))
                return false;

            var point = new POINT(screenX, screenY);
            if (!ScreenToClient(target, ref point))
                return false;

            RECT rect;
            if (!GetClientRect(target, out rect))
                return false;

            if (point.X < rect.Left || point.Y < rect.Top || point.X >= rect.Right || point.Y >= rect.Bottom)
                return false;

            x = point.X;
            y = point.Y;
            return true;
        }

        internal static bool TryGetClientSize(IntPtr target, out int width, out int height)
        {
            width = 0;
            height = 0;
            RECT rect;
            if (!IsWindow(target) || !GetClientRect(target, out rect))
                return false;

            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;
            return width > 0 && height > 0;
        }

        internal static bool PostClick(IntPtr target, ClickPointConfig config)
        {
            if (!IsWindow(target))
                return false;

            IntPtr packed = PackCoordinates(config.X, config.Y);
            int down;
            int up;
            int buttonFlag;

            switch (config.Button)
            {
                case ClickButton.Right:
                    down = WM_RBUTTONDOWN;
                    up = WM_RBUTTONUP;
                    buttonFlag = MK_RBUTTON;
                    break;
                case ClickButton.Middle:
                    down = WM_MBUTTONDOWN;
                    up = WM_MBUTTONUP;
                    buttonFlag = MK_MBUTTON;
                    break;
                default:
                    down = WM_LBUTTONDOWN;
                    up = WM_LBUTTONUP;
                    buttonFlag = MK_LBUTTON;
                    break;
            }

            PostMessage(target, WM_MOUSEMOVE, IntPtr.Zero, packed);
            bool downSent = PostMessage(target, down, new IntPtr(buttonFlag), packed);
            bool upSent = PostMessage(target, up, IntPtr.Zero, packed);
            return downSent && upSent;
        }

        internal static IntPtr PackCoordinates(int x, int y)
        {
            int packed = (y & 0xFFFF) << 16 | (x & 0xFFFF);
            return new IntPtr(packed);
        }

    }
}
