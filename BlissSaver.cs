using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Bliss")]
[assembly: System.Reflection.AssemblyProduct("Bliss Screensaver")]

namespace BlissSaver
{
    public static class Program
    {
        const int MoveSlop = 20;
        const int ArmDelayMs = 700;

        [STAThread]
        public static int Main(string[] args)
        {
            string joined = args == null ? "" : string.Join(" ", args);
            string head = args != null && args.Length > 0 ? args[0] : "/s";
            string flag = head.Trim().TrimStart('-', '/').ToLowerInvariant();

            if (flag.StartsWith("c"))
            {
                ShowConfig();
                return 0;
            }
            if (flag.StartsWith("p"))
            {
                IntPtr parent = ParseHandle(head, args);
                if (parent == IntPtr.Zero)
                    return 0;
                RunPreview(parent);
                return 0;
            }

            RunFull();
            return 0;
        }

        static void ShowConfig()
        {
            string path = FindVideo() ?? "(film nije nadjen)";
            MessageBox.Show(
                "Bliss pu\u0161ta film dobrodo\u0161lice preko celog ekrana.\r\n\r\n" +
                path + "\r\n\r\n" +
                "Pomeranje mi\u0161a, taster ili klik zatvara \u010duvar.",
                "Bliss",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        static void RunFull()
        {
            var app = new Application();
            var origin = Forms.Cursor.Position;
            bool armed = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ArmDelayMs) };
            timer.Tick += delegate { armed = true; timer.Stop(); };
            timer.Start();

            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                bool primary = screen.Primary;
                var window = new Window
                {
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    Background = Brushes.Black,
                    Topmost = true,
                    ShowInTaskbar = false,
                    ShowActivated = primary,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 0,
                    Top = 0,
                    Width = 100,
                    Height = 100,
                    Cursor = Cursors.None,
                    Content = primary ? BuildPlayer(loopSound: true) : null
                };

                window.SourceInitialized += delegate
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    SetWindowPos(handle, new IntPtr(-1),
                        screen.Bounds.Left, screen.Bounds.Top,
                        screen.Bounds.Width, screen.Bounds.Height,
                        0x0040);
                };

                Action close = delegate { app.Shutdown(); };
                window.MouseDown += delegate { if (armed) close(); };
                window.KeyDown += delegate { if (armed) close(); };
                window.MouseMove += delegate
                {
                    if (!armed) return;
                    var now = Forms.Cursor.Position;
                    if (Math.Abs(now.X - origin.X) + Math.Abs(now.Y - origin.Y) > MoveSlop)
                        close();
                };
                window.Show();
            }

            app.Run();
        }

        static void RunPreview(IntPtr parent)
        {
            var app = new Application();
            RECT rect;
            GetClientRect(parent, out rect);
            int width = Math.Max(rect.Right - rect.Left, 1);
            int height = Math.Max(rect.Bottom - rect.Top, 1);

            var parameters = new HwndSourceParameters("BlissPreview")
            {
                ParentWindow = parent,
                WindowStyle = 0x40000000 | 0x10000000,
                Width = width,
                Height = height
            };
            var source = new HwndSource(parameters);
            source.RootVisual = BuildPlayer(loopSound: false);

            var watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            watch.Tick += delegate
            {
                if (!IsWindow(parent))
                    app.Shutdown();
            };
            watch.Start();
            app.Run();
        }

        static UIElement BuildPlayer(bool loopSound)
        {
            string path = FindVideo();
            if (path == null)
            {
                return new TextBlock
                {
                    Text = "Bliss-Welcome-16x9.mp4 nije na\u0111en.",
                    Foreground = Brushes.White,
                    Background = Brushes.Black,
                    FontSize = 18,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            var media = new MediaElement
            {
                Source = new Uri(path, UriKind.Absolute),
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.UniformToFill,
                IsMuted = !loopSound,
                Volume = loopSound ? 1 : 0
            };
            media.MediaEnded += delegate
            {
                media.Position = TimeSpan.Zero;
                media.Play();
            };
            media.Loaded += delegate { media.Play(); };
            return media;
        }

        static string FindVideo()
        {
            string name = "Bliss-Welcome-16x9.mp4";
            var candidates = new List<string>();
            string here = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            if (!string.IsNullOrEmpty(here))
                candidates.Add(Path.Combine(here, name));
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            candidates.Add(Path.Combine(desktop, name));
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        static IntPtr ParseHandle(string head, string[] args)
        {
            string raw = head;
            int colon = head.IndexOf(':');
            if (colon >= 0 && colon < head.Length - 1)
                raw = head.Substring(colon + 1);
            else if (args != null && args.Length > 1)
                raw = args[1];
            long value;
            if (long.TryParse(raw, out value))
                return new IntPtr(value);
            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);

        struct RECT
        {
            public int Left, Top, Right, Bottom;
        }
    }
}
