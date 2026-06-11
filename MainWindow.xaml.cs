using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DesktopVideoWallpaper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // ==========================================
        // KHAI BÁO CÁC HÀM WIN32 API (P/INVOKE)
        // ==========================================

        /// <summary>
        /// Hàm gửi tin nhắn đến một cửa sổ chỉ định và chờ phản hồi hoặc hết thời gian chờ (timeout).
        /// Được dùng để gửi thông điệp đặc biệt 0x052C tới Progman, yêu cầu Windows sinh ra cửa sổ WorkerW.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            SendMessageTimeoutFlags flags,
            uint timeout,
            out IntPtr result);

        /// <summary>
        /// Các cờ thiết lập chế độ gửi thông điệp trong hàm SendMessageTimeout.
        /// </summary>
        [Flags]
        public enum SendMessageTimeoutFlags : uint
        {
            SMTO_NORMAL = 0x0000,
            SMTO_BLOCK = 0x0001,
            SMTO_ABORTIFHUNG = 0x0002,
            SMTO_NOTIMEOUTIFNOTHUNG = 0x0008,
            SMTO_ERRORONEXIT = 0x0020
        }

        /// <summary>
        /// Delegate được gọi bởi hàm EnumWindows để xử lý từng cửa sổ được duyệt qua.
        /// </summary>
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// Duyệt qua danh sách toàn bộ các cửa sổ cấp cao nhất (top-level windows) trên màn hình.
        /// </summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        /// <summary>
        /// Tìm kiếm cửa sổ top-level theo tên lớp Class Name hoặc tên Window Name.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        /// <summary>
        /// Tìm kiếm cửa sổ con của một cửa sổ cha chỉ định theo tên Class hoặc tên Window.
        /// Bắt đầu tìm kiếm từ sau cửa sổ con hwndChildAfter.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(
            IntPtr hwndParent,
            IntPtr hwndChildAfter,
            string? lpszClass,
            string? lpszWindow);

        /// <summary>
        /// Thiết lập cửa sổ cha mới cho một cửa sổ con.
        /// Hàm này giúp nhúng cửa sổ WPF của ứng dụng vào bên dưới các icon desktop bằng cách gán nó làm con của WorkerW.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        /// <summary>
        /// Lấy các thông số hiển thị hoặc cấu hình hệ thống (như độ phân giải màn hình).
        /// </summary>
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0; // Chỉ số chiều rộng màn hình chính
        private const int SM_CYSCREEN = 1; // Chỉ số chiều cao màn hình chính

        /// <summary>
        /// Thay đổi vị trí, kích thước và thứ tự hiển thị Z-order của cửa sổ.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;   // Giữ nguyên thứ tự Z-order hiện có.
        private const uint SWP_SHOWWINDOW = 0x0040; // Hiển thị cửa sổ sau khi định vị lại.

        // Import các hàm Get/Set WindowLong để tùy biến Style cửa sổ tương thích cả x86 và x64.
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLong64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLong64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLong64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLong64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        // Hằng số chỉ số và kiểu dáng của Window Styles
        private const int GWL_STYLE = -16;          // Thay đổi các kiểu dáng cơ bản
        private const int GWL_EXSTYLE = -20;        // Thay đổi các kiểu dáng mở rộng (Extended)
        
        private const long WS_POPUP = 0x80000000;   // Kiểu cửa sổ độc lập (Popup window)
        private const long WS_CHILD = 0x40000000;   // Kiểu cửa sổ con của một cửa sổ khác
        
        private const int WS_EX_LAYERED = 0x00080000;     // Hỗ trợ cửa sổ phân lớp (cho phép vẽ trong suốt)
        private const int WS_EX_TRANSPARENT = 0x00000020; // Cờ click xuyên thấu (Bỏ qua tương tác chuột)

        // P/Invoke & Structures for Low-Level Mouse Hook
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public Win32Point pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Win32Point point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        private const uint GA_ROOT = 2;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private bool IsDesktopWindow(IntPtr hWnd, IntPtr wpfHwnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            if (hWnd == wpfHwnd) return true;
            
            IntPtr root = GetAncestor(hWnd, GA_ROOT);
            if (root == IntPtr.Zero) return false;
            if (root == wpfHwnd) return true;

            var className = new System.Text.StringBuilder(256);
            if (GetClassName(root, className, className.Capacity) > 0)
            {
                string name = className.ToString();
                return name == "WorkerW" || name == "Progman";
            }
            return false;
        }

        // ==========================================
        // CƠ CHẾ KHỞI TẠO VÀ LOGIC CHÍNH
        // ==========================================

        private const string AppVersion = "v1.0.0";
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private string _currentVideoId = "jfKfPfyJRdk"; // ID video hiện tại
        private List<WallpaperPreset> _presets = new();
        private WallpaperPreset? _currentPreset;
        private System.Windows.Forms.ToolStripMenuItem? _presetsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem? _soundMenuItem;
        private System.Windows.Forms.ToolStripMenuItem? _interactiveMenuItem;
        private System.Windows.Forms.ToolStripMenuItem? _zoomMenuItem;
        private bool _isInteractiveMode = false;
        
        private IntPtr _mouseHookId = IntPtr.Zero;
        private LowLevelMouseProc? _mouseProc;
        private uint _lastClickTime = 0;
        private System.Windows.Point _lastClickPoint = new System.Windows.Point();
        private IntPtr _workerwHandle = IntPtr.Zero;
        private IntPtr _wpfHwnd = IntPtr.Zero;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Ghi đè phương thức khởi tạo nguồn của WPF Window.
        /// Đây là thời điểm sớm nhất tay cầm cửa sổ (HWND) được sinh ra và sẵn sàng cho các hàm Win32 API.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
            try { File.WriteAllText(logPath, "OnSourceInitialized started\n"); } catch { }

            try
            {
                _wpfHwnd = new WindowInteropHelper(this).Handle;
                try { File.AppendAllText(logPath, $"WPF HWND: {_wpfHwnd}\n"); } catch { }

                IntPtr progman = FindWindow("Progman", null);
                try { File.AppendAllText(logPath, $"Progman: {progman}\n"); } catch { }

                if (progman == IntPtr.Zero)
                {
                    try { File.AppendAllText(logPath, "Error: Progman not found!\n"); } catch { }
                    return;
                }

                IntPtr result = IntPtr.Zero;
                SendMessageTimeout(
                    progman,
                    0x052C,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    SendMessageTimeoutFlags.SMTO_NORMAL,
                    1000,
                    out result);
                try { File.AppendAllText(logPath, "Sent 0x052C message\n"); } catch { }

                IntPtr workerw = IntPtr.Zero;
                EnumWindows((hwnd, lParam) =>
                {
                    IntPtr shellView = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellView != IntPtr.Zero)
                    {
                        workerw = FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    }
                    return true;
                }, IntPtr.Zero);
                try { File.AppendAllText(logPath, $"WorkerW: {workerw}\n"); } catch { }

                if (workerw != IntPtr.Zero)
                {
                    _workerwHandle = workerw;
                }
                else
                {
                    try { File.AppendAllText(logPath, "Warning: WorkerW not found!\n"); } catch { }
                }

                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);
                SetWindowPos(_wpfHwnd, IntPtr.Zero, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);
                try { File.AppendAllText(logPath, $"Window sized to {screenWidth}x{screenHeight}\n"); } catch { }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(logPath, $"P/Invoke error: {ex}\n"); } catch { }
            }

            try { File.AppendAllText(logPath, "Loading presets\n"); } catch { }
            LoadPresets();

            try { File.AppendAllText(logPath, "Initializing WebView2\n"); } catch { }
            InitializeWebViewAsync();

            try { File.AppendAllText(logPath, "Initializing Tray Icon\n"); } catch { }
            InitializeTrayIcon();

            try { File.AppendAllText(logPath, "Hooking Mouse\n"); } catch { }
            HookMouse();
        }

        private async void InitializeWebViewAsync()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
            try { File.AppendAllText(logPath, "InitializeWebViewAsync started\n"); } catch { }
            try
            {
                string userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2_Cache");
                try { File.AppendAllText(logPath, $"UserDataFolder: {userDataFolder}\n"); } catch { }

                var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
                try { File.AppendAllText(logPath, "Creating CoreWebView2Environment...\n"); } catch { }
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                try { File.AppendAllText(logPath, "CoreWebView2Environment created successfully\n"); } catch { }

                try { File.AppendAllText(logPath, "Calling EnsureCoreWebView2Async...\n"); } catch { }
                await MyWebView.EnsureCoreWebView2Async(env);
                try { File.AppendAllText(logPath, "EnsureCoreWebView2Async finished successfully\n"); } catch { }

                MyWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                MyWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                MyWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                MyWebView.WebMessageReceived += MyWebView_WebMessageReceived;

                string htmlFolder = Path.Combine(userDataFolder, "Html");
                if (!Directory.Exists(htmlFolder))
                {
                    Directory.CreateDirectory(htmlFolder);
                }

                MyWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.local",
                    htmlFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                MyWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "assets.local",
                    AppDomain.CurrentDomain.BaseDirectory,
                    CoreWebView2HostResourceAccessKind.Allow);

                try { File.AppendAllText(logPath, "Calling SetupWallpaperWindow...\n"); } catch { }
                SetupWallpaperWindow();
                try { File.AppendAllText(logPath, "SetupWallpaperWindow finished\n"); } catch { }

                try { File.AppendAllText(logPath, "Calling ApplyCurrentPreset...\n"); } catch { }
                ApplyCurrentPreset();
                try { File.AppendAllText(logPath, "ApplyCurrentPreset finished\n"); } catch { }

                MyWebView.Visibility = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;
                try { File.AppendAllText(logPath, "WebView2 initialization fully completed!\n"); } catch { }
                
                // Kiểm tra cập nhật ứng dụng từ GitHub
                _ = Task.Run(() => CheckForUpdatesAsync());
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(logPath, $"InitializeWebViewAsync error: {ex}\n"); } catch { }
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), ex.ToString()); } catch { }
                TxtStatus.Text = $"Không thể khởi tạo WebView2!\n\nChi tiết: {ex.Message}\n\nHãy đảm bảo máy tính của bạn đã cài đặt Microsoft Edge WebView2 Runtime.";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Tomato;
            }
        }

        private void SetupWallpaperWindow()
        {
            try
            {
                IntPtr wpfHwnd = new WindowInteropHelper(this).Handle;

                // 1. Nhúng cửa sổ WPF vào làm con của WorkerW
                if (_workerwHandle != IntPtr.Zero)
                {
                    SetParent(wpfHwnd, _workerwHandle);
                }

                // 2. Chuyển đổi Window Style của WPF từ POPUP thành CHILD.
                long style = GetWindowLongPtr(wpfHwnd, GWL_STYLE).ToInt64();
                style = (style & ~WS_POPUP) | WS_CHILD;
                SetWindowLongPtr(wpfHwnd, GWL_STYLE, new IntPtr(style));

                // 3. Định vị lại cửa sổ bao phủ toàn bộ màn hình thực tế
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);
                SetWindowPos(wpfHwnd, IntPtr.Zero, 0, 0, screenWidth, screenHeight, SWP_NOZORDER | SWP_SHOWWINDOW);

                // 4. Thiết lập Click-Through
                long exStyle = GetWindowLongPtr(wpfHwnd, GWL_EXSTYLE).ToInt64();
                exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
                SetWindowLongPtr(wpfHwnd, GWL_EXSTYLE, new IntPtr(exStyle));
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), $"SetupWallpaperWindow error: {ex}\n"); } catch { }
            }
        }

        private string GetYouTubeEmbedHtml(string videoId)
        {
            bool isMuted = _currentPreset?.IsMuted ?? true;
            string muteValue = isMuted ? "1" : "0";
            string muteJsCall = isMuted ? "event.target.mute();" : "event.target.unMute();";

            string bgPath = _currentPreset?.BackgroundPath ?? "";
            // Replace backslashes with forward slashes for URL path
            string bgUrl = string.IsNullOrEmpty(bgPath) ? "" : $"http://assets.local/{bgPath.Replace('\\', '/')}";
            string bgDisplay = string.IsNullOrEmpty(bgPath) ? "none" : "block";

            double startSeconds = _currentPreset?.LastTimestamp ?? 0.0;
            string startSecondsStr = startSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

            double x0 = _currentPreset?.Is3D == true ? _currentPreset.X0 : (_currentPreset?.X ?? 0);
            double y0 = _currentPreset?.Is3D == true ? _currentPreset.Y0 : (_currentPreset?.Y ?? 0);
            double x1 = _currentPreset?.Is3D == true ? _currentPreset.X1 : ((_currentPreset?.X ?? 0) + (_currentPreset?.Width ?? 1));
            double y1 = _currentPreset?.Is3D == true ? _currentPreset.Y1 : (_currentPreset?.Y ?? 0);
            double x2 = _currentPreset?.Is3D == true ? _currentPreset.X2 : ((_currentPreset?.X ?? 0) + (_currentPreset?.Width ?? 1));
            double y2 = _currentPreset?.Is3D == true ? _currentPreset.Y2 : ((_currentPreset?.Y ?? 0) + (_currentPreset?.Height ?? 1));
            double x3 = _currentPreset?.Is3D == true ? _currentPreset.X3 : (_currentPreset?.X ?? 0);
            double y3 = _currentPreset?.Is3D == true ? _currentPreset.Y3 : ((_currentPreset?.Y ?? 0) + (_currentPreset?.Height ?? 1));

            string x0Str = x0.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string y0Str = y0.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string x1Str = x1.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string y1Str = y1.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string x2Str = x2.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string y2Str = y2.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string x3Str = x3.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string y3Str = y3.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
    <style>
        * {{
            margin: 0;
            padding: 0;
            overflow: hidden;
            box-sizing: border-box;
            background-color: transparent !important;
        }}
        html, body {{
            width: 100%;
            height: 100%;
            margin: 0;
            padding: 0;
            overflow: hidden;
            background-color: transparent !important;
        }}
        .video-container {{
            position: absolute;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            z-index: 1;
            transform-origin: 0 0;
        }}
        #player {{
            width: 100%;
            height: 100%;
            border: none;
            pointer-events: none;
        }}
        #bg-image {{
            position: absolute;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            z-index: 10;
            pointer-events: none;
            display: {bgDisplay};
            object-fit: fill;
        }}
    </style>
