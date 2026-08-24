using System.Globalization;
using System.Windows;
using StudentManagementApp.Models;

namespace StudentManagementApp.Views
{
    public partial class AddSubjectDialog : Window
    {
        public MonHoc? CreatedSubject { get; private set; }

        public AddSubjectDialog()
        {
            InitializeComponent();
            TxtMaMon.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string maMon = TxtMaMon.Text.Trim();
            string tenMon = TxtTenMon.Text.Trim();
            string rawDiem = TxtDiem.Text.Trim().Replace(',', '.');

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

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
