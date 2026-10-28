using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        const string VideoResource = "BlissSaver.loop.mp4";
        const string MusicResource = "BlissSaver.music.m4a";

        static MediaPlayer music;

        [STAThread]
        public static int Main(string[] args)
        {
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
            MessageBox.Show(
                "Bliss vrti brdo u krug, a muzika dobrodo\u0161lice ide posebno.\r\n\r\n" +
                "Slika i muzika su u samom \u010duvaru.\r\n\r\n" +
                "Pomeranje mi\u0161a, taster ili klik zatvara \u010duvar.",
                "Bliss",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        static void RunFull()
        {
            string video = Extract(VideoResource, "loop.mp4");
            string song = Extract(MusicResource, "music.m4a");
            if (video == null || song == null)
            {
                MessageBox.Show(
                    "U \u010duvaru nedostaje slika ili muzika.",
                    "Bliss",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

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
                    Content = CreateVideo(video)
                };

                window.SourceInitialized += delegate
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    SetWindowPos(handle, new IntPtr(-1),
                        screen.Bounds.Left, screen.Bounds.Top,
                        screen.Bounds.Width, screen.Bounds.Height,
                        0x0040);
                };

                Action close = delegate
                {
                    if (music != null)
                    {
                        music.Stop();
                        music.Close();
                        music = null;
                    }
                    app.Shutdown();
                };
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

            StartMusic(song);
            app.Run();
        }

        static void RunPreview(IntPtr parent)
        {
            string video = Extract(VideoResource, "loop.mp4");
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
            source.RootVisual = video == null
                ? (UIElement)new TextBlock
                {
                    Text = "Bliss",
                    Foreground = Brushes.White,
                    Background = Brushes.Black
                }
                : CreateVideo(video);

            var watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            watch.Tick += delegate
            {
                if (!IsWindow(parent))
                    app.Shutdown();
            };
            watch.Start();
            app.Run();
        }

        static UIElement CreateVideo(string path)
        {
            var timeline = new MediaTimeline(new Uri(path, UriKind.Absolute));
            timeline.RepeatBehavior = RepeatBehavior.Forever;
            var clock = (MediaClock)timeline.CreateClock(true);

            var media = new MediaElement
            {
                Clock = clock,
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.UniformToFill,
                IsMuted = true,
                Volume = 0
            };
            media.Loaded += delegate
            {
                ClockController controller = clock.Controller;
                if (controller == null || clock.CurrentState == ClockState.Active)
                    return;
                controller.Begin();
            };
            return media;
        }

        static void StartMusic(string path)
        {
            music = new MediaPlayer { Volume = 1 };
            music.MediaEnded += delegate
            {
                if (music == null) return;
                music.Position = TimeSpan.Zero;
                music.Play();
            };
            music.MediaOpened += delegate
            {
                if (music != null)
                    music.Play();
            };
            music.Open(new Uri(path, UriKind.Absolute));
        }

        static string Extract(string resourceName, string fileName)
        {
            Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (input == null)
                return null;
            using (input)
            {
                string dir = Path.Combine(Path.GetTempPath(), "BlissScreensaver");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, fileName);
                try
                {
                    using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                        input.CopyTo(output);
                }
                catch (IOException)
                {
                    if (!File.Exists(path))
                        return null;
                }
                return path;
            }
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
