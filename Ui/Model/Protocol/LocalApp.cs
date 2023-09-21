using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using _1Remote.Security;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.View;
using ICSharpCode.AvalonEdit.Editing;

namespace _1RM.Model.Protocol
{
    public enum ArgumentType
    {
        Normal,
        /// <summary>
        /// e.g. -f X:\makefile
        /// </summary>
        File,
        Secret,
        /// <summary>
        /// e.g. --hide
        /// </summary>
        Flag,
        Selection,
    }
    public class Argument : NotifyPropertyChangedBase
    {
        private ArgumentType _type;
        public ArgumentType Type
        {
            get => _type;
            set
            {
                if (SetAndNotifyIfChanged(ref _type, value))
                {
                    // TODO reset value when type is changed
                }
            }
        }

        private bool _isRequired = false;
        public bool IsRequired
        {
            get => _isRequired;
            set => SetAndNotifyIfChanged(ref _isRequired, value);
        }

        private string _name = "";
        public string Name
        {
            get => _name.Trim();
            set => SetAndNotifyIfChanged(ref _name, value.Trim());
        }

        private string _key = "";
        public string Key
        {
            get => _key.Trim();
            set
            {
                if (SetAndNotifyIfChanged(ref _key, value.Trim()))
                    RaisePropertyChanged(nameof(DemoArgumentString));
            }
        }

        private string _value = "";
        public string Value
        {
            get
            {
                if (Type == ArgumentType.Selection 
                    && !_selections.Contains(_value))
                {
                    _value = _selections.FirstOrDefault() ?? "";
                }
                return _value.Trim();
            }
            set
            {
                if(SetAndNotifyIfChanged(ref _value, value.Trim()))
                    RaisePropertyChanged(nameof(DemoArgumentString));
            }
        }

        private List<string> _selections = new List<string>();
        public List<string> Selections
        {
            get => _selections;
            set
            {
                var auto = value.Distinct().Select(x => x.Trim()).Where(x => x != "").ToList();
                if (Type == ArgumentType.Selection)
                {
                    if (auto.Any() == false)
                    {
                        throw new ArgumentException("TXT: can not be null");
                    }
                    if (!IsRequired)
                    {
                        auto.Insert(0, "");
                    }
                    if (!_selections.Contains(Value))
                    {
                        Value = _selections.First();
                    }
                }
                _selections = auto;
                RaisePropertyChanged();
            }
        }

        public string DemoArgumentString => GetArgumentString(true);

        public string GetArgumentString(bool forDemo = false)
        {
            if (Type == ArgumentType.Flag)
            {
                return Value == "1" ? Key : "";
            }

            if (string.IsNullOrEmpty(Value) && !IsRequired)
            {
                return "";
            }

            var value = Value;
            if (Type == ArgumentType.Secret)
            {
                if (forDemo)
                {
                    value = "******";
                }
                else
                {
                    UnSafeStringEncipher.DecryptOrReturnOriginalString(Value);
                }
            }
            if (value.IndexOf(" ", StringComparison.Ordinal) > 0)
                value = $"\"{value}\"";
            if (!string.IsNullOrEmpty(Key))
            {
                value = $"{Key} {value}";
            }
            return value;
        }

        public static string GetArgumentsString(IEnumerable<Argument> arguments)
        {
            string cmd = "";
            foreach (var argument in arguments)
            {
                cmd += argument.GetArgumentString() + " ";
            }
            return cmd.Trim();
        }

