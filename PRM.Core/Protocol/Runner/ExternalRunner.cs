using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using JsonKnownTypes;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Core.I;
using PRM.Core.Protocol.Runner.Default;
using PRM.Core.Service;
using Shawn.Utils;

namespace PRM.Core.Protocol.Runner
{
    public class ExternalRunner : Runner
    {
        public ExternalRunner(string runnerName) : base(runnerName)
        {
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

        public Dictionary<string, string> EnvironmentVariables { get; set; }


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

                    if (string.IsNullOrEmpty(Arguments))
                    {
                        var name = new FileInfo(dlg.FileName).Name.ToLower();
                        if (name == "winscp.exe".ToLower())
                        {
                            _arguments = "sftp://%PRM_USER_NAME%:%PRM_PASSWORD%@%PRM_ADDRESS%:%PRM_PORT%";
                            RunWithHosting = true;
                        }
                        else if (name.IndexOf("kitty", StringComparison.Ordinal) >= 0 || name.IndexOf("putty", StringComparison.Ordinal) >= 0)
                        {
                            _arguments = @"-ssh %PRM_ADDRESS% -P %PRM_PORT% -l %PRM_USER_NAME% -pw %PRM_PASSWORD% -%PRM_SSH_VERSION% -cmd ""%PRM_STARTUP_AUTO_COMMAND%""";
                            RunWithHosting = true;
                        }
                        RaisePropertyChanged(nameof(Arguments));
                    }
                });
            }
        }

        public Process GetProcess()
        {
            if (File.Exists(ExePath))
            {
				// TODO: 设置环境变量
                var startInfo = new ProcessStartInfo();
                startInfo.EnvironmentVariables["RAYPATH"] = "test";

                startInfo.UseShellExecute = false;

                startInfo.FileName = ExePath;

                var p = new Process() { StartInfo = startInfo };
                return p;
            }
            return null;
        }
    }
}
