using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Model.Protocol;

public enum AppArgumentType
{
    Normal,
    Int,
    Float,
    /// <summary>
    /// e.g. -f X:\makefile
    /// </summary>
    File,
    Secret,
    /// <summary>
    /// e.g. --hide
    /// </summary>
    Flag,
    Selection,

    Const,
}

public class AppArgument : NotifyPropertyChangedBase, ICloneable, IDataErrorInfo
{
    public AppArgument(bool? isEditable = true)
    {
        IsEditable = isEditable ?? true;
    }


    /// <summary>
    /// 批量编辑时，如果参数列表不同，禁用参数的编辑
    /// </summary>
    [JsonIgnore]
    public bool? IsEditable { get; }


    private AppArgumentType _type;
    [JsonConverter(typeof(StringEnumConverter))]
    public AppArgumentType Type
    {
        get => _type;
        set => SetAndNotifyIfChanged(ref _type, value);
    }

    private bool _isNullable = true;
    public bool IsNullable
    {
        get => _isNullable;
        set
        {
            SetAndNotifyIfChanged(ref _isNullable, value);
            RaisePropertyChanged(nameof(HintDescription));
        }
    }

    private string _name = "";
    public string Name
    {
        get => _name.Trim();
        set => SetAndNotifyIfChanged(ref _name, value.Trim());
    }

    private string _key = "";
    [DefaultValue("")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Key
    {
        get => _key.Trim();
        set
        {
            if (SetAndNotifyIfChanged(ref _key, value.Trim()))
                RaisePropertyChanged(nameof(DemoArgumentString));
        }
    }



    private bool _addBlankAfterKey = false;
    /// <summary>
    /// argument like "sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%" need it to be false
    /// </summary>
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool AddBlankAfterKey
    {
        get => _addBlankAfterKey;
        set => SetAndNotifyIfChanged(ref _addBlankAfterKey, value);
    }

    private string _value = "";
    public string Value
    {
        get => _value;
        set
        {
            if (SetAndNotifyIfChanged(ref _value, value))
            {
                RaisePropertyChanged(nameof(DemoArgumentString));
            }
        }
    }

    //private string _defaultValue = "";
    //public string DefaultValue
    //{
    //    get => _defaultValue;
    //    set => SetAndNotifyIfChanged(ref _defaultValue, value);
    //}

    private bool _addBlankAfterValue = true;
    /// <summary>
    /// argument like "sftp://%1RM_USERNAME%:%1RM_PASSWORD%@%1RM_HOSTNAME%:%1RM_PORT%" need it to be false
    /// </summary>
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public bool AddBlankAfterValue
    {
        get => _addBlankAfterValue;
        set => SetAndNotifyIfChanged(ref _addBlankAfterValue, value);
    }

    private string _description = "";
    public string Description
    {
        get => _description;
        set
        {
            SetAndNotifyIfChanged(ref _description, value);
            RaisePropertyChanged(nameof(HintDescription));
        }
    }


    [JsonIgnore] public string HintDescription => IsNullable ? $"({IoC.Translate("Optional")})" + _description : _description;



    private Dictionary<string, string> _selections = new Dictionary<string, string>();
    public Dictionary<string, string> Selections
    {
        get => _selections;
        set
        {
            var selections = new Dictionary<string, string>();
            var auto = value.Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToList();
            if (Type == AppArgumentType.Selection && IsNullable)
            {
                selections.Add("", "");
            }
            if (auto.Any())
            {
                foreach (var keyValuePair in auto)
                {
                    var v = keyValuePair.Value.Trim();
                    if (string.IsNullOrEmpty(v))
                    {
                        v = keyValuePair.Key.Trim();
                    }
                    selections.Add(keyValuePair.Key.Trim(), v);
                }
            }
            _selections = selections;
            if (Type == AppArgumentType.Selection)
            {
                Value = selections.LastOrDefault().Key ?? "";
            }
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(SelectionKeys));
        }
    }

    [JsonIgnore] public List<string> SelectionKeys => Selections.Keys.ToList();

    public object Clone()
    {
        var copy = (AppArgument)MemberwiseClone();
        copy.Selections = new System.Collections.Generic.Dictionary<string, string>(this.Selections);
        return copy;
    }

    public string DemoArgumentString => GetArgumentString(true);

