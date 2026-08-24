using System.Globalization;
using System.Windows;
using StudentManagementApp.Models;

namespace StudentManagementApp.Views
{
    public partial class UpdateSubjectGradeDialog : Window
    {
        public double NewGrade { get; private set; }

        public UpdateSubjectGradeDialog(SinhVien student, MonHoc subject)
        {
            InitializeComponent();
            TxtStudentInfo.Text = $"Sinh viên: {student.HoTen} ({student.MaSv}) - Lớp: {student.MaLop}";
            TxtSubjectInfo.Text = $"Môn học: {subject.TenMon} (Mã môn: {subject.MaMon}) - Điểm hiện tại: {subject.Diem}";
            TxtNewDiem.Text = subject.Diem.ToString(CultureInfo.InvariantCulture);
            TxtNewDiem.SelectAll();
            TxtNewDiem.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string rawDiem = TxtNewDiem.Text.Trim().Replace(',', '.');

            if (!double.TryParse(rawDiem, NumberStyles.Any, CultureInfo.InvariantCulture, out double diem) || diem < 0.0 || diem > 10.0)
            {
                MessageBox.Show("Điểm số mới phải là số thực từ 0.0 đến 10.0!", "Lỗi xác thực", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNewDiem.Focus();
                return;
            }

            NewGrade = diem;
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
