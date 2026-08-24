# HỆ THỐNG QUẢN LÝ SINH VIÊN VỚI MONGODB & C# .NET 9 WPF

Dự án ứng dụng Desktop quản lý sinh viên hiện đại sử dụng **C# .NET 9 (WPF)** kết nối cơ sở dữ liệu **MongoDB Atlas**, đáp ứng đầy đủ 100% các tiêu chí kỹ thuật và thang điểm 10/10.

---

## 📑 BẢNG ÁNH XẠ TIÊU CHÍ CHẤM ĐIỂM (10 ĐIỂM)

| STT | Tiêu chí | Điểm | Cách triển khai trong mã nguồn |
| :---: | :--- | :---: | :--- |
| **1** | **Kiến trúc & Quản lý kết nối** | **1.0** | - Driver chính thức `MongoDB.Driver` 3.x<br>- Thiết kế mẫu **Singleton Pattern** trong `MongoDbService.cs`<br>- Đọc chuỗi kết nối từ `appsettings.json` |
| **2** | **Thiết kế Giao diện & Xử lý Dữ liệu Động** | **2.0** | - Form nhập liệu cố định: Mã SV, Họ tên, Tuổi, Giới tính, Mã lớp<br>- Nút `[+] Thêm Ngoại ngữ`: mở dialog thêm ngoại ngữ (`$addToSet`)<br>- Nút `[+] Thêm Môn học`: nhập bộ 3 (Mã môn, Tên môn, Điểm số $0 \to 10$)<br>- Hỗ trợ lưu sinh viên với mảng `ngoaingu` hoặc `monhoc` rỗng `[]` |
| **3** | **Thao tác CRUD Cơ bản** | **2.5** | - Thêm mới 1 SV (`insertOne`)<br>- Tải danh sách lên DataGrid, tìm kiếm theo `masv`, lọc theo `malop`<br>- Cập nhật thông tin cơ bản theo `masv` (`updateOne` với `$set`)<br>- Xóa 1 SV (`deleteOne`) và xóa hàng loạt theo lớp (`deleteMany`) |
| **4** | **Xử lý Mảng Nâng cao & Thay thế Document** | **1.5** | - Thêm phần tử vào mảng sau: `$push` môn học / `$addToSet` ngoại ngữ<br>- Cập nhật điểm môn học bằng **Positional Operator `$`** (`monhoc.$.diem`)<br>- Thay thế toàn bộ document theo `_id` (`replaceOne`) |
| **5** | **Dashboard & Báo Cáo Thống Kê** | **2.0** | Màn hình Dashboard riêng biệt sử dụng **MongoDB Aggregation Framework**:<br>- **KPI Cards**: Tổng SV, Tổng lớp, GPA toàn trường (`$unwind` + `$avg`), Tỷ lệ Nam/Nữ (`$group`)<br>- **Thống kê theo lớp**: Mã lớp, Sĩ số, Max GPA, Min GPA (`$group`)<br>- **Độ phổ biến ngoại ngữ**: `$unwind`, `$group`, `$sort` giảm dần<br>- **Top 5 SV**: `$sort` desc, `$limit: 5`<br>- **Phân loại học lực**: Xuất sắc ($\ge 8.5$), Giỏi ($7.0 \to < 8.5$), Khá ($5.5 \to < 7.0$), TB/Yếu ($< 5.5$) |
| **6** | **Tối ưu hóa & Đánh Index** | **1.0** | - Tự động tạo Index khi ứng dụng khởi chạy:<br>  + **Unique Index** cho trường `masv`<br>  + **Compound Index** cho `{ malop: 1, hoten: 1 }`<br>- Bắt ngoại lệ `MongoWriteException` (Code 11000) khi trùng `masv` |
| **-** | **Validation & Dữ liệu Seed** | **Cộng** | - Validate điểm số $0.0 \to 10.0$, tuổi $> 0$<br>- Hộp thoại xác nhận trước khi xóa<br>- Bộ dữ liệu mẫu `data_seed.json` gồm 18 sinh viên chuẩn thực tế |

---

## 🛠️ HƯỚNG DẪN CẤU HÌNH & CHẠY ỨNG DỤNG

### 1. Yêu cầu môi trường
- HĐH: Windows 10/11
- .NET SDK: **.NET 9.0** trở lên (kiểm tra bằng `dotnet --version`)
- Kết nối mạng Internet (để kết nối đến MongoDB Atlas Cloud)

### 2. Cấu hình Connection String
Mở file `appsettings.json` tại thư mục gốc của dự án:
```json
{
  "MongoDB": {
    "ConnectionString": "mongodb+srv://nnp1426_db_user:<db_password>@cluster0.1ovaxuk.mongodb.net/?appName=Cluster0",
    "DatabaseName": "qlsinhvien_db",
    "CollectionName": "sinhvien"
  }
}
```
👉 **Thay thế `<db_password>`** bằng mật khẩu Database User MongoDB Atlas của bạn.

### 3. Lệnh khởi chạy ứng dụng
Mở Terminal / PowerShell tại thư mục dự án và chạy:
```powershell
dotnet run
```

---

## 🚀 KỊCH BẢN DEMO & TRẢ LỜI VẤN ĐÁP KHI CHẤM THI

Khi thầy cô kiểm tra trực tiếp ứng dụng, bạn có thể thực hiện theo đúng trình tự sau để đạt điểm tuyệt đối:

