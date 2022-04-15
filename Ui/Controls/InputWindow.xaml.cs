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
using System.Windows.Shapes;

namespace PRM.Controls
{
    public partial class InputWindow : Window
    {
        public enum Results
        {
            Ok = 1,
            Cancel = 2
        }

        public string Prompt { get; private init; }

        public string? Response { get; private set; }

        public Results Result { get; private set; }

        /// <summary>
        /// input string return error message or null
        /// </summary>
        public Func<string, string>? Validator { get; private init; }

        protected InputWindow()
        {
            InitializeComponent();
        }

        public static string InputBox(string prompt, string title = "", string defaultResponse = "", Func<string, string>? validate = null, Window? owner = null)
        {
            var inputWindow = new InputWindow
            {
                Title = title,
                Prompt = prompt,
                Response = defaultResponse,
                Result = Results.Cancel,
                Validator = validate,
                ShowInTaskbar = false,
            };
            if (owner != null)
            {
                inputWindow.Owner = owner;
                inputWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            inputWindow.ShowDialog();

            return inputWindow.Result == Results.Ok ? inputWindow.Response : string.Empty;
        }

        private void inputWindow_Loaded(object sender, RoutedEventArgs e)
        {
            textBlock.Text = Prompt;
            textBox.Text = Response;
            textBox.Focus();
            textBox.CaretIndex = Response?.Length ?? 0;
        }

        private void textBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                okButton_Click(sender, null);
            }
            else
            {
                TestValidator();
            }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (TestValidator())
            {
                Result = Results.Ok;
                Response = textBox.Text;
                Close();
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = Results.Cancel;
            Response = null;
            Close();
        }

        private bool TestValidator()
        {
            if (Validator != null)
            {
                var msg = Validator(textBox.Text);
                if (string.IsNullOrWhiteSpace(msg) == false)
                {
                    alert.Visibility = Visibility.Visible;
                    alert.Text = msg;
                    return false;
                }
            }
            return true;
        }
    }
}