</head>
<body>
    <div class=""video-container"">
        <div id=""player""></div>
    </div>
    
    <img id=""bg-image"" src=""{bgUrl}"" />
    
    <script>
        var tag = document.createElement('script');
        tag.src = ""https://www.youtube.com/iframe_api"";
        var firstScriptTag = document.getElementsByTagName('script')[0];
        firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);

        var player;
        function onYouTubeIframeAPIReady() {{
            player = new YT.Player('player', {{
                height: '100%',
                width: '100%',
                videoId: '{videoId}',
                playerVars: {{
                    'autoplay': 1,
                    'mute': {muteValue},
                    'loop': 1,
                    'playlist': '{videoId}',
                    'controls': 1,
                    'disablekb': 0,
                    'fs': 0,
                    'modestbranding': 1,
                    'iv_load_policy': 3,
                    'rel': 0,
                    'showinfo': 0
                }},
                events: {{
                    'onReady': onPlayerReady,
                    'onStateChange': onPlayerStateChange
                }}
            }});
        }}

        function onPlayerReady(event) {{
            {muteJsCall}
            var startSec = {startSecondsStr};
            if (startSec > 0) {{
                event.target.seekTo(startSec, true);
            }}
            event.target.playVideo();
        }}

        function onPlayerStateChange(event) {{
            if (event.data === YT.PlayerState.ENDED) {{
                player.playVideo();
            }}
        }}

        var isInteractiveActive = false;
        var corners = [{x0Str}, {y0Str}, {x1Str}, {y1Str}, {x2Str}, {y2Str}, {x3Str}, {y3Str}];

        function setInteractive(active) {{
            isInteractiveActive = active;
            var playerEl = document.getElementById('player');
            if (playerEl) {{
                playerEl.style.pointerEvents = active ? 'auto' : 'none';
            }}
        }}

        function updateTransform(x0, y0, x1, y1, x2, y2, x3, y3) {{
            corners = [x0, y0, x1, y1, x2, y2, x3, y3];
            var w = window.innerWidth;
            var h = window.innerHeight;

            var p0 = {{ x: x0 * w, y: y0 * h }};
            var p1 = {{ x: x1 * w, y: y1 * h }};
            var p2 = {{ x: x2 * w, y: y2 * h }};
            var p3 = {{ x: x3 * w, y: y3 * h }};

            var dx1 = p1.x - p2.x;
            var dx2 = p3.x - p2.x;
            var sx = p0.x - p1.x + p2.x - p3.x;
            var dy1 = p1.y - p2.y;
            var dy2 = p3.y - p2.y;
            var sy = p0.y - p1.y + p2.y - p3.y;

            var h00, h01, h02, h10, h11, h12, h20, h21;

            if (Math.abs(sx) < 1e-5 && Math.abs(sy) < 1e-5) {{
                h00 = p1.x - p0.x;
                h01 = p2.x - p1.x;
                h02 = p0.x;
                h10 = p1.y - p0.y;
                h11 = p2.y - p1.y;
                h12 = p0.y;
                h20 = 0;
                h21 = 0;
            }} else {{
                var den = dx1 * dy2 - dx2 * dy1;
                if (Math.abs(den) < 1e-5) return;
                var g = (sx * dy2 - sy * dx2) / den;
                var h_val = (dx1 * sy - dy1 * sx) / den;

                h00 = p1.x - p0.x + g * p1.x;
                h01 = p3.x - p0.x + h_val * p3.x;
                h02 = p0.x;
                h10 = p1.y - p0.y + g * p1.y;
                h11 = p3.y - p0.y + h_val * p3.y;
                h12 = p0.y;
                h20 = g;
                h21 = h_val;
            }}

            var m11 = h00 / w;
            var m12 = h10 / w;
            var m14 = h20 / w;
            var m21 = h01 / h;
            var m22 = h11 / h;
            var m24 = h21 / h;
            var m41 = h02;
            var m42 = h12;
            var m44 = 1;

            var transformStr = ""matrix3d("" + 
                m11 + "","" + m12 + "",0,"" + m14 + "","" +
                m21 + "","" + m22 + "",0,"" + m24 + "",0,0,1,0,"" +
                m41 + "","" + m42 + "",0,"" + m44 + "")"";

            var container = document.querySelector('.video-container');
            if (container) {{
                container.style.transform = transformStr;
                container.style.transformOrigin = ""0 0"";
            }}
        }}

        window.addEventListener('resize', function() {{
            updateTransform(corners[0], corners[1], corners[2], corners[3], corners[4], corners[5], corners[6], corners[7]);
        }});

        setTimeout(function() {{
            updateTransform(corners[0], corners[1], corners[2], corners[3], corners[4], corners[5], corners[6], corners[7]);
        }}, 100);

        document.addEventListener('click', function(e) {{
            if (!isInteractiveActive) return;
            
            var container = document.querySelector('.video-container');
            if (container && !container.contains(e.target)) {{
                window.chrome.webview.postMessage(""lock_interaction"");
            }}
        }});

        // Gửi báo cáo vị trí phát (timestamp) mỗi 3 giây về ứng dụng WPF
        setInterval(function() {{
            if (player && typeof player.getCurrentTime === 'function' && typeof player.getPlayerState === 'function') {{
                var state = player.getPlayerState();
                if (state === 1 || state === 2 || state === 3) {{
                    var currentTime = player.getCurrentTime();
                    window.chrome.webview.postMessage(JSON.stringify({{
                        type: ""playback_time"",
                        time: currentTime
                    }}));
                }}
            }}
        }}, 3000);
    </script>