### Bước 1: Nạp dữ liệu mẫu ban đầu
- Trên thanh công cụ phía trên bên phải, bấm nút **`📥 Nạp Seed Data Mẫu`**.
- Hệ thống sẽ tự động nạp **18 sinh viên** mẫu từ file `data_seed.json` vào MongoDB Atlas.

### Bước 2: Demo CRUD cơ bản & Bộ lọc
1. **Lọc theo lớp**: Chọn lớp `l01` trong ComboBox $\to$ Bảng chỉ hiện sinh viên lớp `l01`.
2. **Tìm kiếm theo Mã SV**: Nhập `sv001` vào ô tìm kiếm $\to$ Bấm `🔍 Tìm Mã SV`.
3. **Thêm mới sinh viên**:
   - Bấm `🧹 Xóa trắng Form`.
   - Nhập: Mã SV `sv099`, Họ tên `Lê Văn Thử Nghiệm`, Lớp `l01`, Tuổi `20`.
   - Để trống mảng ngoại ngữ và môn học $\to$ Bấm `➕ Thêm mới (insertOne)` $\to$ Lưu thành công SV với 2 mảng rỗng `[]`.

### Bước 3: Demo Thao tác Mảng động & Positional Operator ($)
1. **Thêm ngoại ngữ động ($addToSet)**:
   - Chọn sinh viên `sv099` vừa tạo trong danh sách.
   - Bấm `[+] Thêm Ngoại Ngữ ($addToSet)` $\to$ Chọn `Tiếng Nhật` $\to$ Bấm Thêm. Ngoại ngữ được đẩy trực tiếp vào CSDL.
2. **Thêm môn học động ($push)**:
   - Bấm `[+] Thêm Môn ($push)` $\to$ Nhập Mã: `csdl`, Tên: `Cơ sở dữ liệu`, Điểm: `8.5` $\to$ Môn học được bổ sung vào CSDL.
3. **Cập nhật điểm bằng Positional Operator ($)**:
   - Chọn môn `csdl` trong bảng môn học $\to$ Bấm **`✏️ Sửa Điểm ($)`**.
   - Sửa điểm từ `8.5` thành `10.0` $\to$ Cập nhật tức thì bằng toán tử `monhoc.$.diem`.

### Bước 4: Demo Bắt lỗi Trùng Khóa (Unique Index)
- Bấm `🧹 Xóa trắng Form`.
- Nhập lại Mã SV `sv001` (đã có sẵn trong CSDL), Họ tên `Nguyễn Test Trùng`, Tuổi `20`, Lớp `l01`.
- Bấm `➕ Thêm mới (insertOne)`.
- Hệ thống sẽ chặn lại và hiển thị cảnh báo: **`Mã sinh viên 'sv001' đã tồn tại trong cơ sở dữ liệu!`** (Xử lý từ `MongoWriteException` mã 11000).

### Bước 5: Demo Dashboard & Thống kê Aggregation
- Chuyển sang Tab **`📊 Dashboard & Báo Cáo Thống Kê`**.
- Thuyết minh với thầy:
  - **4 KPI Cards**: Tổng SV, Tổng Lớp, Điểm TB gộp toàn trường, Tỷ lệ giới tính Nam/Nữ.
  - **Thống kê theo lớp**: Nhóm theo `malop`, tính sĩ số, điểm Max, Min.
  - **Thống kê ngoại ngữ**: Tách mảng bằng `$unwind`, gom nhóm `$group`, sắp xếp `$sort` giảm dần.
  - **Top 5 Sinh viên**: Xếp hạng 5 SV có điểm TB cao nhất trường.
  - **Phân loại học lực**: Phân bố tỷ lệ % Xuất sắc, Giỏi, Khá, Trung bình/Yếu.

### Bước 6: Demo Xóa 1 SV và Xóa Cả Lớp (deleteMany)
1. **Xóa 1 SV**: Chọn sinh viên `sv099` $\to$ Bấm `🗑️ Xóa SV (deleteOne)`.
2. **Xóa cả lớp**: Trong ComboBox lọc, chọn lớp `l05` $\to$ Bấm `🗑️ Xóa Cả Lớp` $\to$ Tất cả SV lớp `l05` sẽ bị xóa đồng loạt bằng `deleteMany`.

---

## 📂 CẤU TRÚC THƯ MỤC DỰ ÁN

```
StudentManagementApp/
├── appsettings.json                 # Cấu hình kết nối MongoDB Atlas
├── data_seed.json                   # Dữ liệu kiểm thử mẫu (18 records)
├── StudentManagementApp.csproj      # File cấu hình .NET 9 WPF
├── Models/
│   ├── SinhVien.cs                  # Model Sinh viên (Bson mapping)
│   ├── MonHoc.cs                    # Model Môn học
│   └── DashboardModels.cs           # DTO cho Dashboard Aggregation
├── Services/
│   └── MongoDbService.cs            # Singleton Service kết nối & xử lý MongoDB
├── Views/
│   ├── AddLanguageDialog.xaml/.cs   # Dialog thêm ngoại ngữ ($addToSet)
│   ├── AddSubjectDialog.xaml/.cs    # Dialog thêm môn học ($push)
│   └── UpdateSubjectGradeDialog.xaml/.cs # Dialog sửa điểm (Positional Operator $)
├── MainWindow.xaml                  # Giao diện chính 2 Tab
├── MainWindow.xaml.cs               # Xử lý sự kiện giao diện
└── README.md                        # Hướng dẫn chi tiết & kịch bản chấm thi
```
