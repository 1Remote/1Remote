using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Core.Protocol.Base
{
    public enum FieldType
    {
        String,
        Int,
    }

    public class ExtendOption : NotifyPropertyChangedBase
    {

        private string _fieldName = "";
        public string FieldName
        {
            get => _fieldName;
            set => SetAndNotifyIfChanged(nameof(FieldName), ref _fieldName, value);
        }


        private FieldType _type = FieldType.String;
        public FieldType Type
        {
            get => _type;
            set => SetAndNotifyIfChanged(nameof(Type), ref _type, value);
        }
    }
}
