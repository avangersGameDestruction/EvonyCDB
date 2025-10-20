using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace EvonyCDB
{
    public sealed class ProcessFinder : IDisposable
    {
        public Process TargetProcess { get; private set; }
        public IntPtr MainWindowHandle { get; private set; } = IntPtr.Zero;

        /// <summary>Latest window rectangle in screen coordinates (including frame).</summary>
        public Rectangle WindowRect { get; private set; } = Rectangle.Empty;

        public bool IsConnected => TargetProcess != null && !TargetProcess.HasExited && MainWindowHandle != IntPtr.Zero;

        public event EventHandler ProcessExited;

        public void Dispose()
        {
            Detach();
        }

        public void Detach()
        {
            if (TargetProcess != null)
            {
                try { TargetProcess.EnableRaisingEvents = false; } catch { /* ignore */ }
                try { TargetProcess.Exited -= OnProcessExited; } catch { /* ignore */ }
                try { TargetProcess.Dispose(); } catch { /* ignore */ }
            }
            TargetProcess = null;
            MainWindowHandle = IntPtr.Zero;
            WindowRect = Rectangle.Empty;
        }

        /// <summary>
        /// Attempts to connect to a running process by exe name (e.g., "Evony.exe" or "Evony").
        /// Will wait briefly for a main window handle to appear.
        /// </summary>
        public bool TryConnect(string exeOrProcessName, out string errorMessage, int waitForWindowMs = 2000)
        {
            errorMessage = null;
            Detach();

            if (string.IsNullOrWhiteSpace(exeOrProcessName))
            {
                errorMessage = "No process name provided.";
                return false;
            }

            // Normalize: strip .exe if present
            var name = exeOrProcessName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            // Find processes by name
            var candidates = Process.GetProcessesByName(name);
            if (candidates is null || candidates.Length == 0)
            {
                errorMessage = $"Process \"{name}\" not found.";
                return false;
            }

            // Prefer a process that already has a main window handle
            TargetProcess = candidates.FirstOrDefault(p => GetSafeMainWindowHandle(p) != IntPtr.Zero)
                            ?? candidates.First();

            try
            {
                TargetProcess.EnableRaisingEvents = true;
                TargetProcess.Exited += OnProcessExited;
            }
            catch { /* ignore */ }

            // Resolve main window handle, waiting briefly if needed
            var deadline = Environment.TickCount + waitForWindowMs;
            while (Environment.TickCount < deadline)
            {
                MainWindowHandle = GetTopLevelWindowForProcess(TargetProcess);
                if (MainWindowHandle != IntPtr.Zero && IsWindow(MainWindowHandle))
                    break;

                System.Threading.Thread.Sleep(50);
            }

            if (MainWindowHandle == IntPtr.Zero)
            {
                errorMessage = "Found the process, but could not locate a top-level window.";
                Detach();
                return false;
            }

            UpdateWindowRect(); // initialize bounds
            return true;
        }

        /// <summary>
        /// Brings the window to the foreground and shows it if minimized/hidden.
        /// </summary>
        public bool FocusWindow()
        {
            if (!IsConnected) return false;

            // If minimized, restore
            if (IsIconic(MainWindowHandle))
                ShowWindowAsync(MainWindowHandle, SW_RESTORE);
            else
                ShowWindowAsync(MainWindowHandle, SW_SHOW);

            // Try foreground
            return SetForegroundWindow(MainWindowHandle);
        }

        /// <summary>
        /// Updates WindowRect. Returns false if the window is no longer valid.
        /// </summary>
        public bool UpdateWindowRect()
        {
            if (!IsConnected) return false;

            // Prefer DWM extended frame bounds (more accurate with aero frames)
            if (DwmGetWindowAttribute(MainWindowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT dwmRect, Marshal.SizeOf<RECT>()) == 0)
            {
                WindowRect = Rectangle.FromLTRB(dwmRect.Left, dwmRect.Top, dwmRect.Right, dwmRect.Bottom);
                return true;
            }

            if (GetWindowRect(MainWindowHandle, out RECT r))
            {
                WindowRect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a point inside the target window (client coords) to screen coords.
        /// Useful when you want to click relative to the window only.
        /// </summary>
        public Point ClientToScreen(Point clientPoint)
        {
            if (!IsConnected) return Point.Empty;
            POINT p = new POINT { X = clientPoint.X, Y = clientPoint.Y };
            if (ClientToScreen(MainWindowHandle, ref p))
                return new Point(p.X, p.Y);
            return Point.Empty;
        }

        /// <summary>
        /// Convenience: check if a screen point is inside the current window bounds.
        /// </summary>
        public bool ScreenPointInWindow(Point screenPoint)
        {
            return IsConnected && WindowRect.Contains(screenPoint);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            ProcessExited?.Invoke(this, EventArgs.Empty);
            Detach();
        }

        // --- Helpers ---------------------------------------------------------

        private static IntPtr GetSafeMainWindowHandle(Process p)
        {
            try { return p.MainWindowHandle; } catch { return IntPtr.Zero; }
        }

        private static IntPtr GetTopLevelWindowForProcess(Process p)
        {
            // First try MainWindowHandle
            var h = GetSafeMainWindowHandle(p);
            if (h != IntPtr.Zero && IsWindow(h))
                return h;

            // Fallback: enumerate top-level windows and match by PID
            IntPtr result = IntPtr.Zero;
            uint targetPid = (uint)p.Id;

            bool Callback(IntPtr hwnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hwnd)) return true;
                uint pid;
                _ = GetWindowThreadProcessId(hwnd, out pid);
                if (pid == targetPid && GetWindow(hwnd, GW_OWNER) == IntPtr.Zero)
                {
                    result = hwnd;
                    return false; // stop
                }
                return true;
            }

            EnumWindows(Callback, IntPtr.Zero);
            return result;
        }

        // --- Win32 interop ---------------------------------------------------

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int GW_OWNER = 4;

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }
    }
}
