# ĐỒ ÁN: XÂY DỰNG ỨNG DỤNG QUẢN LÝ SINH VIÊN VỚI MONGODB

Ứng dụng Desktop quản lý sinh viên được xây dựng bằng ngôn ngữ **C# (.NET 9 WPF)** kết hợp với hệ quản trị cơ sở dữ liệu **MongoDB**.

---

## 1. Giới thiệu tổng quan

Hệ thống cung cấp giải pháp quản lý thông tin sinh viên toàn diện, tận dụng các ưu điểm của mô hình dữ liệu dạng tài liệu (Document-oriented NoSQL) của MongoDB. Ứng dụng hỗ trợ lưu trữ cấu trúc phân cấp phức tạp (mảng nhúng môn học, danh sách ngoại ngữ), thao tác dữ liệu nâng cao với các toán tử mảng, và trực quan hóa số liệu qua màn hình Dashboard sử dụng Aggregation Pipeline.

---

## 2. Công nghệ phát triển

- **Ngôn ngữ & Nền tảng**: C# (.NET 9.0) - Windows Presentation Foundation (WPF)
- **Hệ quản trị CSDL**: MongoDB Atlas (MongoDB 5.0+)
- **Thư viện Driver**: `MongoDB.Driver` (v3.11.0)
- **Kiến trúc kết nối**: Singleton Pattern cho `MongoClient` và `IMongoDatabase`
- **Quản lý cấu hình**: `appsettings.json`

---

## 3. Thiết kế CSDL & Cấu trúc Document

Hệ thống sử dụng database `qlsinhvien_db` và collection `sinhvien`. Mỗi sinh viên được lưu trữ dưới dạng một Document với cấu trúc mảng nhúng:

```json
{
  "_id": ObjectId("64f1a2b3c4d5e6f7a8b9c0d1"),
  "masv": "sv001",
  "hoten": "Nguyễn Văn An",
  "tuoi": 20,
  "phai": "Nam",
  "malop": "l01",
  "ngoaingu": ["Tiếng Anh", "Tiếng Nhật"],
  "monhoc": [
    {
      "mamon": "csdl",
      "tenmon": "Cơ sở dữ liệu",
      "diem": 8.5
    },
    {
      "mamon": "laptrinh",
      "tenmon": "Lập trình Cơ bản",
      "diem": 7.0
    }
  ]
}
```

---

## 4. Các chức năng chính

### 4.1. Quản lý Sinh viên (CRUD Cơ bản & Nâng cao)
- **Thêm mới (`insertOne`)**: Tạo mới sinh viên với đầy đủ thông tin hoặc khởi tạo với mảng rỗng `[]` để bổ sung sau.
- **Tìm kiếm & Lọc**: Tìm kiếm sinh viên theo Mã SV (`masv`) và lọc danh sách sinh viên theo Lớp (`malop`).
- **Cập nhật thông tin cơ bản (`updateOne` với `$set`)**: Cập nhật họ tên, tuổi, phái, mã lớp.
- **Thay thế Document (`replaceOne`)**: Thay thế toàn bộ nội dung document dựa theo `_id`.
- **Xóa dữ liệu**:
  - Xóa 1 sinh viên được chọn (`deleteOne`).
  - Xóa toàn bộ sinh viên thuộc một lớp cụ thể (`deleteMany`).

### 4.2. Thao tác Mảng Động & Positional Operator
- **Thêm phần tử vào mảng**: Bổ sung ngoại ngữ mới (`$addToSet` / `$push`) hoặc môn học mới (`$push`) cho sinh viên đã có trong CSDL.
- **Cập nhật phần tử trong mảng**: Sửa điểm số của một môn học cụ thể dựa vào `masv` và `mamon` bằng toán tử **Positional Operator (`$`)** mà không cần thay thế cả document.