    public string GetArgumentString(bool forDemo = false, LocalApp? app = null)
    {
        var key = $"{Key}{(AddBlankAfterKey ? " " : "")}";
        if (Type == AppArgumentType.Flag)
        {
            return Value == "1" ? key : "";
        }

        // REPLACE %xxx% with SystemEnvironment, 替换系统环境变量
        string value = Value;

        if (forDemo && app != null)
        {
            value = value.Replace(ProtocolBaseWithAddressPortUserPwd.MACRO_PASSWORD, "******");
        }

        if (app != null)
        {
            value = OtherNameAttributeExtensions.Replace(app, value);
        }
        value = Environment.ExpandEnvironmentVariables(value);

        if (IsNullable && string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (Type == AppArgumentType.Secret && !string.IsNullOrEmpty(Value))
        {
            value = forDemo ? "******" : UnSafeStringEncipher.DecryptOrReturnOriginalString(Value);
        }

        //if (value.IndexOf(" ", StringComparison.Ordinal) > 0)
        //{
        //    value = $"\"{value}\"";
        //}

        value = $"{value}{(AddBlankAfterValue ? " " : "")}";

        if (!string.IsNullOrEmpty(Key))
        {
            value = $"{key}{value}";
        }
        return value;
    }

    public static string GetArgumentsString(IEnumerable<AppArgument> arguments, bool isDemo, LocalApp? app)
    {
        string cmd = "";
        foreach (var argument in arguments)
        {
            cmd += argument.GetArgumentString(isDemo, app);
        }
        return cmd.Trim();
    }

    private RelayCommand? _cmdSelectArgumentFile;
    [JsonIgnore]
    public RelayCommand CmdSelectArgumentFile
    {
        get
        {
            return _cmdSelectArgumentFile ??= new RelayCommand((o) =>
            {
                string initPath;
                try
                {
                    initPath = new FileInfo(o?.ToString() ?? "").DirectoryName!;
                }
                catch (Exception)
                {
                    initPath = Environment.CurrentDirectory;
                }
                var path = SelectFileHelper.OpenFile(initialDirectory: initPath, currentDirectoryForShowingRelativePath: Environment.CurrentDirectory);
                if (path == null) return;
                Value = path;
            });
        }
    }



    public bool IsConfigEqualTo(in AppArgument newValue)
    {
        if (this.Type != newValue.Type) return false;
        if (this.Name != newValue.Name) return false;
        if (this.Key != newValue.Key) return false;
        if (this.IsNullable != newValue.IsNullable) return false;
        if (this.AddBlankAfterKey != newValue.AddBlankAfterKey) return false;
        if (this.AddBlankAfterValue != newValue.AddBlankAfterValue) return false;
        if (this.Selections.Count != Selections.Count) return false;
        if (Type == AppArgumentType.Selection)
        {
            foreach (var selection in Selections)
            {
                if (!newValue.Selections.Contains(selection)) return false;
            }
        }
        else
        {
            foreach (var selection in Selections)
            {
                if (IsNullable == true && selection.Key == "")
                    continue;
                if (!newValue.Selections.Contains(selection)) return false;
            }
        }
        return true;
    }

    public bool IsDefaultValue()
    {
        switch (Type)
        {
            case AppArgumentType.Selection:
                return Value == Selections.FirstOrDefault().Key;
            default:
                return string.IsNullOrWhiteSpace(Value);
        }
    }

    #region IDataErrorInfo

    public static Tuple<bool, string> CheckName(List<string> existedNames, string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Tuple<bool, string>(false, $"`{IoC.Translate(LanguageService.NAME)}` {IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY)}");
        }

        if (existedNames.Any(x => string.Equals(x, name, StringComparison.CurrentCultureIgnoreCase)) == true)
        {
            return new Tuple<bool, string>(false, IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, name));
        }

        return new Tuple<bool, string>(true, "");
    }

    public static Tuple<bool, string> CheckValue(string value, bool isNullable, AppArgumentType type)
    {
        if (value.StartsWith("%") && value.EndsWith("%"))
        {
            return new Tuple<bool, string>(true, "");
        }
        if (string.IsNullOrEmpty(value) && type != AppArgumentType.Selection)
        {
            if (!isNullable && type != AppArgumentType.Const)
                return new Tuple<bool, string>(false, IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY));
            else
                return new Tuple<bool, string>(true, "");
        }
        else
        {
            switch (type)
            {
                case AppArgumentType.File when !File.Exists(value):
                    return new Tuple<bool, string>(false, IoC.Translate("Not existed"));
                case AppArgumentType.Float when !double.TryParse(value, out _):
                    return new Tuple<bool, string>(false, IoC.Translate("Not a number"));
                case AppArgumentType.Int when !long.TryParse(value, out _):
                    return new Tuple<bool, string>(false, IoC.Translate("Not a number"));
            }
        }
        return new Tuple<bool, string>(true, "");
    }

    [JsonIgnore] public string Error => "";

    [JsonIgnore]
    public string this[string columnName]
    {
        get
        {
            switch (columnName)
            {
                case nameof(Value):
                    {
                        var t = CheckValue(Value, IsNullable, Type);
                        if (t.Item1 == false)
                        {
                            return string.IsNullOrEmpty(t.Item2) ? "error" : t.Item2;
                        }
                        break;
                    }
                    //case nameof(DefaultValue):
                    //    {
                    //        var t = CheckValue(DefaultValue, true, Type);
                    //        if (t.Item1 == false)
                    //        {
                    //            return string.IsNullOrEmpty(t.Item2) ? "error" : t.Item2;
                    //        }
                    //        break;
                    //    }
            }
            return "";
        }
    }
    #endregion
}