using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Core.Protocol.Base;
using PRM.Core.Protocol.VNC;
using Shawn.Utils;

namespace PRM.Core.Protocol.Extend
{
    public class ProtocolServerApp : ProtocolServerBase
    {
        public ProtocolServerApp() : base("APP", "APP.V1", "APP")
        {
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        protected string _exePath = "";
        public string ExePath
        {
            get => _exePath;
            set => SetAndNotifyIfChanged(ref _exePath, value);
        }


        protected string _arguments = "";
        public string Arguments
        {
            get => _arguments;
            set => SetAndNotifyIfChanged(ref _arguments, value);
        }


        protected bool _runWithHosting = true;
        public bool RunWithHosting
        {
            get => _runWithHosting;
            set => SetAndNotifyIfChanged(ref _runWithHosting, value);
        }

        public override ProtocolServerBase CreateFromJsonString(string jsonString)
        {
            try
            {
                var app = JsonConvert.DeserializeObject<ProtocolServerApp>(jsonString);
                return app;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        protected override string GetSubTitle()
        {
            return base.DisplayName;
        }

        public override double GetListOrder()
        {
            return 7;
        }

        private RelayCommand _cmdSelectDbPath;
        [JsonIgnore]
        public RelayCommand CmdSelectExePath
        {
            get
            {
                return _cmdSelectDbPath ??= new RelayCommand((o) =>
                {
                    string initPath;
                    try
                    {
                        initPath = new FileInfo(ExePath).DirectoryName;
                    }
                    catch (Exception)
                    {
                        initPath = Environment.CurrentDirectory;
                    }


                    var dlg = new OpenFileDialog
                    {
                        Filter = "Exe|*.exe",
                        CheckFileExists = true,
                        InitialDirectory = initPath,
                    };
                    if (dlg.ShowDialog() != true) return;
                    ExePath = dlg.FileName;
                });
            }
        }
    }
}
