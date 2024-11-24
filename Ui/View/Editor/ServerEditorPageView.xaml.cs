using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using _1RM.Resources.Icons;
using _1RM.Service.DataSource;
using _1RM.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Image;

namespace _1RM.View.Editor
{
    public partial class ServerEditorPageView : UserControl
    {
        public ServerEditorPageView()
        {
            this.Loaded += (sender, args) =>
            {
                if (this.DataContext is ServerEditorPageViewModel vm)
                {
                    // add mode
                    if (vm.IsAddMode)
                    {
                        ButtonSave.Content = IoC.Translate("Add");
                        if (vm.Server.IconImg == null
                            && ServerIcons.Instance.IconsBase64.Count > 0)
                        {
                            var r = new Random(DateTime.Now.Millisecond);
                            vm.Server.IconBase64 = ServerIcons.Instance.IconsBase64[r.Next(0, ServerIcons.Instance.IconsBase64.Count)];
                        }
                    }
                }
            };
        }
        ~ServerEditorPageView()
        {
            Console.WriteLine($"Release {this.GetType().Name}({this.GetHashCode()})");
        }

        private void TextBoxMarkdown_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        private void ButtonShowNote_OnMouseEnter(object sender, MouseEventArgs e)
        {
            PopupNote.IsOpen = false;
            PopupNote.IsOpen = true;
        }
    }
}