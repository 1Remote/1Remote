using System;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using System.Windows;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class LocalAppFormView : FormBase
    {
        public LocalAppFormView(ProtocolBase vm)
        {
            // TODO 改为 MVVM 模式
            InitializeComponent();
        }

        public override bool CanSave()
        {
            if (DataContext is LocalApp app)
            {
                if (!app.Verify())
                    return false;
                if (string.IsNullOrEmpty(app.ExePath))
                    return false;
                if (!string.IsNullOrEmpty(app[nameof(app.Address)])
                    || !string.IsNullOrEmpty(app[nameof(app.Port)])
                    || !string.IsNullOrEmpty(app[nameof(app.UserName)])
                    || !string.IsNullOrEmpty(app[nameof(app.Password)])
                   )
                    return false;
                return true;
            }
            return false;
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
