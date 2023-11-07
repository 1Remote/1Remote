using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
            base._name = Name;
            CodePages = new List<string>
            {
                "UTF-8",
                "ISO-8859-1:1998 (Latin-1, West Europe)",
                "ISO-8859-2:1999 (Latin-2, East Europe)",
                "ISO-8859-3:1999 (Latin-3, South Europe)",
                "ISO-8859-4:1998 (Latin-4, North Europe)",
                "ISO-8859-5:1999 (Latin/Cyrillic)",
                "ISO-8859-6:1999 (Latin/Arabic)",
                "ISO-8859-7:1987 (Latin/Greek)",
                "ISO-8859-8:1999 (Latin/Hebrew)",
                "ISO-8859-9:1999 (Latin-5, Turkish)",
                "ISO-8859-10:1998 (Latin-6, Nordic)",
                "ISO-8859-11:2001 (Latin/Thai)",
                "ISO-8859-13:1998 (Latin-7, Baltic)",
                "ISO-8859-14:1998 (Latin-8, Celtic)",
                "ISO-8859-15:1999 (Latin-9, \"euro\")",
                "ISO-8859-16:2001 (Latin-10, Balkan)",
                "KOI8-U",
                "KOI8-R",
                "HP-ROMAN8",
                "VSCII",
                "DEC-MCS",
                "Win1250 (Central European)",
                "Win1251 (Cyrillic)",
                "Win1252 (Western)",
                "Win1253 (Greek)",
                "Win1254 (Turkish)",
                "Win1255 (Hebrew)",
                "Win1256 (Arabic)",
                "Win1257 (Baltic)",
                "Win1258 (Vietnamese)",
                "CP437",
                "CP620 (Mazovia)",
                "CP819",
                "CP852",
                "CP878",
                "Use font encoding",
            };
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

        [JsonIgnore]
        public List<string> CodePages { get; }

        private string _lineCodePage = "UTF-8";
        public string LineCodePage
        {
            get => _lineCodePage;
            set => SetAndNotifyIfChanged(ref _lineCodePage, value);
        }

        public string GetLineCodePageForIni()
        {
            //    { "UTF-8", "UTF-8" },
            //    { "ISO-8859-1:1998 (Latin-1, West Europe)", "ISO-8859-1%3A1998%20(Latin-1,%20West%20Europe)" },
            //    { "ISO-8859-5:1999 (Latin/Cyrillic)", "ISO-8859-5%3A1999%20(Latin%2FCyrillic)" },
            //    { "ISO-8859-15:1999 (Latin-9, \"euro\")", "ISO-8859-15%3A1999%20(Latin-9,%20%22euro%22)" },
            //    { "CP437", "CP437" },
            return _lineCodePage.Trim()
                .Replace(" ", "%20")
                .Replace("\"", "%22")
                .Replace("/", "%2F")
                .Replace(":", "%3A");
        }

        private string _puttyExePath = "";
        public string PuttyExePath
        {
            get => (File.Exists(_puttyExePath) ? _puttyExePath : PuttyConnectableExtension.GetInternalKittyExeFullName()).Replace(Environment.CurrentDirectory, ".");
            set => SetAndNotifyIfChanged(ref _puttyExePath, value.Replace(Environment.CurrentDirectory, "."));
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
