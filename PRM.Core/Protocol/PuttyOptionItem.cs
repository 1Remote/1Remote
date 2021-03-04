using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using PRM.Core.Protocol.Putty;

namespace PRM.Core.Protocol
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
            if (double.TryParse(value, out double nValue))
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
