using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using _1RM.Service;
using Stylet;

namespace _1RM.Controls
{
    /// <summary>
    /// PasswordInput.xaml 的交互逻辑
    /// </summary>
    public partial class PasswordInput : UserControl
    {
        public PasswordInput()
        {
            InitializeComponent();
            CipherTextBox.PasswordChanged += PasswordBox_PasswordChanged;
            PlainTextBox.TextChanged += PlainTextBoxOnPlainTextBoxChanged;
            PlainTextBox.Visibility = Visibility.Collapsed;
        }

        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("Password", typeof(string), typeof(PasswordInput),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnServerDataChanged)));

        public new void Focus()
        {
            CipherTextBox.Focus();
        }

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordInput p && e.NewValue is string s && p.CipherTextBox.Password != s)
            {
                p.CipherTextBox.PasswordChanged -= p.PasswordBox_PasswordChanged;
                p.PlainTextBox.TextChanged -= p.PlainTextBoxOnPlainTextBoxChanged;
                p.PlainTextBox.Text = s;
                p.CipherTextBox.Password = s;
                p.CipherTextBox.PasswordChanged += p.PasswordBox_PasswordChanged;
                p.PlainTextBox.TextChanged += p.PlainTextBoxOnPlainTextBoxChanged;
            }
        }
        public string Password
        {
            get => (string)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PlainTextBox.TextChanged -= PlainTextBoxOnPlainTextBoxChanged;
            PlainTextBox.Text = CipherTextBox.Password;
            PlainTextBox.TextChanged += PlainTextBoxOnPlainTextBoxChanged;
            Password = CipherTextBox.Password;
        }

        private void PlainTextBoxOnPlainTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            CipherTextBox.PasswordChanged -= PasswordBox_PasswordChanged;
            CipherTextBox.Password = PlainTextBox.Text;
            CipherTextBox.PasswordChanged += PasswordBox_PasswordChanged;
            Password = CipherTextBox.Password;
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            SecondaryVerificationHelper.VerifyAsyncUiCallBack(b =>
                {
                    Execute.OnUIThreadSync(() =>
                    {

                        if (b != true)
                        {
                            if (sender is CheckBox cb)
                                cb.IsChecked = false;
                            return;
                        }
                        PlainTextBox.Visibility = Visibility.Visible;
                        PlainTextBox.Focus();
                        PlainTextBox.CaretIndex = PlainTextBox.Text.Length;
                    });
                });
        }
        private void SetSelection(PasswordBox passwordBox, int start, int length)
        {
            passwordBox.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(passwordBox, new object[] { start, length });
        }
        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            CipherTextBox.Focus();
            SetSelection(CipherTextBox, CipherTextBox.Password.Length, 0);
            PlainTextBox.Visibility = Visibility.Hidden;
        }
    }
}
