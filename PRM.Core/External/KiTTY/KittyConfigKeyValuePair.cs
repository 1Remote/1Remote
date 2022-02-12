using System.Linq;
using Microsoft.Win32;

namespace PRM.Core.Protocol.Putty
{
    public class KittyConfigKeyValuePair
    {
        public static KittyConfigKeyValuePair Create(string key, int value)
        {
            return new KittyConfigKeyValuePair
            {
                Key = key,
                Value = value,
                ValueKind = RegistryValueKind.DWord,
            };
        }

        public static KittyConfigKeyValuePair Create(string key, string value)
        {
            if (key.ToCharArray().Count(c => c == '.') <= 1 
                && double.TryParse(value.Replace(',', '_'), out double nValue))
            {
                return new KittyConfigKeyValuePair
                {
                    Key = key,
                    Value = value,
                    ValueKind = RegistryValueKind.DWord,
                };
            }
            else
            {
                return new KittyConfigKeyValuePair
                {
                    Key = key,
                    Value = value,
                    ValueKind = RegistryValueKind.String,
                };
            }
        }

        public string Key;
        public object Value;
        public RegistryValueKind ValueKind;
    }
}