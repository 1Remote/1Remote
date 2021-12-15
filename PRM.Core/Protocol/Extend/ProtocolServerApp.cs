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
            return $"{this.ExePath} {this.Arguments}";
        }

        public override double GetListOrder()
        {
            return 7;
        }

        public string GetCmd()
        {
            // 若参数中有空格，则需要使用引号包裹命令参数
            if (ExePath.Trim().IndexOf(" ", StringComparison.Ordinal) > 0)
                return $"\"{this.ExePath}\" " + this.Arguments;
            return $"{this.ExePath} {this.Arguments}";
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

                    var path = SelectFileHelper.OpenFile(filter: "Exe|*.exe", initialDirectory: initPath, currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                    if (path == null) return;
                    ExePath = path;
                });
            }
        }

        private RelayCommand _cmdSelectArgumentFile;
        [JsonIgnore]
        public RelayCommand CmdSelectArgumentFile
        {
            get
            {
                return _cmdSelectArgumentFile ??= new RelayCommand((o) =>
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
                    var path = SelectFileHelper.OpenFile(initialDirectory: initPath, currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                    if (path == null) return;
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
                    try
                    {
                        Process.Start(ExePath, Arguments);

                        //var p = new Process
                        //{
                        //    StartInfo =
                        //{
                        //    FileName = "cmd.exe",
                        //    UseShellExecute = false,
                        //    RedirectStandardInput = true,
                        //    RedirectStandardOutput = true,
                        //    RedirectStandardError = true,
                        //    CreateNoWindow = true
                        //}
                        //};
                        //p.Start();
                        //p.StandardInput.WriteLine(GetCmd());
                        //p.StandardInput.WriteLine("exit");
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Error: " + e.Message);
                        throw;
                    }
                });
            }
        }
    }
}
