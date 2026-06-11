# Desktop YouTube Video Wallpaper (WPF + Win32 API)

Ứng dụng WPF (.NET 10.0) hiện đại cho phép bạn nhúng bất kỳ video hoặc luồng phát trực tiếp (live stream) YouTube nào làm hình nền động cho máy tính của mình. 

Ứng dụng này sử dụng các kỹ thuật lập trình hệ thống Win32 API (P/Invoke) để nhúng cửa sổ WebView2 bên dưới các biểu tượng màn hình chính (Desktop Icons), hỗ trợ vẽ phối cảnh 3D và tự động lưu/khôi phục trạng thái phát.

---

## ✨ Các tính năng nổi bật

1. **Nhúng Video Xuống Dưới Desktop Icons**: Sử dụng thông điệp `0x052C` để ra lệnh cho `Progman` phân tách lớp nền, tìm cửa sổ `WorkerW` và gán cửa sổ ứng dụng làm con của nó. Hỗ trợ click xuyên thấu (`WS_EX_TRANSPARENT`) để bạn làm việc bình thường.
2. **Căn chỉnh góc 3D (Perspective Matrix)**: Hỗ trợ biến đổi phối cảnh 3D (Homography Matrix) thông qua CSS `matrix3d` để khớp video hoàn hảo vào màn hình TV/khung tranh có góc nghiêng chéo trên ảnh nền.
3. **Chế độ Tương tác (Interactive Mode)**: Click đúp chuột vào vùng video trên màn hình để mở khóa tương tác (cho phép tạm dừng, tua, đổi độ phân giải, chọn video liên quan). Click ra ngoài vùng video để tự động khóa lại.
4. **Bộ lọc Click thông minh (Smart Click Filtering)**: Sử dụng Hook chuột mức thấp (`WH_MOUSE_LL`) kết hợp Win32 API `WindowFromPoint` để nhận diện click đúp. Ứng dụng chỉ mở khóa tương tác khi bạn click đúp trên vùng trống Desktop, bỏ qua hoàn toàn nếu bạn click đúp trên các phần mềm khác (Chrome, Notepad, game...).
5. **Ghi nhớ và Tiếp tục phát (Video Resume)**: Tự động ghi nhớ video đang phát gần nhất và mốc thời gian (timestamp) chạy. Khi khởi động lại ứng dụng, video sẽ tự động phát tiếp tục từ giây cuối cùng bạn xem.
6. **Chế độ Portable di động**: Lưu toàn bộ cấu hình presets (`presets.json`), bộ nhớ đệm WebView2 (`WebView2_Cache`) và ảnh nền tại thư mục gốc của ứng dụng. Dễ dàng sao chép đi nơi khác mà không mất dữ liệu.
7. **Điều chỉnh Tỷ lệ giao diện (Zoom Factor)**: Cho phép phóng to giao diện trang web YouTube (từ 100% đến 250%) từ menu khay hệ thống để dễ dàng điều khiển khi ở chế độ tương tác.

---

## 🛠️ Cấu trúc dự án

* `DesktopVideoWallpaper.csproj`: Cấu hình dự án .NET 10.0 và thư viện WebView2.
* `MainWindow.xaml` & `MainWindow.xaml.cs`: Giao diện chính và toàn bộ logic điều khiển Win32, quản lý sự kiện chuột, bàn phím và khay hệ thống.
* `CalibrationWindow.xaml` & `CalibrationWindow.xaml.cs`: Cửa sổ kéo thả 4 góc để căn chỉnh video (hỗ trợ cả 2D và 3D).
* `WallpaperPreset.cs`: Cấu trúc dữ liệu cấu hình Preset (tọa độ, âm thanh, zoom, góc 3D, video hiện tại, vị trí phát).
* `presets.json`: File lưu trữ cấu hình các Preset.

---

## 🚀 Cách chạy và Đóng gói

### 1. Chạy trong Visual Studio / SDK
* Yêu cầu: **.NET 10.0 SDK** và Visual Studio 2022 (v17.12 trở lên).
* Mở file `DesktopVideoWallpaper.csproj` và nhấn **F5**.

### 2. Sử dụng Bộ cài đặt (Setup Installer)
* Dự án hỗ trợ đóng gói bằng **Inno Setup**.
* File cài đặt đầu ra sau khi biên dịch nằm tại: `installer_output/DesktopVideoWallpaper_Setup.exe`.
* Hỗ trợ tùy chọn **Khởi động cùng Windows (Startup)** và tạo biểu tượng ngoài màn hình nền.
