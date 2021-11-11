using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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


        protected bool _runWithHosting = false;
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

        public string GetCmd()
        {
            return $"{this.ExePath} \"" + this.Arguments + "\"";
        }


        private string SelectFile(string filter, string initPath = null)
        {
            if(string.IsNullOrWhiteSpace(initPath) || Directory.Exists(initPath) == false)
                initPath = Environment.CurrentDirectory;

            var dlg = new OpenFileDialog
            {
                Filter = filter,
                CheckFileExists = true,
                InitialDirectory = initPath,
            };
            if (dlg.ShowDialog() != true) return null;
            return dlg.FileName;
        }

        private RelayCommand _cmdSelectExePath;
        [JsonIgnore]
        public RelayCommand CmdSelectExePath
        {
            get
            {
                return _cmdSelectExePath ??= new RelayCommand((o) =>
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
                    var path = SelectFile("Exe|*.exe", initPath);
                    if (string.IsNullOrEmpty(path) == false)
                        ExePath = path;
                });
            }
        }

        private RelayCommand _cmdSelectFilePath;
        [JsonIgnore]
        public RelayCommand CmdSelectFilePath
        {
            get
            {
                return _cmdSelectFilePath ??= new RelayCommand((o) =>
                {
                    string initPath;
                    try
                    {
                        initPath = new FileInfo(Arguments).DirectoryName;
                    }
                    catch (Exception)
                    {
                        initPath = Environment.CurrentDirectory;
                    }
                    var path = SelectFile("*|*.*", initPath);
                    if (string.IsNullOrEmpty(path) == false)
                        Arguments = path;
                });
            }
        }

        private RelayCommand _cmdPreview;
        [JsonIgnore]
        public RelayCommand CmdPreview
        {
            get
            {
                return _cmdPreview ??= new RelayCommand((o) =>
                {
                    MessageBox.Show(GetCmd());
                });
            }
        }

        private RelayCommand _cmdTest;
        [JsonIgnore]
        public RelayCommand CmdTest
        {
            get
            {
                return _cmdTest ??= new RelayCommand((o) =>
                {
                    Process.Start(ExePath, Arguments);
                });
            }
        }
    }
}
