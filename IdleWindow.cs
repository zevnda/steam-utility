using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SteamUtility
{
    public class IdleWindow : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam
        );

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const uint WS_POPUP = 0x80000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_DISABLED = 0x08000000;
        private const int SW_HIDE = 0;

        private IntPtr _hWnd;
        private long appid;
        private string appName;
        private bool disposed = false;

        public IdleWindow(long appid, string appName = "Idling")
        {
            this.appid = appid;
            this.appName = appName ?? "Idling";

            string title = $"{this.appName} [{appid}]";

            _hWnd = CreateWindowEx(
                WS_EX_TOOLWINDOW,
                "STATIC",
                title,
                WS_POPUP | WS_DISABLED,
                0,
                0,
                1,
                1,
                IntPtr.Zero,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero
            );

            ShowWindow(_hWnd, SW_HIDE);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (_hWnd != IntPtr.Zero)
                {
                    DestroyWindow(_hWnd);
                    _hWnd = IntPtr.Zero;
                }

                disposed = true;
            }
        }

        ~IdleWindow()
        {
            Dispose(false);
        }
    }
}
