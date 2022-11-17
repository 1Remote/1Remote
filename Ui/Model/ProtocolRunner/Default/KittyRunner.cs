using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using _1RM.Utils.KiTTY;

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
            get
            {
                return string.IsNullOrEmpty(_puttyThemeName) ? PuttyThemeNames.First() : _puttyThemeName;
            }
            set => SetAndNotifyIfChanged(ref _puttyThemeName, value);
        }

        [JsonIgnore]
        public ObservableCollection<string> PuttyThemeNames => new ObservableCollection<string>(PuttyThemes.GetThemes().Keys);
    }
}
