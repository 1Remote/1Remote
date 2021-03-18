using System.IO;
using System.Windows;
using PRM.Core.DB;
using PRM.Core.Model;
using Shawn.Utils;

using Shawn.Utils;

using NotifyPropertyChangedBase = PRM.Core.NotifyPropertyChangedBase;

namespace PRM.ViewModel
{
    public class VmSystemConfigPage : NotifyPropertyChangedBase
    {
        public readonly VmMain Host;
        private readonly PrmContext _context;

        public VmSystemConfigPage(VmMain vmMain, PrmContext context)
        {
            Host = vmMain;
            _context = context;
            // create new SystemConfigGeneral object
            SystemConfig = SystemConfig.Instance;
        }

        public SystemConfig SystemConfig { get; set; }

        private Visibility _progressBarVisibility = Visibility.Collapsed;

        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set => SetAndNotifyIfChanged(nameof(ProgressBarVisibility), ref _progressBarVisibility, value);
        }

        #region CMD

        private RelayCommand _cmdSaveAndGoBack;

        public RelayCommand CmdSaveAndGoBack
        {
            get
            {
                if (_cmdSaveAndGoBack != null) return _cmdSaveAndGoBack;
                _cmdSaveAndGoBack = new RelayCommand((o) =>
                {
                    // check if Db is ok
                    var res = _context.DbOperator.CheckDbRsaStatus();
                    if (res != EnumDbStatus.OK)
                    {
                        MessageBox.Show(res.GetErrorInfo(SystemConfig.Instance.Language, SystemConfig.Instance.DataSecurity.DbPath), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                        return;
                    }

                    Host.DispPage = null;
                    /***                update config                ***/
                    SystemConfig.Save();
                });
                return _cmdSaveAndGoBack;
            }
        }

        private RelayCommand _cmdOpenPath;

        public RelayCommand CmdOpenPath
        {
            get
            {
                if (_cmdOpenPath != null) return _cmdOpenPath;
                _cmdOpenPath = new RelayCommand((o) =>
                {
                    var path = o.ToString();
                    if (File.Exists(path))
                    {
                        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe");
                        psi.Arguments = "/e,/select," + path;
                        System.Diagnostics.Process.Start(psi);
                    }

                    if (Directory.Exists(path))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", path);
                    }
                });
                return _cmdOpenPath;
            }
        }

        #endregion CMD
    }
}