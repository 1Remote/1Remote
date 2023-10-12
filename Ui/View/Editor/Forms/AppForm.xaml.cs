using System;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using _1RM.Utils;
using System.Windows;
using System.Windows.Controls;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class AppForm : FormBase
    {
        public readonly AppFormViewModel ViewModel;
        public AppForm(ProtocolBase vm) : base(vm)
        {
            if (_vm is not LocalApp app)
            {
                throw new Exception($"passing none {nameof(LocalApp)} to {nameof(AppForm)}!");
            }

            ViewModel = new AppFormViewModel(app);
            InitializeComponent();
            DataContext = ViewModel;
        }

        public override bool CanSave()
        {
            if (_vm is LocalApp app)
            {
                if (!app.Verify())
                    return false;
                if (string.IsNullOrEmpty(app.ExePath))
                    return false;
            }

            if (!string.IsNullOrEmpty(ViewModel[nameof(ViewModel.Address)])
               || !string.IsNullOrEmpty(ViewModel[nameof(ViewModel.Port)])
               || !string.IsNullOrEmpty(ViewModel[nameof(ViewModel.UserName)])
               || !string.IsNullOrEmpty(ViewModel[nameof(ViewModel.Password)])
                   )
                return false;

            return true;
        }

        private void ButtonOpenPrivateKey_OnClick(object sender, RoutedEventArgs e)
        {
            var path = SelectFileHelper.OpenFile(filter: "ppk|*.*", currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
            if (path == null) return;
            ViewModel.PrivateKey = path;
        }
    }
}
