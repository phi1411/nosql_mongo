using System.Windows;
using System.Windows.Controls;

namespace StudentManagementApp.Views
{
    public partial class AddLanguageDialog : Window
    {
        public string? SelectedLanguage { get; private set; }

        public AddLanguageDialog()
        {
            InitializeComponent();
            TxtLanguage.Focus();
        }

        private void CboLanguagePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboLanguagePreset.SelectedItem is ComboBoxItem item && item.Content.ToString() != "-- Chọn nhanh hoặc tự nhập bên dưới --")
            {
                TxtLanguage.Text = item.Content.ToString() ?? string.Empty;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string lang = TxtLanguage.Text.Trim();
            if (string.IsNullOrWhiteSpace(lang))
            {
                MessageBox.Show("Vui lòng nhập hoặc chọn một ngoại ngữ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtLanguage.Focus();
                return;
            }

            SelectedLanguage = lang;
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
