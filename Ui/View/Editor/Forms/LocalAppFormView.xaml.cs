using System;
using _1RM.Model.Protocol;
using System.Windows;
using System.Windows.Controls;
using Shawn.Utils.Wpf.FileSystem;

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
            var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
            if (path == null) return;
            if (DataContext is LocalApp app)
                app.PrivateKey = path;
        }
    }
}
