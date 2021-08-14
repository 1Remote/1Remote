using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRM.Core.External.KiTTY;

namespace PRM.Core.Protocol.Runner.Default
{
    public class SshDefaultRunner : ExternRunner
    {
        public new static string Name = "Default";
        public SshDefaultRunner() : base(Name)
        {
            ExePath = PuttyConnectableExtension.GetKittyExeFullName();
        }

        private static int _puttyFontSize = 14;
        public int PuttyFontSize
        {
            get => _puttyFontSize;
            set => SetAndNotifyIfChanged(nameof(PuttyFontSize), ref _puttyFontSize, value);
        }

        public static int GetPuttyFontSize()
        {
            return _puttyFontSize;
        }

        private static string _puttyThemeName = "";
        public string PuttyThemeName
        {
            get => string.IsNullOrEmpty(_puttyThemeName) ? PuttyThemeNames.First() : _puttyThemeName;
            set => SetAndNotifyIfChanged(nameof(PuttyThemeName), ref _puttyThemeName, value);
        }

        public static string GetPuttyThemeName()
        {
            return string.IsNullOrEmpty(_puttyThemeName) ? PuttyThemes.GetThemes().Keys.First() : _puttyThemeName;
        }

        private ObservableCollection<string> PuttyThemeNames => new ObservableCollection<string>(PuttyThemes.GetThemes().Keys);

        public void Save()
        {
            //_ini.WriteValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize.ToString());
            //_ini.WriteValue(nameof(PuttyThemeName).ToLower(), _sectionName, PuttyThemeName);
        }
        public void Load()
        {
            //PuttyThemeName = _ini.GetValue(nameof(PuttyThemeName).ToLower(), _sectionName, PuttyThemeName);
            //if (!PuttyThemeNames.Contains(PuttyThemeName))
            //    PuttyThemeName = PuttyThemeNames.First();
            //PuttyFontSize = _ini.GetValue(nameof(PuttyFontSize).ToLower(), _sectionName, PuttyFontSize);
        }
    }
}