</body>
</html>";
        }

        private void LoadPresets()
        {
            try
            {
                string presetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets.json");
                if (File.Exists(presetPath))
                {
                    string json = File.ReadAllText(presetPath);
                    _presets = JsonSerializer.Deserialize<List<WallpaperPreset>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi tải Presets: {ex.Message}", "Lỗi Presets", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (_presets.Count == 0)
            {
                _presets.Add(new WallpaperPreset
                {
                    Name = "Toàn màn hình",
                    BackgroundPath = "",
                    X = 0,
                    Y = 0,
                    Width = 1,
                    Height = 1,
                    VideoId = "jfKfPfyJRdk"
                });
                
                _presets.Add(new WallpaperPreset
                {
                    Name = "Phòng khách TV (bg1.png)",
                    BackgroundPath = "backgroud\\bg1.png",
                    X = 0.337,
                    Y = 0.328,
                    Width = 0.325,
                    Height = 0.318,
                    VideoId = "jfKfPfyJRdk"
                });
                
                SavePresets();
            }

            _currentPreset = _presets.Find(p => p.IsLastActive) ?? _presets.Find(p => !string.IsNullOrEmpty(p.BackgroundPath)) ?? _presets[0];
            foreach (var p in _presets)
            {
                p.IsLastActive = (p == _currentPreset);
            }
            _currentVideoId = _currentPreset.VideoId;
        }

        private void SavePresets()
        {
            try
            {
                string presetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_presets, options);
                File.WriteAllText(presetPath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi lưu Presets: {ex.Message}", "Lỗi Presets", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyCurrentPreset()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
            try { File.AppendAllText(logPath, $"ApplyCurrentPreset: _currentPreset is {(this._currentPreset != null ? "not null" : "null")}\n"); } catch { }
            if (_currentPreset == null) return;

            this.Dispatcher.Invoke(() =>
            {
                // WebView luôn chiếm toàn bộ màn hình
                MyWebView.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                MyWebView.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                MyWebView.Width = double.NaN;
                MyWebView.Height = double.NaN;
                MyWebView.Margin = new Thickness(0);

                // Thiết lập tỷ lệ phóng to giao diện WebView2
                MyWebView.ZoomFactor = _currentPreset.ZoomFactor > 0 ? _currentPreset.ZoomFactor : 1.0;

                try { File.AppendAllText(logPath, $"ApplyCurrentPreset: _currentVideoId={_currentVideoId}, PresetVideoId={_currentPreset.VideoId}, WebViewSource={MyWebView.Source}, ZoomFactor={MyWebView.ZoomFactor}\n"); } catch { }

                if (_currentVideoId != _currentPreset.VideoId || 
                    MyWebView.Source == null || 
                    MyWebView.Source.ToString() == "about:blank" || 
                    !MyWebView.Source.ToString().Contains("app.local"))
                {
                    _currentVideoId = _currentPreset.VideoId;
                    try { File.AppendAllText(logPath, "ApplyCurrentPreset: Calling ReloadYouTubeVideo...\n"); } catch { }
                    ReloadYouTubeVideo();
                }
                else
                {
                    try { File.AppendAllText(logPath, "ApplyCurrentPreset: Calling UpdateHtmlBackgroundAndCoordinates...\n"); } catch { }
                    UpdateHtmlBackgroundAndCoordinates();
                }
            });
        }

        private void UpdateHtmlBackgroundAndCoordinates()
        {
            if (_currentPreset == null || MyWebView.CoreWebView2 == null) return;

            this.Dispatcher.Invoke(() =>
            {
                string bgPath = _currentPreset.BackgroundPath ?? "";
                string bgUrl = string.IsNullOrEmpty(bgPath) ? "" : $"http://assets.local/{bgPath.Replace('\\', '/')}";
                string bgDisplay = string.IsNullOrEmpty(bgPath) ? "none" : "block";

                double x0 = _currentPreset.Is3D ? _currentPreset.X0 : _currentPreset.X;
                double y0 = _currentPreset.Is3D ? _currentPreset.Y0 : _currentPreset.Y;
                double x1 = _currentPreset.Is3D ? _currentPreset.X1 : (_currentPreset.X + _currentPreset.Width);
                double y1 = _currentPreset.Is3D ? _currentPreset.Y1 : _currentPreset.Y;
                double x2 = _currentPreset.Is3D ? _currentPreset.X2 : (_currentPreset.X + _currentPreset.Width);
                double y2 = _currentPreset.Is3D ? _currentPreset.Y2 : (_currentPreset.Y + _currentPreset.Height);
                double x3 = _currentPreset.Is3D ? _currentPreset.X3 : _currentPreset.X;
                double y3 = _currentPreset.Is3D ? _currentPreset.Y3 : (_currentPreset.Y + _currentPreset.Height);

                string x0Str = x0.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string y0Str = y0.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string x1Str = x1.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string y1Str = y1.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string x2Str = x2.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string y2Str = y2.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string x3Str = x3.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string y3Str = y3.ToString(System.Globalization.CultureInfo.InvariantCulture);

                string js = $@"
                    (function() {{
                        var bg = document.getElementById('bg-image');
                        if (bg) {{
                            bg.src = '{bgUrl}';
                            bg.style.display = '{bgDisplay}';
                        }}
                        if (typeof updateTransform === 'function') {{
                            updateTransform({x0Str}, {y0Str}, {x1Str}, {y1Str}, {x2Str}, {y2Str}, {x3Str}, {y3Str});
                        }}
                    }})();
                ";
                MyWebView.CoreWebView2.ExecuteScriptAsync(js);
            });
        }

        private void ReloadYouTubeVideo()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
            try { File.AppendAllText(logPath, "ReloadYouTubeVideo: started\n"); } catch { }
            if (MyWebView.CoreWebView2 == null)
            {
                try { File.AppendAllText(logPath, "ReloadYouTubeVideo: MyWebView.CoreWebView2 is null!\n"); } catch { }
                return;
            }

            string userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2_Cache");
            string htmlFolder = Path.Combine(userDataFolder, "Html");
            string htmlPath = Path.Combine(htmlFolder, "index.html");
            try { File.AppendAllText(logPath, $"ReloadYouTubeVideo: htmlPath={htmlPath}\n"); } catch { }
            
            string html = GetYouTubeEmbedHtml(_currentVideoId);
            
            if (!Directory.Exists(htmlFolder))
            {
                Directory.CreateDirectory(htmlFolder);
            }
            File.WriteAllText(htmlPath, html, System.Text.Encoding.UTF8);
            try { File.AppendAllText(logPath, "ReloadYouTubeVideo: index.html written successfully\n"); } catch { }

            MyWebView.CoreWebView2.Navigate("http://app.local/index.html");
            try { File.AppendAllText(logPath, "ReloadYouTubeVideo: Navigate called\n"); } catch { }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                _notifyIcon.Text = "Desktop Video Wallpaper";
                
                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(16, 16))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(System.Drawing.Color.Transparent);
                        
                        using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(79, 70, 229)))
                        {
                            g.FillEllipse(brush, 1, 1, 14, 14);
                        }
                        
                        using (System.Drawing.SolidBrush playBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                        {
                            System.Drawing.PointF[] points = {
                                new System.Drawing.PointF(6f, 4.5f),
                                new System.Drawing.PointF(6f, 11.5f),
                                new System.Drawing.PointF(11.5f, 8f)
                            };
                            g.FillPolygon(playBrush, points);
                        }
                    }
                    IntPtr hIcon = bmp.GetHicon();
                    _notifyIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
                }
                
                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                
                var editItem = new System.Windows.Forms.ToolStripMenuItem("Thay đổi Link Video");
                editItem.Click += (s, e) => ShowEditLinkDialog();

                _soundMenuItem = new System.Windows.Forms.ToolStripMenuItem("Phát kèm âm thanh");
                _soundMenuItem.Checked = !(_currentPreset?.IsMuted ?? true);
                _soundMenuItem.Click += (s, e) =>
                {
                    if (_currentPreset == null || _soundMenuItem == null) return;
                    _currentPreset.IsMuted = !_currentPreset.IsMuted;
                    _soundMenuItem.Checked = !_currentPreset.IsMuted;
                    SavePresets();

                    if (MyWebView.CoreWebView2 != null)
                    {
                        string js = _currentPreset.IsMuted 
                            ? "if (typeof player !== 'undefined') { player.mute(); }" 
                            : "if (typeof player !== 'undefined') { player.unMute(); }";
                        MyWebView.CoreWebView2.ExecuteScriptAsync(js);
                    }
                };

                _interactiveMenuItem = new System.Windows.Forms.ToolStripMenuItem("Tương tác với Video (Tua/Âm lượng)");
                _interactiveMenuItem.Checked = _isInteractiveMode;
                _interactiveMenuItem.Click += (s, e) =>
                {
                    ToggleInteractiveMode(!_isInteractiveMode);
                };

                _zoomMenuItem = new System.Windows.Forms.ToolStripMenuItem("Tỷ lệ giao diện điều khiển (Zoom)");
                BuildZoomMenu();

                _presetsMenuItem = new System.Windows.Forms.ToolStripMenuItem("Chọn nền máy tính / Preset");
                BuildPresetsMenu();

                var calibrateItem = new System.Windows.Forms.ToolStripMenuItem("Cân chỉnh Vị trí Video");
                calibrateItem.Click += (s, e) => ShowCalibrationDialog();

                var changeBgItem = new System.Windows.Forms.ToolStripMenuItem("Thay đổi ảnh nền Preset...");
                changeBgItem.Click += (s, e) => ChangePresetBackground();

                var newPresetItem = new System.Windows.Forms.ToolStripMenuItem("Thêm Preset mới...");
                newPresetItem.Click += (s, e) => CreateNewPreset();

                var deletePresetItem = new System.Windows.Forms.ToolStripMenuItem("Xóa Preset hiện tại...");
                deletePresetItem.Click += (s, e) => DeleteCurrentPreset();

                var exitItem = new System.Windows.Forms.ToolStripMenuItem("Thoát ứng dụng");
                exitItem.Click += (s, e) => ExitApplication();
                
                contextMenu.Items.Add(editItem);
                contextMenu.Items.Add(_soundMenuItem);
                contextMenu.Items.Add(_interactiveMenuItem);
                contextMenu.Items.Add(_zoomMenuItem);
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add(_presetsMenuItem);
                contextMenu.Items.Add(calibrateItem);
                contextMenu.Items.Add(changeBgItem);
                contextMenu.Items.Add(newPresetItem);
                contextMenu.Items.Add(deletePresetItem);
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add(exitItem);
                
                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.Visible = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi khởi tạo Tray Icon: {ex.Message}", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildPresetsMenu()
        {
            if (_presetsMenuItem == null) return;
            _presetsMenuItem.DropDownItems.Clear();
            foreach (var preset in _presets)
            {
                var item = new System.Windows.Forms.ToolStripMenuItem(preset.Name);
                item.Checked = (preset == _currentPreset);
                var capturedPreset = preset;
                item.Click += (s, e) =>
                {
                    _currentPreset = capturedPreset;
                    foreach (var p in _presets)
                    {
                        p.IsLastActive = (p == _currentPreset);
                    }
                    ApplyCurrentPreset();
                    SavePresets();
                    BuildPresetsMenu();
                    BuildZoomMenu();
                    if (_soundMenuItem != null)
                    {
                        _soundMenuItem.Checked = !_currentPreset.IsMuted;
                    }
                };
                _presetsMenuItem.DropDownItems.Add(item);
            }
        }

        private void BuildZoomMenu()
        {
            if (_zoomMenuItem == null) return;
            _zoomMenuItem.DropDownItems.Clear();

            double[] zoomLevels = { 1.0, 1.25, 1.5, 1.75, 2.0, 2.5 };
            foreach (var zoom in zoomLevels)
            {
                var item = new System.Windows.Forms.ToolStripMenuItem($"{zoom * 100}%");
                item.Checked = (Math.Abs((_currentPreset?.ZoomFactor ?? 1.0) - zoom) < 0.05);
                var capturedZoom = zoom;
                item.Click += (s, e) =>
                {
                    if (_currentPreset != null)
                    {
                        _currentPreset.ZoomFactor = capturedZoom;
                        SavePresets();
                        if (MyWebView != null)
                        {
                            MyWebView.ZoomFactor = capturedZoom;
                        }
                        BuildZoomMenu();
                    }
                };
                _zoomMenuItem.DropDownItems.Add(item);
            }
        }

        private void ApplyPresetCoordinatesOnly()
        {
            UpdateHtmlBackgroundAndCoordinates();
        }

        private void ShowCalibrationDialog()
        {
            if (_currentPreset == null) return;

            this.Dispatcher.Invoke(() =>
            {
                double originalX = _currentPreset.X;
                double originalY = _currentPreset.Y;
                double originalW = _currentPreset.Width;
                double originalH = _currentPreset.Height;
                double originalX0 = _currentPreset.X0;
                double originalY0 = _currentPreset.Y0;
                double originalX1 = _currentPreset.X1;
                double originalY1 = _currentPreset.Y1;
                double originalX2 = _currentPreset.X2;
                double originalY2 = _currentPreset.Y2;
                double originalX3 = _currentPreset.X3;
                double originalY3 = _currentPreset.Y3;
                bool originalIs3D = _currentPreset.Is3D;

                string fullBgPath = string.Empty;
                if (!string.IsNullOrEmpty(_currentPreset.BackgroundPath))
                {
                    fullBgPath = Path.IsPathRooted(_currentPreset.BackgroundPath)
                        ? _currentPreset.BackgroundPath
                        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _currentPreset.BackgroundPath);
                }

                var calWindow = new CalibrationWindow(fullBgPath, _currentPreset);

                calWindow.PositionChanged += (s, args) =>
                {
                    _currentPreset.X = args.X;
                    _currentPreset.Y = args.Y;
                    _currentPreset.Width = args.Width;
                    _currentPreset.Height = args.Height;
                    _currentPreset.X0 = args.X0;
                    _currentPreset.Y0 = args.Y0;
                    _currentPreset.X1 = args.X1;
                    _currentPreset.Y1 = args.Y1;
                    _currentPreset.X2 = args.X2;
                    _currentPreset.Y2 = args.Y2;
                    _currentPreset.X3 = args.X3;
                    _currentPreset.Y3 = args.Y3;
                    _currentPreset.Is3D = args.Is3D;

                    ApplyPresetCoordinatesOnly();
                };

                if (calWindow.ShowDialog() == true)
                {
                    _currentPreset.X = calWindow.ResultX;
                    _currentPreset.Y = calWindow.ResultY;
                    _currentPreset.Width = calWindow.ResultWidth;
                    _currentPreset.Height = calWindow.ResultHeight;
                    _currentPreset.X0 = calWindow.ResultX0;
                    _currentPreset.Y0 = calWindow.ResultY0;
                    _currentPreset.X1 = calWindow.ResultX1;
                    _currentPreset.Y1 = calWindow.ResultY1;
                    _currentPreset.X2 = calWindow.ResultX2;
                    _currentPreset.Y2 = calWindow.ResultY2;
                    _currentPreset.X3 = calWindow.ResultX3;
                    _currentPreset.Y3 = calWindow.ResultY3;
                    _currentPreset.Is3D = calWindow.ResultIs3D;

                    ApplyCurrentPreset();
                    SavePresets();
                }
                else
                {
                    _currentPreset.X = originalX;
                    _currentPreset.Y = originalY;
                    _currentPreset.Width = originalW;
                    _currentPreset.Height = originalH;
                    _currentPreset.X0 = originalX0;
                    _currentPreset.Y0 = originalY0;
                    _currentPreset.X1 = originalX1;
                    _currentPreset.Y1 = originalY1;
                    _currentPreset.X2 = originalX2;
                    _currentPreset.Y2 = originalY2;
                    _currentPreset.X3 = originalX3;
                    _currentPreset.Y3 = originalY3;
                    _currentPreset.Is3D = originalIs3D;

                    ApplyPresetCoordinatesOnly();
                }
            });
        }

        private void ChangePresetBackground()
        {
            if (_currentPreset == null) return;

            this.Dispatcher.Invoke(() =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Chọn ảnh nền cho Preset"
                };

                if (ofd.ShowDialog() == true)
                {
                    string bgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backgroud");
                    if (!Directory.Exists(bgDir))
                    {
                        Directory.CreateDirectory(bgDir);
                    }

                    string destName = Path.GetFileName(ofd.FileName);
                    string destPath = Path.Combine(bgDir, destName);
                    
                    try
                    {
                        if (ofd.FileName != destPath)
                        {
                            File.Copy(ofd.FileName, destPath, true);
                        }
                        
                        _currentPreset.BackgroundPath = Path.Combine("backgroud", destName);
                        
                        ApplyCurrentPreset();
                        SavePresets();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Không thể copy ảnh nền: {ex.Message}", "Lỗi hình ảnh", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }

        private void CreateNewPreset()
        {
            this.Dispatcher.Invoke(() =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Chọn ảnh nền cho Preset mới"
                };

                if (ofd.ShowDialog() == true)
                {
                    string bgDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backgroud");
                    if (!Directory.Exists(bgDir))
                    {
                        Directory.CreateDirectory(bgDir);
                    }

                    string destName = Path.GetFileName(ofd.FileName);
                    string destPath = Path.Combine(bgDir, destName);

                    try
                    {
                        if (ofd.FileName != destPath)
                        {
                            File.Copy(ofd.FileName, destPath, true);
                        }

                        var newPreset = new WallpaperPreset
                        {
                            Name = Path.GetFileNameWithoutExtension(destName),
                            BackgroundPath = Path.Combine("backgroud", destName),
                            X = 0.2,
                            Y = 0.2,
                            Width = 0.6,
                            Height = 0.6,
                            VideoId = _currentVideoId
                        };

                        _presets.Add(newPreset);
                        _currentPreset = newPreset;
                        foreach (var p in _presets)
                        {
                            p.IsLastActive = (p == _currentPreset);
                        }
                        
                        ApplyCurrentPreset();
                        SavePresets();
                        BuildPresetsMenu();
                        
                        System.Windows.MessageBox.Show("Preset mới đã được tạo. Hãy dùng menu chuột phải chọn 'Cân chỉnh Vị trí Video' để đưa video vào đúng góc TV của bạn!", "Tạo Preset thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Lỗi tạo Preset mới: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }

        private void DeleteCurrentPreset()
        {
            if (_currentPreset == null) return;

            this.Dispatcher.Invoke(() =>
            {
                if (_presets.Count <= 1)
                {
                    System.Windows.MessageBox.Show("Không thể xóa preset cuối cùng của ứng dụng!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = System.Windows.MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa preset '{_currentPreset.Name}' không?", 
                    "Xác nhận xóa Preset", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _presets.Remove(_currentPreset);
                    _currentPreset = _presets[0];

                    ApplyCurrentPreset();
                    SavePresets();
                    BuildPresetsMenu();
                    BuildZoomMenu();
                }
            });
        }

        private void ShowEditLinkDialog()
        {
            this.Dispatcher.Invoke(() =>
            {
                var currentUrl = $"https://www.youtube.com/watch?v={_currentVideoId}";
                var dlg = new LinkInputDialog(currentUrl)
                {
                    Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                if (dlg.ShowDialog() == true)
                {
                    string newId = ExtractVideoId(dlg.ResultUrl);
                    if (!string.IsNullOrWhiteSpace(newId) && newId != _currentVideoId)
                    {
                        _currentVideoId = newId;
                        if (_currentPreset != null)
                        {
                            _currentPreset.VideoId = newId;
                            SavePresets();
                        }
                        ReloadYouTubeVideo();
                    }
                }
            });
        }

        private void ToggleInteractiveMode(bool enable)
        {
            _isInteractiveMode = enable;
            var helper = new WindowInteropHelper(this);
            IntPtr wpfHwnd = helper.Handle;
            long style = GetWindowLongPtr(wpfHwnd, GWL_STYLE).ToInt64();
            long extendedStyle = GetWindowLongPtr(wpfHwnd, GWL_EXSTYLE).ToInt64();
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            if (_isInteractiveMode)
            {
                // Đưa ra làm cửa sổ Top-level (không làm con của WorkerW nữa để nhận click)
                SetParent(wpfHwnd, IntPtr.Zero);
                
                // Đổi Style từ CHILD sang POPUP
                style = (style & ~WS_CHILD) | WS_POPUP;
                SetWindowLongPtr(wpfHwnd, GWL_STYLE, new IntPtr(style));

                // Bỏ click xuyên thấu
                extendedStyle &= ~WS_EX_TRANSPARENT;
                SetWindowLongPtr(wpfHwnd, GWL_EXSTYLE, new IntPtr(extendedStyle));

                // Đặt lên trên cùng và định vị lại kích thước fullscreen
                this.Topmost = true;
                SetWindowPos(wpfHwnd, new IntPtr(-1), 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);

                this.Dispatcher.Invoke(() =>
                {
                    this.Activate(); // Tập trung tiêu điểm chuột
                    this.Focus();
                });
            }
            else
            {
                // Tắt topmost
                this.Topmost = false;

                // Trả về làm Child Window của WorkerW
                style = (style & ~WS_POPUP) | WS_CHILD;
                SetWindowLongPtr(wpfHwnd, GWL_STYLE, new IntPtr(style));

                if (_workerwHandle != IntPtr.Zero)
                {
                    SetParent(wpfHwnd, _workerwHandle);
                }

                // Kích hoạt lại click xuyên thấu
                extendedStyle |= WS_EX_TRANSPARENT;
                SetWindowLongPtr(wpfHwnd, GWL_EXSTYLE, new IntPtr(extendedStyle));

                // Định vị lại phía sau icon desktop
                SetWindowPos(wpfHwnd, IntPtr.Zero, 0, 0, screenWidth, screenHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
            }

            // Cập nhật trạng thái hiển thị của YouTube Player (cho phép click và hiện thanh điều khiển) qua JS
            if (MyWebView.CoreWebView2 != null)
            {
                string js = $"setInteractive({(enable ? "true" : "false")});";
                MyWebView.CoreWebView2.ExecuteScriptAsync(js);
            }

            // Cập nhật trạng thái menu khay hệ thống
            if (_interactiveMenuItem != null)
            {
                _interactiveMenuItem.Checked = _isInteractiveMode;
            }
        }

        private void MyWebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                if (message == "lock_interaction")
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        ToggleInteractiveMode(false);
                    });
                }
                else if (!string.IsNullOrEmpty(message) && message.StartsWith("{"))
                {
                    var msgData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message);
                    if (msgData != null && msgData.TryGetValue("type", out var typeElement) && typeElement.GetString() == "playback_time")
                    {
                        if (msgData.TryGetValue("time", out var timeElement) && timeElement.TryGetDouble(out double seconds))
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                if (_currentPreset != null)
                                {
                                    if (Math.Abs(_currentPreset.LastTimestamp - seconds) >= 5)
                                    {
                                        _currentPreset.LastTimestamp = seconds;
                                        SavePresets();
                                    }
                                }
                            });
                        }
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }

        private void HookMouse()
        {
            _mouseProc = HookCallback;
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                if (curModule != null && curModule.ModuleName != null)
                {
                    _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        private void UnhookMouse()
        {
            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                uint currentTime = hookStruct.time;
                
                uint doubleClickTime = GetDoubleClickTime();
                if (currentTime - _lastClickTime <= doubleClickTime)
                {
                    double distance = Math.Sqrt(Math.Pow(hookStruct.pt.x - _lastClickPoint.X, 2) + Math.Pow(hookStruct.pt.y - _lastClickPoint.Y, 2));
                    if (distance < 10) // Click gần nhau
                    {
                        if (!_isInteractiveMode && _currentPreset != null)
                        {
                            IntPtr clickedHwnd = WindowFromPoint(hookStruct.pt);
                            if (IsDesktopWindow(clickedHwnd, _wpfHwnd))
                            {
                                double screenWidth = SystemParameters.PrimaryScreenWidth;
                                double screenHeight = SystemParameters.PrimaryScreenHeight;

                                double videoLeft = _currentPreset.X * screenWidth;
                                double videoTop = _currentPreset.Y * screenHeight;
                                double videoWidth = _currentPreset.Width * screenWidth;
                                double videoHeight = _currentPreset.Height * screenHeight;

                                if (hookStruct.pt.x >= videoLeft && hookStruct.pt.x <= videoLeft + videoWidth &&
                                    hookStruct.pt.y >= videoTop && hookStruct.pt.y <= videoTop + videoHeight)
                                {
                                    this.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        ToggleInteractiveMode(true);
                                    }));
                                }
                            }
                        }
                    }
                }
                
                _lastClickTime = currentTime;
                _lastClickPoint = new System.Windows.Point(hookStruct.pt.x, hookStruct.pt.y);
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private void ExitApplication()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                System.Windows.Application.Current.Shutdown();
            });
        }

        /// <summary>
        /// Hàm giải trích để trích xuất Video ID từ các định dạng đường dẫn YouTube khác nhau.
        /// </summary>
        private string ExtractVideoId(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId)) return "jfKfPfyJRdk";
            urlOrId = urlOrId.Trim();
            
            if (urlOrId.Length == 11 && !urlOrId.Contains("/") && !urlOrId.Contains(".") && !urlOrId.Contains("?"))
            {
                return urlOrId;
            }

            try
            {
                if (urlOrId.Contains("v="))
                {
                    var parts = urlOrId.Split(new[] { "v=" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        string id = parts[1];
                        int ampIndex = id.IndexOf('&');
                        if (ampIndex != -1) id = id.Substring(0, ampIndex);
                        if (id.Length == 11) return id;
                    }
                }

                if (urlOrId.Contains("youtu.be/"))
                {
                    var parts = urlOrId.Split(new[] { "youtu.be/" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        string id = parts[1];
                        int queryIndex = id.IndexOf('?');
                        if (queryIndex != -1) id = id.Substring(0, queryIndex);
                        if (id.Length == 11) return id;
                    }
                }

                if (urlOrId.Contains("/live/"))
                {
                    var parts = urlOrId.Split(new[] { "/live/" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        string id = parts[1];
                        int queryIndex = id.IndexOf('?');
                        if (queryIndex != -1) id = id.Substring(0, queryIndex);
                        if (id.Length == 11) return id;
                    }
                }

                if (urlOrId.Contains("/embed/"))
                {
                    var parts = urlOrId.Split(new[] { "/embed/" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        string id = parts[1];
                        int queryIndex = id.IndexOf('?');
                        if (queryIndex != -1) id = id.Substring(0, queryIndex);
                        if (id.Length == 11) return id;
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return urlOrId;
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; DesktopVideoWallpaper/1.0)");
                    var localVerStr = AppVersion.Replace("v", "").Trim();
                    var localVer = new Version(localVerStr);

                    string json = await client.GetStringAsync("https://api.github.com/repos/thaovd/Desktop-YouTube-Wallpaper/releases/latest");
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("tag_name", out var tagProp))
                        {
                            string remoteTag = tagProp.GetString() ?? "";
                            string remoteVerStr = remoteTag.Replace("v", "").Trim();
                            if (Version.TryParse(remoteVerStr, out Version? remoteVer) && remoteVer > localVer)
                            {
                                string downloadUrl = "";
                                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var asset in assetsProp.EnumerateArray())
                                    {
                                        if (asset.TryGetProperty("name", out var nameProp) && 
                                            asset.TryGetProperty("browser_download_url", out var urlProp))
                                        {
                                            string name = nameProp.GetString() ?? "";
                                            if (name.EndsWith(".exe"))
                                            {
                                                downloadUrl = urlProp.GetString() ?? "";
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(downloadUrl))
                                {
                                    string finalDownloadUrl = downloadUrl;
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        var result = System.Windows.MessageBox.Show(
                                            $"Đã có phiên bản mới {remoteTag}! Bạn có muốn tải về và cài đặt cập nhật ngay không?",
                                            "Cập nhật ứng dụng",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                                        if (result == MessageBoxResult.Yes)
                                        {
                                            Task.Run(() => DownloadAndRunInstaller(finalDownloadUrl));
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
                try { File.AppendAllText(logPath, $"CheckForUpdatesAsync error: {ex.Message}\n"); } catch { }
            }
        }

        private async Task DownloadAndRunInstaller(string downloadUrl)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), "DesktopVideoWallpaper_Setup_Update.exe");
                
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; DesktopVideoWallpaper/1.0)");
                    using (var response = await client.GetAsync(downloadUrl))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                    }
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                });

                this.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"Lỗi khi tải hoặc cài đặt bản cập nhật: {ex.Message}", "Lỗi cập nhật", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            UnhookMouse();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnClosed(e);
        }
    }
}
