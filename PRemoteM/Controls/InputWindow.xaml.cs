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
            OK = 1,
            Cancel = 2
        }

        public string Prompt { get; private set; }

        public string Response { get; private set; }

        public Results Result { get; private set; }
        public Func<string, string> Validater { get; private set; } = null;

        protected InputWindow()
        {
            InitializeComponent();
        }

        public static string InputBox(string prompt, string title = "", string defaultResponse = "", Func<string, string> validate = null)
        {
            var inputWindow = new InputWindow
            {
                Title = title,
                Prompt = prompt,
                Response = defaultResponse,
                Result = Results.Cancel,
                Validater = validate
            };

            inputWindow.ShowDialog();

            return inputWindow.Result == Results.OK ? inputWindow.Response : string.Empty;
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
                var msg = Validater(textBox.Text);
                if (string.IsNullOrWhiteSpace(msg) == false)
                {
                    alert.Visibility = Visibility.Visible;
                    alert.Text = msg;
                }
                else
                {
                    alert.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (Validater != null)
            {
                var msg = Validater(textBox.Text);
                if (string.IsNullOrWhiteSpace(msg) == false)
                {
                    alert.Visibility = Visibility.Visible;
                    alert.Text = msg;
                    return;
                }
            }
            Result = Results.OK;
            Response = textBox.Text;
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = Results.Cancel;
            Response = null;
            Close();
        }
    }
}
