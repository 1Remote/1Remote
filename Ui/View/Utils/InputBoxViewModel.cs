using Stylet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using _1RM.Service;

namespace _1RM.View.Utils
{
    /// <summary>
    /// Default implementation of IMessageBoxViewModel, and is therefore the ViewModel shown by default by ShowMessageBox
    /// </summary>
    public class InputBoxViewModel : NotifyPropertyChangedBaseScreen
    {

        /// <summary>
        /// input string return error message or null
        /// </summary>
        public new Func<string, string>? Validator { get; private set; }

        private string _validateMessage = "";
        public string ValidateMessage
        {
            get => _validateMessage;
            set => SetAndNotifyIfChanged(ref _validateMessage, value);
        }

        public virtual MessageBoxResult ClickedButton { get; protected set; }

        private string _response = "";
        public string Response
        {
            get => _response;
            set => SetAndNotifyIfChanged(ref _response, value.Trim());
        }

        public InputBoxViewModel(string caption, Func<string, string>? validator, string defaultResponse = "")
        {
            this.DisplayName = $"{caption}";
            Validator = validator;
            Response = defaultResponse;
        }


        private bool TestValidator()
        {
            if (Validator != null)
            {
                var msg = Validator(Response);
                if (string.IsNullOrWhiteSpace(msg) == false)
                {
                    ValidateMessage = msg;
                    return false;
                }
            }

            ValidateMessage = "";
            return true;
        }


        //private RelayCommand? _cmdShowTabByIndex;
        //public RelayCommand CmdShowTabByIndex
        //{
        //    get
        //    {
        //        return _cmdShowTabByIndex ??= new RelayCommand((o) =>
        //        {
        //            if (int.TryParse(o?.ToString() ?? "0", out int i))
        //            {
        //                if (i > 0 && i <= Items.Count)
        //                {
        //                    SelectedItem = Items[i - 1];
        //                }
        //            }
        //        }, o => this.SelectedItem != null);
        //    }
        //}

        public void KeyDown(Key key = Key.None)
        {
            if (key == Key.Enter)
            {
                ButtonClicked(MessageBoxResult.OK);
            }
            else if (key == Key.Escape)
            {
                ButtonClicked(MessageBoxResult.Cancel);
            }
            else
            {
                TestValidator();
            }
        }

        public void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            TestValidator();
        }

        /// <summary>
        /// Called when MessageBoxView when the user clicks a button
        /// </summary>
        /// <param name="button">Button which was clicked</param>
        public void ButtonClicked(MessageBoxResult button)
        {
            Response = Response.Trim();
            this.ClickedButton = button;
            if (button != MessageBoxResult.OK)
            {
                Response = string.Empty;
                this.RequestClose(false);
            }
            else if (TestValidator())
            {
                this.RequestClose(true);
            }
        }

        public static string? GetValue(string caption, Func<string, string>? validator = null, string defaultResponse = "", IViewAware? ownerViewModel = null)
        {
            var vm = new InputBoxViewModel(caption, validator, defaultResponse);
            if (true == IoC.Get<IWindowManager>().ShowDialog(vm, ownerViewModel))
            {
                return vm.Response;
            }
            else
            {
                return null;
            }
        }
    }
}
