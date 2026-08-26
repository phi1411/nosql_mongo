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
    public partial class MainWindow : Window
    {
        private readonly MongoDbService _mongoService;
        private List<SinhVien> _allStudents = new();
        private SinhVien? _selectedStudent;

        // Observable collections cho form động
        private readonly ObservableCollection<string> _currentNgoaiNgu = new();
        private readonly ObservableCollection<MonHoc> _currentMonHoc = new();

        public MainWindow()
        {
            InitializeComponent();
            _mongoService = MongoDbService.Instance;
            LstNgoaiNgu.ItemsSource = _currentNgoaiNgu;
            DgMonHoc.ItemsSource = _currentMonHoc;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtStatusBar.Text = "Đang kết nối MongoDB Atlas và khởi tạo Indexes...";
            try
            {
                // Khởi tạo Unique Index và Compound Index
                await _mongoService.InitializeIndexesAsync();
                TxtStatusBadge.Text = "MongoDB Connected";
                TxtStatusBar.Text = "Đã kết nối MongoDB Atlas & thiết lập Indexes thành công.";

                // Tải dữ liệu
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

                ApplyFilter();

                // Tải dữ liệu Dashboard
                await LoadDashboardDataAsync();
                TxtStatusBar.Text = $"Đã tải thành công {_allStudents.Count} sinh viên.";
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = $"Lỗi tải dữ liệu: {ex.Message}";
            }
        }

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

            // 3. Sắp xếp theo tùy chọn
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

        private static string GetTenGoi(string hoTen)
        {
            if (string.IsNullOrWhiteSpace(hoTen)) return string.Empty;
            var parts = hoTen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[^1] : hoTen;
        }

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

                // 4. Top 5 sinh viên
                DgTop5Students.ItemsSource = summary.Top5SinhVien;

                // 5. Phân loại học lực
                IcAcademicClass.ItemsSource = summary.PhanLoaiHocLuc;
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = $"Lỗi cập nhật Dashboard: {ex.Message}";
            }
        }

        #endregion

        #region Bộ lọc & Tìm kiếm

        private void CboFilterClass_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void CboSortBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchMaSv.Text = string.Empty;
            CboFilterClass.SelectedIndex = 0;
            CboSortBy.SelectedIndex = 0;
            ApplyFilter();
            TxtStatusBar.Text = "Đã đặt lại toàn bộ bộ lọc và sắp xếp về mặc định.";
        }

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

                // Cập nhật ObservableCollection cho Ngoại ngữ
                _currentNgoaiNgu.Clear();
                if (sv.NgoaiNgu != null)
                {
                    foreach (var nn in sv.NgoaiNgu)
                    {
                        _currentNgoaiNgu.Add(nn);
                    }
                }

                // Cập nhật ObservableCollection cho Môn học
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

        private void BtnClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        #endregion

        #region Thao tác Mảng Động & Positional Operator ($)

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

        private async void BtnAddSubject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSubjectDialog { Owner = this };
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
                // Bắt lỗi Unique Index
                MessageBox.Show(ex.Message, "Lỗi Trùng Mã Sinh Viên (Unique Index Violation)", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtFormMaSv.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi thêm mới: {ex.Message}", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        private async void BtnSeedData_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Bạn có muốn nạp dữ liệu mẫu từ tệp 'data_seed.json' (200 sinh viên) vào CSDL MongoDB không?\n\nLưu ý: Hành động này sẽ làm mới toàn bộ collection sinhvien!",
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

        private async void BtnRefreshAll_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
        }

        private async void BtnRefreshDashboard_Click(object sender, RoutedEventArgs e)
        {
            TxtStatusBar.Text = "Đang tính toán lại số liệu Dashboard qua Aggregation Pipeline...";
            await LoadDashboardDataAsync();
            TxtStatusBar.Text = "Đã cập nhật số liệu Dashboard mới nhất.";
        }

        #endregion
    }
}