        private RelayCommand? _cmdSelectArgumentFile;
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
                        initPath = new FileInfo(o?.ToString() ?? "").DirectoryName!;
                    }
                    catch (Exception)
                    {
                        initPath = Environment.CurrentDirectory;
                    }
                    var path = SelectFileHelper.OpenFile(initialDirectory: initPath, currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                    if (path == null) return;
                    Value = path;
                });
            }
        }
    }

    public class LocalApp : ProtocolBase
    {
        public LocalApp() : base("APP", "APP.V1", "APP")
        {
            _appProtocolDisplayName = "APP";
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }


        private string _appSubTitle = "";
        public string AppSubTitle
        {
            get => _appSubTitle;
            set => SetAndNotifyIfChanged(ref _appSubTitle, value);
        }

        private string _exePath = "";
        public string ExePath
        {
            get => _exePath;
            set => SetAndNotifyIfChanged(ref _exePath, value);
        }

        private string _appProtocolDisplayName = "";
        public string AppProtocolDisplayName
        {
            get => _appProtocolDisplayName;
            set => SetAndNotifyIfChanged(ref _appProtocolDisplayName, value);
        }

        public override string GetProtocolDisplayName()
        {
            return _appProtocolDisplayName;
        }

        [Obsolete]
        private string _arguments = "";
        [Obsolete]
        public string Arguments
        {
            get => _arguments;
            set => SetAndNotifyIfChanged(ref _arguments, value);
        }

        private List<Argument> _argumentList = new List<Argument>();
        public List<Argument> ArgumentList
        {
            get
            {
                if (_argumentList.Count == 0)
                {
                    _argumentList.Add(new Argument()
                    {
                        Name = "key1",
                        Key = "--key1",
                        Type = ArgumentType.Normal,
                        Selections = new List<string>() { "ABC", "ZZW1", "CAWE" },
                    });
                    _argumentList.Add(new Argument()
                    {
                        Name = "key1.1",
                        Key = "--key1.1",
                        Type = ArgumentType.Normal,
                    });
                    _argumentList.Add(new Argument()
                    {
                        Name = "key2",
                        Key = "--key2",
                        Type = ArgumentType.Secret,
                    });
                    _argumentList.Add(new Argument()
                    {
                        Name = "key3",
                        Key = "--key3",
                        Type = ArgumentType.File,
                    });
                    _argumentList.Add(new Argument()
                    {
                        Name = "key4",
                        Key = "--key4",
                        Type = ArgumentType.Selection,
                        Selections = new List<string>() { "ABC", "ZZW1", "CAWE" },
                    });
                    _argumentList.Add(new Argument()
                    {
                        Name = "key5",
                        Key = "--key5",
                        Type = ArgumentType.Flag,
                    });
                }
                return _argumentList;
            }
            set => SetAndNotifyIfChanged(ref _argumentList, value);
        }


        private bool _runWithHosting = false;
        public bool RunWithHosting
        {
            get => _runWithHosting;
            set => SetAndNotifyIfChanged(ref _runWithHosting, value);
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var app = JsonConvert.DeserializeObject<LocalApp>(jsonString);
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
            return string.IsNullOrEmpty(AppSubTitle) ? $"{this.ExePath} {this.ArgumentList}" : AppSubTitle;
        }

        public override double GetListOrder()
        {
            return 100;
        }

        public string GetArguments()
        {
            return Argument.GetArgumentsString(ArgumentList);
        }


        private RelayCommand? _cmdSelectExePath;
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
                        initPath = new FileInfo(ExePath).DirectoryName!;
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

        private RelayCommand? _cmdSelectArgumentFile;
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
                        initPath = new FileInfo(o?.ToString() ?? "").DirectoryName!;
                    }
                    catch (Exception)
                    {
                        initPath = Environment.CurrentDirectory;
                    }
                    var path = SelectFileHelper.OpenFile(initialDirectory: initPath, currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                    if (path == null) return;
                    //Arguments = path;
                });
            }
        }

        private RelayCommand? _cmdPreview;
        [JsonIgnore]
        public RelayCommand CmdPreview
        {
            get
            {
                return _cmdPreview ??= new RelayCommand((o) =>
                {
                    MessageBoxHelper.Info(ExePath + " " + GetArguments(), ownerViewModel: IoC.Get<MainWindowViewModel>());
                });
            }
        }

        private RelayCommand? _cmdTest;
        [JsonIgnore]
        public RelayCommand CmdTest
        {
            get
            {
                return _cmdTest ??= new RelayCommand((o) =>
                {
                    try
                    {
                        Process.Start(ExePath, Argument.GetArgumentsString(ArgumentList));
                        //    StartInfo =
                        //{
                        //    FileName = "cmd.exe",
                        //    UseShellExecute = false,
                        //    RedirectStandardInput = true,
                        //    RedirectStandardOutput = true,
                        //    RedirectStandardError = true,
                        //    CreateNoWindow = true
                        //}
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
                        MessageBoxHelper.ErrorAlert(e.Message);
                    }
                });
            }
        }
    }
}