### 4.3. Dashboard & Báo cáo Thống kê (Aggregation Framework)
- **KPI Cards**:
  - Tổng số sinh viên hiện có
  - Tổng số lớp học khác nhau
  - Điểm trung bình toàn trường (tính gộp từ tất cả môn học của toàn bộ sinh viên bằng `$unwind` và `$avg`)
  - Tỷ lệ phần trăm giới tính Nam / Nữ (`$group`)
- **Thống kê theo Lớp**: Mã lớp, Sĩ số, Điểm TB cao nhất, Điểm TB thấp nhất (`$group`).
- **Thống kê Ngoại ngữ**: Đếm số lượng sinh viên theo từng ngoại ngữ (`$unwind` $\to$ `$group` $\to$ `$sort` desc).
- **Bảng xếp hạng Top 5**: Top 5 sinh viên có điểm TB cao nhất trường (`$sort` $\to$ `$limit: 5`).
- **Phân loại Học lực**: Thống kê số lượng và tỷ lệ % sinh viên theo các mức: Xuất sắc ($\ge 8.5$), Giỏi ($7.0 \to < 8.5$), Khá ($5.5 \to < 7.0$), Trung bình/Yếu ($< 5.5$).

### 4.4. Tối ưu hóa & Đánh Index
- Khởi tạo Index tự động khi ứng dụng khởi chạy:
  - **Unique Index**: Áp dụng trên trường `masv` nhằm ngăn chặn trùng lặp mã sinh viên.
  - **Compound Index**: Áp dụng trên cặp trường `{ malop: 1, hoten: 1 }` giúp tối ưu hóa truy vấn lọc và sắp xếp theo lớp.
- Xử lý ngoại lệ `MongoWriteException` khi vi phạm Unique Index để thông báo lỗi rõ ràng.
- Ràng buộc dữ liệu: Điểm số trong thang điểm $0.0 \to 10.0$, tuổi là số nguyên dương $> 0$.

---

## 5. Hướng dẫn Cài đặt & Chạy Ứng dụng

### 5.1. Yêu cầu hệ thống
- Hệ điều hành: Windows 10/11
- .NET SDK: Phiên bản **.NET 9.0** trở lên

### 5.2. Cấu hình chuỗi kết nối
Mở tệp `appsettings.json` tại thư mục gốc của dự án và điền chuỗi kết nối MongoDB Atlas:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb+srv://<username>:<password>@cluster0.1ovaxuk.mongodb.net/?appName=Cluster0",
    "DatabaseName": "qlsinhvien_db",
    "CollectionName": "sinhvien"
  }
}
```

### 5.3. Khởi chạy ứng dụng
Mở terminal tại thư mục dự án và thực hiện lệnh:

```powershell
dotnet run
```

*(Hoặc mở file `StudentManagementApp.csproj` bằng Visual Studio 2022 và nhấn `F5`).*

---

## 6. Cấu trúc Thư mục

```
StudentManagementApp/
├── appsettings.json                 # Cấu hình kết nối CSDL
├── data_seed.json                   # Dữ liệu mẫu (200 sinh viên)
├── StudentManagementApp.csproj      # File cấu hình dự án .NET
├── Models/
│   ├── SinhVien.cs                  # Model ánh xạ BSON sinh viên
│   ├── MonHoc.cs                    # Model môn học
│   └── DashboardModels.cs           # Các DTO phục vụ Aggregation
├── Services/
│   └── MongoDbService.cs            # Xử lý kết nối Singleton & thao tác MongoDB
├── Views/
│   ├── AddLanguageDialog.xaml/.cs   # Dialog thêm ngoại ngữ ($addToSet)
│   ├── AddSubjectDialog.xaml/.cs    # Dialog thêm môn học ($push)
│   └── UpdateSubjectGradeDialog.xaml/.cs # Dialog sửa điểm (Positional $)
├── MainWindow.xaml                  # Giao diện chính (Quản lý SV & Dashboard)
├── MainWindow.xaml.cs               # Xử lý sự kiện giao diện
└── README.md                        # Tài liệu hướng dẫn đồ án
```
