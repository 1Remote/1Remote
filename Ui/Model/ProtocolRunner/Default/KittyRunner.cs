using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using _1RM.Model.Protocol;
using Newtonsoft.Json;
using _1RM.Utils.KiTTY;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Model.ProtocolRunner.Default
{
    public class KittyRunner : InternalDefaultRunner
    {
        public new static string Name = "Internal KiTTY";

        [JsonConstructor]
        public KittyRunner(string ownerProtocolName) : base(ownerProtocolName)
        {
            base.Name = Name;
        }

        private int _puttyFontSize = 14;
        public int PuttyFontSize
        {
            get => _puttyFontSize;
            set => SetAndNotifyIfChanged(ref _puttyFontSize, value);
        }

        public int GetPuttyFontSize()
        {
            return _puttyFontSize > 0 ? _puttyFontSize : 14;
        }

        private string _puttyThemeName = "";
        public string PuttyThemeName
        {
            get => PuttyThemeNames.Contains(_puttyThemeName) ? _puttyThemeName : PuttyThemeNames.First();
            set => SetAndNotifyIfChanged(ref _puttyThemeName, value);
        }

        [JsonIgnore]
        public ObservableCollection<string> PuttyThemeNames => new ObservableCollection<string>(PuttyThemes.Themes.Keys);



        private string _puttyExePath = "";
        public string PuttyExePath
        {
            get => File.Exists(_puttyExePath) ? _puttyExePath : PuttyConnectableExtension.GetInternalKittyExeFullName();
            set => SetAndNotifyIfChanged(ref _puttyExePath, value);
        }




        private RelayCommand? _cmdSelectDbPath;
        [JsonIgnore]
        public RelayCommand CmdSelectExePath
        {
            get
            {
                return _cmdSelectDbPath ??= new RelayCommand((o) =>
                {
                    string? initPath = null;
                    try
                    {
                        initPath = new FileInfo(PuttyExePath).DirectoryName;
                    }
                    catch
                    {
                        // ignored
                    }


                    var path = SelectFileHelper.OpenFile(filter: "exe|*.exe", checkFileExists: true, initialDirectory: initPath);
                    if (path == null) return;
                    PuttyExePath = path;
                });
            }
        }
    }
}
