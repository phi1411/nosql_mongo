using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StudentManagementApp.Models;
using StudentManagementApp.Services;

namespace StudentManagementApp.Views
{
    /// <summary>
    /// Hộp thoại thêm môn học mới với tính năng tự động gợi ý mã môn (Autocomplete) và chống trùng lặp mã môn
    /// </summary>
    public partial class AddSubjectDialog : Window
    {
        public MonHoc? CreatedSubject { get; private set; }
        private readonly List<string> _existingMaMon = new();
        private List<MonHocSuggestionDto> _allSubjects = new();
        private bool _isSelectingSuggestion = false;

        /// <summary>
        /// Constructor nhận vào danh sách các mã môn học hiện có của sinh viên để chống thêm trùng
        /// </summary>
        public AddSubjectDialog(IEnumerable<string>? existingMaMon = null)
        {
            InitializeComponent();
            if (existingMaMon != null)
            {
                _existingMaMon = existingMaMon.Select(m => m.Trim().ToLower()).ToList();
            }
            TxtMaMon.Focus();
        }

        /// <summary>
        /// Khi mở Dialog: Tự động tải danh sách toàn bộ các môn học trong CSDL qua Aggregation Pipeline để làm gợi ý
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _allSubjects = await MongoDbService.Instance.GetDistinctSubjectsAsync();
            }
            catch
            {
                // Fallback danh sách môn học mặc định nếu chưa có kết nối
                _allSubjects = new List<MonHocSuggestionDto>
                {
                    new() { MaMon = "csdl", TenMon = "Cơ sở dữ liệu" },
                    new() { MaMon = "csdl_nc", TenMon = "Cơ sở dữ liệu nâng cao" },
                    new() { MaMon = "laptrinh", TenMon = "Lập trình Cơ bản" },
                    new() { MaMon = "web", TenMon = "Lập trình Web" },
                    new() { MaMon = "ai", TenMon = "Trí tuệ nhân tạo" },
                    new() { MaMon = "cloud", TenMon = "Điện toán đám mây" },
                    new() { MaMon = "security", TenMon = "An toàn thông tin" },
                    new() { MaMon = "mobile", TenMon = "Lập trình Di động" },
                    new() { MaMon = "mangmaytinh", TenMon = "Mạng máy tính" },
                    new() { MaMon = "ctdl", TenMon = "Cấu trúc dữ liệu & Giải thuật" },
                    new() { MaMon = "uxui", TenMon = "Thiết kế Giao diện" }
                };
            }
        }

        /// <summary>
        /// Sự kiện khi người dùng gõ mã môn: Tự động tìm kiếm và hiển thị danh sách gợi ý (Autocomplete)
        /// </summary>
        private void TxtMaMon_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingSuggestion) return;

            string keyword = TxtMaMon.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                BrdSuggestions.Visibility = Visibility.Collapsed;
                return;
            }

            // Lọc các môn có Mã môn hoặc Tên môn khớp với từ khóa người dùng gõ (Regex/Contains)
            var matches = _allSubjects
                .Where(s => s.MaMon.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                            s.TenMon.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            if (matches.Count > 0)
            {
                LstSuggestions.ItemsSource = matches;
                BrdSuggestions.Visibility = Visibility.Visible;
            }
            else
            {
                BrdSuggestions.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Sự kiện khi người dùng click chọn một môn học từ danh sách gợi ý
        /// </summary>
        private void LstSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSuggestions.SelectedItem is MonHocSuggestionDto selected)
            {
                _isSelectingSuggestion = true;
                TxtMaMon.Text = selected.MaMon;
                TxtTenMon.Text = selected.TenMon;
                _isSelectingSuggestion = false;

                BrdSuggestions.Visibility = Visibility.Collapsed;
                TxtDiem.Focus();
                TxtDiem.SelectAll();
            }
        }

        /// <summary>
        /// Cho phép dùng phím mũi tên Xuống để chọn gợi ý nhanh
        /// </summary>
        private void TxtMaMon_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && BrdSuggestions.Visibility == Visibility.Visible && LstSuggestions.Items.Count > 0)
            {
                LstSuggestions.Focus();
                LstSuggestions.SelectedIndex = 0;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Sự kiện khi bấm nút "Thêm vào CSDL ($push)": Kiểm tra hợp lệ và chống trùng lặp mã môn
        /// </summary>
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string maMon = TxtMaMon.Text.Trim();
            string tenMon = TxtTenMon.Text.Trim();
            string rawDiem = TxtDiem.Text.Trim().Replace(',', '.');

            // 1. Kiểm tra trống
            if (string.IsNullOrWhiteSpace(maMon))
            {
                MessageBox.Show("Mã môn học không được để trống!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMaMon.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(tenMon))
            {
                MessageBox.Show("Tên môn học không được để trống!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTenMon.Focus();
                return;
            }

            // 2. Chống trùng mã môn học (Tiêu chí chống trùng lặp môn học trước và sau)
            if (_existingMaMon.Any(m => string.Equals(m, maMon, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Môn học có mã '{maMon}' đã có trong danh sách môn học của sinh viên này!\n\nVui lòng không thêm trùng mã môn học.", "Trùng mã môn học", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMaMon.Focus();
                return;
            }

            // 3. Kiểm tra điểm số
            if (!double.TryParse(rawDiem, NumberStyles.Any, CultureInfo.InvariantCulture, out double diem) || diem < 0.0 || diem > 10.0)
            {
                MessageBox.Show("Điểm số môn học phải là số thực từ 0.0 đến 10.0!", "Lỗi xác thực", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDiem.Focus();
                return;
            }

            CreatedSubject = new MonHoc
            {
                MaMon = maMon.ToLower(),
                TenMon = tenMon,
                Diem = diem
            };

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Đóng hộp thoại
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
