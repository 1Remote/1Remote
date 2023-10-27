using System.IO;
using System.Windows;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils.KiTTY;
using _1RM.Utils.KiTTY.Model;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms
{
    public partial class SerialFormView : FormBase
    {
        public SerialFormView()
        {
            InitializeComponent();
        }

        private void ButtonSelectSessionConfigFile_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is IKittyConnectable pc)
            {
                var path = SelectFileHelper.OpenFile(filter: "KiTTY Session|*.*");
                if (path == null) return;
                if (File.Exists(path)
                    && KittyConfig.Read(path)?.Count > 0)
                {
                    pc.ExternalKittySessionConfigPath = path;
                }
                else
                {
                    pc.ExternalKittySessionConfigPath = "";
                }
            }
        }
    }
}
