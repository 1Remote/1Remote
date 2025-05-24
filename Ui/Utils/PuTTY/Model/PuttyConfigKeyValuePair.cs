using System.Linq;
using Microsoft.Win32;

namespace _1RM.Utils.PuTTY.Model
{
    public class PuttyConfigKeyValuePair
    {
        public static PuttyConfigKeyValuePair Create(string key, int value)
        {
            return new PuttyConfigKeyValuePair
            {
                Key = key,
                Value = value,
                ValueKind = RegistryValueKind.DWord,
            };
        }

        public static PuttyConfigKeyValuePair Create(string key, string value)
        {
            if (key.ToCharArray().Count(c => c == '.') <= 1
                && double.TryParse(value.Replace(',', '_'), out var nValue))
            {
                return new PuttyConfigKeyValuePair
                {
                    Key = key,
                    Value = value,
                    ValueKind = RegistryValueKind.DWord,
                };
            }
            else
            {
                return new PuttyConfigKeyValuePair
                {
                    Key = key,
                    Value = value,
                    ValueKind = RegistryValueKind.String,
                };
            }
        }

        public string Key = "";
        public object Value = "";
        public RegistryValueKind ValueKind = RegistryValueKind.String;
    }
}