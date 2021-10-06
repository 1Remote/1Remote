using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PRM.Core.External.KiTTY;
using PRM.Core.Properties;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Service;

namespace PRM.Core.Protocol.Runner.Default
{
    public class SshDefaultRunner : ExternRunner
    {
        public new static string Name = "Default";
        public SshDefaultRunner() : base(Name, ProtocolServerSSH.ProtocolName)
        {
            _exePath = PuttyConnectableExtension.GetKittyExeFullName();
            base.Name = Name;
        }

        private static int _puttyFontSize = 14;
        public int PuttyFontSize
        {
            get => _puttyFontSize;
            set => SetAndNotifyIfChanged(ref _puttyFontSize, value);
        }

        public static int GetPuttyFontSize()
        {
            return _puttyFontSize;
        }

        private static string _puttyThemeName = "";
        public string PuttyThemeName
        {
            get => string.IsNullOrEmpty(_puttyThemeName) ? PuttyThemeNames.First() : _puttyThemeName;
            set => SetAndNotifyIfChanged(ref _puttyThemeName, value);
        }

        public static string GetPuttyThemeName()
        {
            return string.IsNullOrEmpty(_puttyThemeName) ? PuttyThemes.GetThemes().Keys.First() : _puttyThemeName;
        }

        [JsonIgnore]
        public ObservableCollection<string> PuttyThemeNames => new ObservableCollection<string>(PuttyThemes.GetThemes().Keys);
    }
}
