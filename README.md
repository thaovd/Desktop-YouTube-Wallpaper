# Desktop Video Wallpaper (YouTube Wallpaper)

Ứng dụng WPF (.NET 8.0) cho phép bạn nhúng một video hoặc luồng phát trực tiếp (live stream) YouTube làm hình nền động của máy tính. Ứng dụng này sử dụng các kỹ thuật lập trình hệ thống nâng cao thông qua Win32 API (P/Invoke) để đưa cửa sổ ứng dụng nằm dưới các biểu tượng màn hình chính (Desktop Icons) nhưng đè lên hình nền tĩnh cũ của Windows, đồng thời cho phép click xuyên qua video để bạn có thể làm việc và click chọn icon bình thường.

---

## 🛠️ Cấu trúc dự án
1. `DesktopVideoWallpaper.csproj`: Cấu hình dự án .NET 8.0 và tham chiếu thư viện `Microsoft.Web.WebView2`.
2. `App.xaml` & `App.xaml.cs`: Điểm khởi chạy của ứng dụng WPF.
3. `MainWindow.xaml`: Giao diện ứng dụng gồm màn hình chờ (giao diện gradient hiện đại) và điều khiển nhúng `WebView2`.
4. `MainWindow.xaml.cs`: Code xử lý logic chính, bao gồm khai báo P/Invoke Win32 API, tìm kiếm cửa sổ `WorkerW`, Reparenting (nhúng cửa sổ) và thiết lập cờ Click-Through (`WS_EX_TRANSPARENT`).

---

## 🚀 Cách xây dựng và chạy ứng dụng

Do máy tính của bạn hiện tại chưa cấu hình biến môi trường `dotnet` trong terminal, bạn có thể thực hiện chạy ứng dụng theo một trong các cách sau:

### Cách 1: Sử dụng Visual Studio (Khuyên dùng)
1. Mở phần mềm **Visual Studio 2022** (hoặc mới hơn).
2. Chọn **Open a project or solution**.
3. Tìm và chọn file `DesktopVideoWallpaper.csproj` trong thư mục này.
4. Nhấn **F5** để tự động khôi phục các gói NuGet (`Microsoft.Web.WebView2`) và chạy ứng dụng.

### Cách 2: Sử dụng VS Code hoặc JetBrains Rider
1. Mở thư mục này bằng VS Code hoặc Rider.
2. Đảm bảo bạn đã cài đặt **.NET 8.0 SDK** trên máy.
3. Chạy lệnh build hoặc debug bằng các extension tích hợp sẵn.

---

## ⚙️ Các hàm Win32 API chính được sử dụng
* **`SendMessageTimeout`**: Gửi thông điệp `0x052C` đến cửa sổ điều khiển desktop `Progman`. Đây là cơ chế ẩn của Windows để tách lớp hình nền cũ và tạo ra một lớp chứa mới có tên lớp là `WorkerW`.
* **`EnumWindows`**: Lặp qua danh sách tất cả các cửa sổ đang mở trong hệ thống để quét và tìm cửa sổ `WorkerW` nằm ngay phía sau các biểu tượng desktop (`SHELLDLL_DefView`).
* **`SetParent`**: Chuyển cửa sổ ứng dụng WPF thành con của cửa sổ `WorkerW` đó.
* **`SetWindowLongPtr`**: Thay đổi thuộc tính kiểu dáng của cửa sổ:
  - Chuyển `GWL_STYLE` thành `WS_CHILD` để ngăn cửa sổ bị ẩn đi khi nhấn tổ hợp phím `Win + D` (Show Desktop).
  - Thêm cờ `WS_EX_LAYERED` và `WS_EX_TRANSPARENT` vào `GWL_EXSTYLE` để chuột click xuyên qua ứng dụng (Click-through).
* **`SetWindowPos`**: Đảm bảo cửa sổ được hiển thị ở góc (0,0) và kéo rộng full kích thước màn hình theo pixel chuẩn mà không bị lệch do cài đặt DPI.

---

## 🎵 Tùy chỉnh Video của riêng bạn
Mở file `MainWindow.xaml.cs`, tìm hàm `InitializeWebViewAsync` và thay đổi giá trị của biến `videoId`:
```csharp
// Thay đổi ID này bằng ID video YouTube bất kỳ hoặc link livestream
string videoId = "jfKfPfyJRdk"; 
```

**⚠️ Lưu ý:** Để tính năng tự động phát (Autoplay) hoạt động trơn tru trong các trình duyệt nhân Chromium (như WebView2), video buộc phải được tắt âm (`mute=1`). Đây là chính sách bảo mật trình duyệt mặc định của hệ thống.
