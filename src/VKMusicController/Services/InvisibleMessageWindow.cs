using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VKMusicController.Services
{
    internal class InvisibleMessageWindow : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private readonly HwndSource _hwndSource;

        public event Action<int>? HotkeyPressed;

        public InvisibleMessageWindow()
        {
            var parameters = new HwndSourceParameters("VKMusicControllerHotkeyWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0x800000, // WS_POPUP
                HwndSourceHook = WndProc
            };
            _hwndSource = new HwndSource(parameters);
        }

        public IntPtr Handle => _hwndSource.Handle;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                HotkeyPressed?.Invoke(id);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            _hwndSource.Dispose();
        }
    }
}
