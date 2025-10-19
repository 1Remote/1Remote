using System;
using _1RM.Model.Protocol;
using System.Windows;
using System.Windows.Controls;
using _1RM.Utils;

namespace _1RM.View.Editor.Forms
{
    public partial class LocalAppFormView : UserControl
    {
        public LocalAppFormView()
        {
            InitializeComponent();
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory, owner: Window.GetWindow(this));
            if (path == null) return;
            if (DataContext is LocalApp app)
                app.PrivateKey = path;
        }

        private void TextBoxExePath_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is LocalAppFormViewModel vm)
            {
                vm.ResSetArguments();
            }
        }
    }
}
