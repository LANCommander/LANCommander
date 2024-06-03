using Microsoft.UI.Windowing;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace LANCommander.Client.Services
{
    public class WindowService
    {
        public bool IsMaximized
        {
            get
            {
                var appWindow = GetAppWindow();

                if (appWindow != null)
                {
                    var presenter = appWindow.Presenter as OverlappedPresenter;

                    return presenter.State == OverlappedPresenterState.Maximized;
                }
                else return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Windows.Foundation.Point lpPoint);
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWind, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const uint WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 0x02;

        public Windows.Foundation.Point GetCursorPosition()
        {
            GetCursorPos(out Windows.Foundation.Point point);

            return point;
        }

        public void StartDragWindow()
        {
            var window = App.Current.Windows.First();
            var nativeWindow = window.Handler.PlatformView;
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);            

            SendMessage(windowHandle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            /*var Window = App.Current.Windows.First();
            var nativeWindow = Window.Handler.PlatformView;
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            WindowId WindowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            AppWindow appWindow = AppWindow.GetFromWindowId(WindowId);

            var cursor = GetCursorPosition();

            appWindow.Move(new Windows.Graphics.PointInt32((int)cursor.X, (int)cursor.Y));*/
        }

        public void StopDragWindow()
        {
            ReleaseCapture();
        }

        public void Minimize()
        {
            var p = GetPresenter();

            p.Minimize();
        }

        public void ToggleMaximize()
        {
            var presenter = GetAppWindow().Presenter as OverlappedPresenter;

            if (presenter.State != OverlappedPresenterState.Maximized)
                presenter.Maximize();
            else
                presenter.Restore();
        }

        public void Close()
        {
            Application.Current.Quit();
        }

        AppWindow GetAppWindow()
        {
            var window = App.Current.Windows.First();

            if (window.Handler != null)
            {
                var nativeWindow = window.Handler.PlatformView;
                IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                return appWindow;
            }
            else return null;
        }

        OverlappedPresenter GetPresenter()
        {
            var Window = App.Current.Windows.First();
            var nativeWindow = Window.Handler.PlatformView;
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            WindowId WindowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            AppWindow appWindow = AppWindow.GetFromWindowId(WindowId);

            return appWindow.Presenter as OverlappedPresenter;
        }
    }
}
