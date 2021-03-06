using Microsoft.Win32;

namespace PRM.Core.Protocol.Putty
{
    public class PuttyOptionItem
    {
        public PuttyOptionItem() { }
        public static PuttyOptionItem Create(string key, int value)
        {
            return new PuttyOptionItem
            {
                Key = key,
                Value = value,
                ValueKind = RegistryValueKind.DWord,
            };
        }
        public static PuttyOptionItem Create(string key, string value)
        {
            if (double.TryParse(value.Replace(',', '_'), out double nValue))
            {
                return new PuttyOptionItem
                {
                    Key = key,
                    Value = value,
                    ValueKind = RegistryValueKind.DWord,
                };
            }
            else
            {
                return new PuttyOptionItem
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
