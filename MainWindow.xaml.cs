using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StudentManagementApp.Models;
using StudentManagementApp.Services;
using StudentManagementApp.Views;

namespace StudentManagementApp
{
    /// <summary>
    /// Lớp xử lý giao diện chính MainWindow: Bắt sự kiện người dùng và điều phối dữ liệu
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MongoDbService _mongoService;
        private List<SinhVien> _allStudents = new();
        private SinhVien? _selectedStudent;

        // ObservableCollection giúp tự động cập nhật danh sách Ngoại ngữ & Môn học lên UI khi có thay đổi
        private readonly ObservableCollection<string> _currentNgoaiNgu = new();
        private readonly ObservableCollection<MonHoc> _currentMonHoc = new();

        /// <summary>
        /// Constructor khởi tạo MainWindow: Gán Singleton MongoDbService và liên kết DataBinding
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _mongoService = MongoDbService.Instance;
            LstNgoaiNgu.ItemsSource = _currentNgoaiNgu;
            DgMonHoc.ItemsSource = _currentMonHoc;
        }

        /// <summary>
        /// Sự kiện chạy khi cửa sổ vừa tải xong (Window_Loaded):
        /// 1. Tự động khởi tạo Unique Index và Compound Index dưới MongoDB Atlas
        /// 2. Nạp toàn bộ dữ liệu sinh viên và Dashboard lên màn hình
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtStatusBar.Text = "Đang kết nối MongoDB Atlas và khởi tạo Indexes...";
            try
            {
                // Khởi tạo Unique Index (masv) và Compound Index ({ malop: 1, hoten: 1 })
                await _mongoService.InitializeIndexesAsync();
                TxtStatusBadge.Text = "MongoDB Connected";
                TxtStatusBar.Text = "Đã kết nối MongoDB Atlas & thiết lập Indexes thành công.";

                // Tải danh sách sinh viên và tính toán Dashboard
                await LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                TxtStatusBadge.Text = "Kết nối Thất Bại";
                TxtStatusBar.Text = $"Lỗi kết nối MongoDB: {ex.Message}";
                MessageBox.Show(
                    $"Không thể kết nối đến MongoDB Atlas.\n\nChi tiết: {ex.Message}\n\n" +
                    "Gợi ý: Hãy kiểm tra chuỗi kết nối và mật khẩu trong file 'appsettings.json' hoặc kiểm tra kết nối mạng Internet.",
                    "Lỗi kết nối MongoDB",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #region Tải dữ liệu & Cập nhật UI

        /// <summary>
        /// Hàm tải toàn bộ danh sách sinh viên từ MongoDB Atlas, nạp danh sách lớp vào bộ lọc và cập nhật Dashboard
        /// </summary>
        private async Task LoadAllDataAsync()
        {
            try
            {
                TxtStatusBar.Text = "Đang tải danh sách sinh viên...";
                _allStudents = await _mongoService.GetAllAsync();

                // Cập nhật danh sách lớp vào ComboBox bộ lọc
                var classes = await _mongoService.GetAllClassesAsync();
                var currentFilter = CboFilterClass.SelectedItem as string;

                CboFilterClass.Items.Clear();
                CboFilterClass.Items.Add("Tất cả các lớp");
                foreach (var cls in classes)
                {
                    CboFilterClass.Items.Add(cls);
                }

                if (!string.IsNullOrEmpty(currentFilter) && CboFilterClass.Items.Contains(currentFilter))
                {
                    CboFilterClass.SelectedItem = currentFilter;
                }
                else
                {
                    CboFilterClass.SelectedIndex = 0;
                }

                // Áp dụng bộ lọc và sắp xếp lên DataGrid
                ApplyFilter();

                // Tải dữ liệu thống kê cho màn hình Dashboard
                await LoadDashboardDataAsync();
                TxtStatusBar.Text = $"Đã tải thành công {_allStudents.Count} sinh viên.";
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = $"Lỗi tải dữ liệu: {ex.Message}";
            }
        }

        /// <summary>
        /// Hàm áp dụng bộ lọc (theo Lớp, tìm kiếm Mã SV) và sắp xếp dữ liệu (theo Mã SV, Tên A-Z, Điểm TB, Tuổi)
        /// </summary>
        private void ApplyFilter()
        {
            if (_allStudents == null || DgSinhVien == null || TxtTotalGridCount == null)
                return;

            string selectedClass = CboFilterClass?.SelectedItem as string ?? "Tất cả các lớp";
            string searchMaSv = TxtSearchMaSv?.Text.Trim().ToLower() ?? string.Empty;
            string sortBy = (CboSortBy?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Mặc định (Lớp & Họ tên)";

            var filtered = _allStudents.AsEnumerable();

            // 1. Lọc theo lớp
            if (selectedClass != "Tất cả các lớp" && !string.IsNullOrEmpty(selectedClass))
            {
                filtered = filtered.Where(s => s.MaLop.Equals(selectedClass, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Tìm kiếm theo mã SV
            if (!string.IsNullOrWhiteSpace(searchMaSv))
            {
                filtered = filtered.Where(s => s.MaSv.ToLower().Contains(searchMaSv));
            }

            // 3. Sắp xếp theo tùy chọn của người dùng (hỗ trợ sắp xếp Tiếng Việt chuẩn)
            var viCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
            var viComparer = StringComparer.Create(viCulture, true);

            filtered = sortBy switch
            {
                "Mã SV (Tăng dần A ➔ Z)" => filtered.OrderBy(s => s.MaSv, viComparer),
                "Mã SV (Giảm dần Z ➔ A)" => filtered.OrderByDescending(s => s.MaSv, viComparer),
                "Họ và Tên (A ➔ Z)" => filtered.OrderBy(s => GetTenGoi(s.HoTen), viComparer).ThenBy(s => s.HoTen, viComparer),
                "Họ và Tên (Z ➔ A)" => filtered.OrderByDescending(s => GetTenGoi(s.HoTen), viComparer).ThenByDescending(s => s.HoTen, viComparer),
                "Điểm Trung Bình (Cao ➔ Thấp ⬇)" => filtered.OrderByDescending(s => s.DiemTrungBinh),
                "Điểm Trung Bình (Thấp ➔ Cao ⬆)" => filtered.OrderBy(s => s.DiemTrungBinh),
                "Tuổi (Tăng dần ⬆)" => filtered.OrderBy(s => s.Tuoi),
                "Tuổi (Giảm dần ⬇)" => filtered.OrderByDescending(s => s.Tuoi),
                _ => filtered.OrderBy(s => s.MaLop, viComparer).ThenBy(s => GetTenGoi(s.HoTen), viComparer).ThenBy(s => s.HoTen, viComparer)
            };

            var list = filtered.ToList();
            DgSinhVien.ItemsSource = list;
            TxtTotalGridCount.Text = $"{list.Count} sinh viên";
        }

        /// <summary>
        /// Hàm phụ trợ tách lấy tên gọi cuối cùng của người Việt (Ví dụ: "Nguyễn Văn An" -> "An") để sắp xếp A-Z chuẩn
        /// </summary>
        private static string GetTenGoi(string hoTen)
        {
            if (string.IsNullOrWhiteSpace(hoTen)) return string.Empty;
            var parts = hoTen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[^1] : hoTen;
        }

        /// <summary>
        /// Hàm gọi Aggregation Pipeline từ MongoDB để tính toán và cập nhật các chỉ số lên màn hình Dashboard
        /// </summary>
        private async Task LoadDashboardDataAsync()
        {
            try
            {
                var summary = await _mongoService.GetDashboardDataAsync();

                // 1. KPI Cards
                TxtKpiTotalStudents.Text = summary.Kpi.TongSinhVien.ToString();
                TxtKpiTotalClasses.Text = summary.Kpi.TongSoLop.ToString();
                TxtKpiSchoolGpa.Text = summary.Kpi.DiemTbToanTruong.ToString("0.00");
                TxtKpiGenderRatio.Text = $"{summary.Kpi.TyLeNam}% / {summary.Kpi.TyLeNu}%";
                TxtKpiGenderDetail.Text = $"Nam: {summary.Kpi.SoLuongNam} | Nữ: {summary.Kpi.SoLuongNu}";

                // 2. Thống kê theo lớp
                DgClassStats.ItemsSource = summary.ThongKeLop;

                // 3. Thống kê ngoại ngữ
                DgLangStats.ItemsSource = summary.ThongKeNgoaiNgu;

                // 4. Top 5 sinh viên điểm cao nhất
                DgTop5Students.ItemsSource = summary.Top5SinhVien;

                // 5. Phân loại học lực toàn trường
                IcAcademicClass.ItemsSource = summary.PhanLoaiHocLuc;
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = $"Lỗi cập nhật Dashboard: {ex.Message}";
            }
        }

        #endregion

        #region Bộ lọc & Tìm kiếm

        /// <summary>
        /// Sự kiện khi người dùng chọn lớp khác trong ComboBox -> Áp dụng lại bộ lọc
        /// </summary>
        private void CboFilterClass_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        /// <summary>
        /// Sự kiện khi người dùng chọn kiểu sắp xếp khác trong ComboBox -> Sắp xếp lại danh sách
        /// </summary>
        private void CboSortBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        /// <summary>
        /// Sự kiện khi bấm nút "Tìm Mã SV" -> Tìm kiếm theo mã sinh viên
        /// </summary>
        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        /// <summary>
        /// Sự kiện khi bấm nút "Đặt lại bộ lọc" -> Xóa từ khóa tìm kiếm và đưa bộ lọc/sắp xếp về mặc định
        /// </summary>
        private void BtnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchMaSv.Text = string.Empty;
            CboFilterClass.SelectedIndex = 0;
            CboSortBy.SelectedIndex = 0;
            ApplyFilter();
            TxtStatusBar.Text = "Đã đặt lại toàn bộ bộ lọc và sắp xếp về mặc định.";
        }

        /// <summary>
        /// Sự kiện khi bấm nút "Xóa Lớp" -> Hiện xác nhận và gọi deleteMany xóa toàn bộ sinh viên trong lớp
        /// </summary>
        private async void BtnDeleteClass_Click(object sender, RoutedEventArgs e)
        {
            string selectedClass = CboFilterClass.SelectedItem as string ?? "";
            if (string.IsNullOrWhiteSpace(selectedClass) || selectedClass == "Tất cả các lớp")
            {
                MessageBox.Show("Vui lòng chọn một lớp học cụ thể trong danh sách lọc để thực hiện xóa toàn bộ lớp!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"BẠN CÓ CHẮC CHẮN MUỐN XÓA TOÀN BỘ SINH VIÊN THUỘC LỚP '{selectedClass}' KHÔNG?\n\nThao tác này sử dụng 'deleteMany' và không thể hoàn tác!",
                "Xác nhận xóa hàng loạt (deleteMany)",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    long deletedCount = await _mongoService.DeleteManyByMaLopAsync(selectedClass);
                    MessageBox.Show($"Đã xóa thành công {deletedCount} sinh viên thuộc lớp '{selectedClass}' (deleteMany).", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    await LoadAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xóa lớp: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Xử lý Chọn Sinh Viên & Form Binding

        /// <summary>
        /// Sự kiện khi người dùng click chọn 1 sinh viên trong DataGrid -> Đổ dữ liệu lên Form chi tiết bên phải
        /// </summary>
        private void DgSinhVien_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgSinhVien.SelectedItem is SinhVien sv)
            {
                _selectedStudent = sv;
                TxtFormMaSv.Text = sv.MaSv;
                TxtFormHoTen.Text = sv.HoTen;
                TxtFormTuoi.Text = sv.Tuoi.ToString();
                TxtFormMaLop.Text = sv.MaLop;

                if (string.Equals(sv.Phai, "Nữ", StringComparison.OrdinalIgnoreCase) || string.Equals(sv.Phai, "Nu", StringComparison.OrdinalIgnoreCase))
                {
                    RadNu.IsChecked = true;
                }
                else
                {
                    RadNam.IsChecked = true;
                }

                // Cập nhật danh sách Ngoại ngữ động lên ListBox
                _currentNgoaiNgu.Clear();
                if (sv.NgoaiNgu != null)
                {
                    foreach (var nn in sv.NgoaiNgu)
                    {
                        _currentNgoaiNgu.Add(nn);
                    }
                }

                // Cập nhật danh sách Môn học động lên DataGrid môn học
                _currentMonHoc.Clear();
                if (sv.MonHoc != null)
                {
                    foreach (var mh in sv.MonHoc)
                    {
                        _currentMonHoc.Add(new MonHoc
                        {
                            MaMon = mh.MaMon,
                            TenMon = mh.TenMon,
                            Diem = mh.Diem
                        });
                    }
                }

                TxtStatusBar.Text = $"Đang chọn sinh viên: {sv.HoTen} ({sv.MaSv})";
            }
        }

        /// <summary>
        /// Hàm làm sạch Form nhập liệu để chuẩn bị cho việc thêm mới sinh viên
        /// </summary>
        private void ClearForm()
        {
            _selectedStudent = null;
            DgSinhVien.SelectedItem = null;
            TxtFormMaSv.Text = string.Empty;
            TxtFormHoTen.Text = string.Empty;
            TxtFormTuoi.Text = "20";
            TxtFormMaLop.Text = string.Empty;
            RadNam.IsChecked = true;
            _currentNgoaiNgu.Clear();
            _currentMonHoc.Clear();
            TxtStatusBar.Text = "Đã làm mới form nhập liệu.";
        }

        /// <summary>
        /// Sự kiện khi bấm nút "Xóa trắng Form (Nhập mới)"
        /// </summary>
        private void BtnClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        #endregion

        #region Thao tác Mảng Động & Positional Operator ($)

        /// <summary>
        /// Sự kiện khi bấm nút "[+] Thêm Ngoại Ngữ": Mở Dialog nhập ngoại ngữ và gọi $addToSet đẩy vào CSDL
        /// </summary>
        private async void BtnAddLanguage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddLanguageDialog { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedLanguage))
            {
                string lang = dialog.SelectedLanguage;

                // Nếu đang chọn 1 SV đã lưu trong DB -> cập nhật trực tiếp qua $addToSet
                if (_selectedStudent != null && !string.IsNullOrWhiteSpace(_selectedStudent.MaSv))
                {
                    try
                    {
                        bool success = await _mongoService.AddNgoaiNguAsync(_selectedStudent.MaSv, lang);
                        if (success)
                        {
                            if (!_currentNgoaiNgu.Contains(lang))
                            {
                                _currentNgoaiNgu.Add(lang);
                            }
                            MessageBox.Show($"Đã bổ sung ngoại ngữ '{lang}' cho sinh viên {_selectedStudent.MaSv} thành công bằng toán tử $addToSet!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                            await LoadAllDataAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi thêm ngoại ngữ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Đang ở chế độ nhập form mới
                    if (!_currentNgoaiNgu.Contains(lang))
                    {
                        _currentNgoaiNgu.Add(lang);
                    }
                }
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "Xóa Ngoại Ngữ": Xóa ngoại ngữ đang chọn khỏi danh sách
        /// </summary>
        private void BtnRemoveLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (LstNgoaiNgu.SelectedItem is string lang)
            {
                _currentNgoaiNgu.Remove(lang);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn 1 ngoại ngữ trong danh sách để xóa!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "[+] Thêm Môn": Mở Dialog nhập Mã môn, Tên môn, Điểm số và gọi $push đẩy vào CSDL
        /// </summary>
        private async void BtnAddSubject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSubjectDialog(_currentMonHoc.Select(m => m.MaMon)) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.CreatedSubject != null)
            {
                var newSubject = dialog.CreatedSubject;

                // Kiểm tra xem môn học đã có trong danh sách chưa
                if (_currentMonHoc.Any(m => m.MaMon.Equals(newSubject.MaMon, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Mã môn '{newSubject.MaMon}' đã có trong danh sách môn học của sinh viên này!", "Trùng mã môn", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Nếu đang chọn 1 SV đã lưu trong DB -> đẩy trực tiếp vào CSDL qua $push
                if (_selectedStudent != null && !string.IsNullOrWhiteSpace(_selectedStudent.MaSv))
                {
                    try
                    {
                        bool success = await _mongoService.AddMonHocAsync(_selectedStudent.MaSv, newSubject);
                        if (success)
                        {
                            _currentMonHoc.Add(newSubject);
                            MessageBox.Show($"Đã thêm môn '{newSubject.TenMon}' (Điểm: {newSubject.Diem}) cho sinh viên {_selectedStudent.MaSv} bằng toán tử $push!", "Thành công ($push)", MessageBoxButton.OK, MessageBoxImage.Information);
                            await LoadAllDataAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi thêm môn học: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Đang ở chế độ nhập form mới
                    _currentMonHoc.Add(newSubject);
                }
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "✏️ Sửa Điểm ($)": Mở Dialog nhập điểm mới và gọi Positional Operator ($) cập nhật CSDL
        /// </summary>
        private async void BtnUpdateGrade_Click(object sender, RoutedEventArgs e)
        {
            if (DgMonHoc.SelectedItem is not MonHoc selectedSubject)
            {
                MessageBox.Show("Vui lòng chọn một môn học trong bảng môn học để sửa điểm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_selectedStudent == null || string.IsNullOrWhiteSpace(_selectedStudent.MaSv))
            {
                MessageBox.Show("Vui lòng chọn một sinh viên đã lưu trong CSDL để thực hiện cập nhật điểm bằng toán tử Positional ($)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new UpdateSubjectGradeDialog(_selectedStudent, selectedSubject) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                double newGrade = dialog.NewGrade;
                try
                {
                    // Sử dụng Positional Operator ($) trực tiếp trong MongoDB Driver
                    bool success = await _mongoService.UpdateDiemMonHocAsync(_selectedStudent.MaSv, selectedSubject.MaMon, newGrade);
                    if (success)
                    {
                        selectedSubject.Diem = newGrade;
                        DgMonHoc.Items.Refresh();
                        MessageBox.Show($"Đã cập nhật điểm môn '{selectedSubject.TenMon}' thành {newGrade} bằng toán tử Positional Operator ($) thành công!", "Thành công ($)", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadAllDataAsync();
                    }
                    else
                    {
                        MessageBox.Show("Không tìm thấy môn học hoặc điểm số không thay đổi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật điểm: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "Xóa Môn": Xóa môn học đang chọn khỏi danh sách
        /// </summary>
        private void BtnRemoveSubject_Click(object sender, RoutedEventArgs e)
        {
            if (DgMonHoc.SelectedItem is MonHoc subject)
            {
                _currentMonHoc.Remove(subject);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn 1 môn học trong bảng để xóa!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Thao tác CRUD (Create, Update, Replace, Delete)

        /// <summary>
        /// Hàm xác thực tính hợp lệ của dữ liệu đầu vào Form (Tuổi > 0, không để trống Mã SV, Tên, Lớp)
        /// </summary>
        private bool ValidateInput(out string masv, out string hoten, out int tuoi, out string phai, out string malop)
        {
            masv = TxtFormMaSv.Text.Trim();
            hoten = TxtFormHoTen.Text.Trim();
            malop = TxtFormMaLop.Text.Trim();
            phai = RadNam.IsChecked == true ? "Nam" : "Nữ";
            tuoi = 0;

            if (string.IsNullOrWhiteSpace(masv))
            {
                MessageBox.Show("Mã sinh viên không được để trống!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFormMaSv.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(hoten))
            {
                MessageBox.Show("Họ và tên sinh viên không được để trống!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFormHoTen.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(malop))
            {
                MessageBox.Show("Mã lớp không được để trống!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFormMaLop.Focus();
                return false;
            }

            if (!int.TryParse(TxtFormTuoi.Text.Trim(), out tuoi) || tuoi <= 0)
            {
                MessageBox.Show("Tuổi sinh viên phải là một số nguyên dương (> 0)!", "Lỗi xác thực", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFormTuoi.Focus();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sự kiện khi bấm nút "➕ Thêm mới (insertOne)": Kiểm tra hợp lệ và gửi lệnh insertOne xuống MongoDB Atlas
        /// </summary>
        private async void BtnInsertOne_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput(out string masv, out string hoten, out int tuoi, out string phai, out string malop))
                return;

            var newStudent = new SinhVien
            {
                MaSv = masv,
                HoTen = hoten,
                Tuoi = tuoi,
                Phai = phai,
                MaLop = malop,
                NgoaiNgu = _currentNgoaiNgu.ToList(),
                MonHoc = _currentMonHoc.ToList()
            };

            try
            {
                TxtStatusBar.Text = $"Đang thêm mới sinh viên {masv}...";
                await _mongoService.InsertOneAsync(newStudent);
                MessageBox.Show($"Thêm mới sinh viên '{hoten}' ({masv}) thành công (insertOne)!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                await LoadAllDataAsync();
            }
            catch (DuplicateStudentIdException ex)
            {
                // Bắt lỗi Unique Index nếu trùng mã sinh viên
                MessageBox.Show(ex.Message, "Lỗi Trùng Mã Sinh Viên (Unique Index Violation)", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtFormMaSv.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi thêm mới: {ex.Message}", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "💾 Cập nhật ($set)": Gọi updateOne với toán tử $set để cập nhật thông tin cơ bản
        /// </summary>
        private async void BtnUpdateBasic_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput(out string masv, out string hoten, out int tuoi, out string phai, out string malop))
                return;

            try
            {
                TxtStatusBar.Text = $"Đang cập nhật thông tin cơ bản sinh viên {masv}...";
                bool updated = await _mongoService.UpdateBasicInfoAsync(masv, hoten, tuoi, phai, malop);
                if (updated)
                {
                    MessageBox.Show($"Đã cập nhật thông tin sinh viên '{masv}' thành công (updateOne với $set)!", "Thành công ($set)", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadAllDataAsync();
                }
                else
                {
                    MessageBox.Show($"Không tìm thấy sinh viên có mã '{masv}' để cập nhật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi cập nhật: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "🔄 Thay thế (replaceOne)": Ghi đè thay thế toàn bộ Document cũ theo _id
        /// </summary>
        private async void BtnReplaceOne_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStudent == null || string.IsNullOrWhiteSpace(_selectedStudent.Id))
            {
                MessageBox.Show("Vui lòng chọn một sinh viên hiện có từ bảng danh sách để thực hiện thay thế toàn bộ document (replaceOne)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateInput(out string masv, out string hoten, out int tuoi, out string phai, out string malop))
                return;

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn thay thế toàn bộ document sinh viên ID: {_selectedStudent.Id} (replaceOne)?",
                "Xác nhận thay thế Document",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var replacement = new SinhVien
            {
                Id = _selectedStudent.Id,
                MaSv = masv,
                HoTen = hoten,
                Tuoi = tuoi,
                Phai = phai,
                MaLop = malop,
                NgoaiNgu = _currentNgoaiNgu.ToList(),
                MonHoc = _currentMonHoc.ToList()
            };

            try
            {
                TxtStatusBar.Text = $"Đang thay thế document {_selectedStudent.Id}...";
                bool replaced = await _mongoService.ReplaceOneAsync(_selectedStudent.Id, replacement);
                if (replaced)
                {
                    MessageBox.Show($"Đã thay thế toàn bộ nội dung document sinh viên '{masv}' thành công (replaceOne)!", "Thành công (replaceOne)", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadAllDataAsync();
                }
                else
                {
                    MessageBox.Show("Không tìm thấy document để thay thế.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (DuplicateStudentIdException ex)
            {
                MessageBox.Show(ex.Message, "Lỗi Trùng Mã Sinh Viên", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi thay thế document: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "🗑️ Xóa SV (deleteOne)": Hiện hộp thoại xác nhận và gọi deleteOne xóa 1 sinh viên
        /// </summary>
        private async void BtnDeleteOne_Click(object sender, RoutedEventArgs e)
        {
            string masv = TxtFormMaSv.Text.Trim();
            if (string.IsNullOrWhiteSpace(masv))
            {
                MessageBox.Show("Vui lòng nhập hoặc chọn mã sinh viên cần xóa!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa sinh viên có mã '{masv}' khỏi cơ sở dữ liệu (deleteOne)?",
                "Xác nhận xóa sinh viên",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    TxtStatusBar.Text = $"Đang xóa sinh viên {masv}...";
                    bool deleted = await _mongoService.DeleteOneAsync(masv);
                    if (deleted)
                    {
                        MessageBox.Show($"Đã xóa sinh viên '{masv}' thành công (deleteOne)!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                        ClearForm();
                        await LoadAllDataAsync();
                    }
                    else
                    {
                        MessageBox.Show($"Không tìm thấy sinh viên có mã '{masv}' để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xóa: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Seed Data & Refresh

        /// <summary>
        /// Sự kiện khi bấm nút "📥 Nạp Seed Data Mẫu": Đọc data_seed.json và nạp 40 sinh viên vào MongoDB Atlas
        /// </summary>
        private async void BtnSeedData_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Bạn có muốn nạp dữ liệu mẫu từ tệp 'data_seed.json' vào CSDL MongoDB không?\n\nLưu ý: Hành động này sẽ làm mới toàn bộ collection sinhvien!",
                "Xác nhận nạp dữ liệu mẫu",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    string seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_seed.json");
                    if (!File.Exists(seedPath))
                    {
                        seedPath = "data_seed.json";
                    }

                    int count = await _mongoService.SeedSampleDataAsync(seedPath);
                    MessageBox.Show($"Đã nạp thành công {count} sinh viên mẫu vào CSDL!", "Nạp Seed Data thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    await LoadAllDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi nạp dữ liệu mẫu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "🔄 Tải lại" trên thanh Header: Tải lại toàn bộ dữ liệu từ MongoDB Atlas
        /// </summary>
        private async void BtnRefreshAll_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
        }

        /// <summary>
        /// Sự kiện khi bấm nút "🔄 Làm mới Dashboard": Chạy lại Aggregation Pipeline để cập nhật số liệu
        /// </summary>
        private async void BtnRefreshDashboard_Click(object sender, RoutedEventArgs e)
        {
            TxtStatusBar.Text = "Đang tính toán lại số liệu Dashboard qua Aggregation Pipeline...";
            await LoadDashboardDataAsync();
            TxtStatusBar.Text = "Đã cập nhật số liệu Dashboard mới nhất.";
        }

        #endregion
    }
}