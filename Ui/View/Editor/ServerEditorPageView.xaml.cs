using System;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Resources.Icons;

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
                    if (vm.Server.IsTmpSession())
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