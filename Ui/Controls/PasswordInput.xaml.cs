using _1RM.Utils.Windows;
using _1RM.View.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Shawn.Utils;
using _1RM.View;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection;
using _1RM.Utils;

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

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(PasswordInput),
        new PropertyMetadata(null, new PropertyChangedCallback(OnServerDataChanged)));

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

        private async void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (await WindowsHelloHelper.StrictHelloVerifyAsyncUi() != true)
            {
                if (sender is CheckBox cb)
                    cb.IsChecked = false;
                return;
            }
            PlainTextBox.Visibility = Visibility.Visible;
            PlainTextBox.Focus();
            PlainTextBox.CaretIndex = PlainTextBox.Text.Length;
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
