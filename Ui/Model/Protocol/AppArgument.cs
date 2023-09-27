using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _1RM.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.Model.Protocol;

public enum AppArgumentType
{
    Normal,
    Int,
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
}

public class AppArgument : NotifyPropertyChangedBase, ICloneable
{
    public AppArgument(bool? isEditable = true)
    {
        IsEditable = isEditable;
    }

    /// <summary>
    /// todo 批量编辑时，如果参数列表不同，禁用
    /// </summary>
    [JsonIgnore]
    public bool? IsEditable { get; }


    private AppArgumentType _type;
    [JsonConverter(typeof(StringEnumConverter))]
    public AppArgumentType Type
    {
        get => _type;
        set
        {
            if (SetAndNotifyIfChanged(ref _type, value))
            {
                // TODO reset value when type is changed
            }
        }
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
    public string Key
    {
        get => _key.Trim();
        set
        {
            if (SetAndNotifyIfChanged(ref _key, value.Trim()))
                RaisePropertyChanged(nameof(DemoArgumentString));
        }
    }

    private string _value = "";
    public string Value
    {
        get
        {
            if (Type == AppArgumentType.Selection
                && !_selections.Keys.Contains(_value))
            {
                _value = _selections.Keys.FirstOrDefault() ?? "";
            }
            return _value.Trim();
        }
        set
        {
            if (SetAndNotifyIfChanged(ref _value, value.Trim()))
                RaisePropertyChanged(nameof(DemoArgumentString));

            if (Type == AppArgumentType.Selection
                && !_selections.Keys.Contains(_value))
            {
                _value = _selections.Keys.FirstOrDefault() ?? "";
            }
            else if (Type == AppArgumentType.Int
                && !int.TryParse(_value, out _))
            {
                throw new ArgumentException("not a number");
            }
        }
    }

    private string _description = "";
    public string Description
    {
        get => _description.Trim();
        set
        {
            SetAndNotifyIfChanged(ref _description, value.Trim());
            RaisePropertyChanged(nameof(HintDescription));
        }
    }

    [JsonIgnore] public string HintDescription => IsNullable ? "(optional)" + _description : _description;



    private Dictionary<string, string> _selections = new Dictionary<string, string>();
    [JsonIgnore]
    public Dictionary<string, string> Selections
    {
        get => _selections;
        set
        {
            var n = new Dictionary<string, string>();
            var auto = value.Where(x => x.Key.Trim() != "").ToList();
            if (Type == AppArgumentType.Selection)
            {
                if (auto.Any() == false)
                {
                    throw new ArgumentException("TXT: can not be empty");
                }
            }
            if (IsNullable)
            {
                n.Add("", "");
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
                    n.Add(keyValuePair.Key.Trim(), v);
                }
                if (n.All(x => x.Key != Value))
                    Value = n.First().Value;
            }
            _selections = n;
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

    public string GetArgumentString(bool forDemo = false)
    {
        if (Type == AppArgumentType.Flag)
        {
            return Value == "1" ? Key : "";
        }

        if (string.IsNullOrEmpty(Value) && !IsNullable)
        {
            return "";
        }

        var value = Value;
        if (Type == AppArgumentType.Secret)
        {
            if (forDemo)
            {
                value = "******";
            }
            else
            {
                UnSafeStringEncipher.DecryptOrReturnOriginalString(Value);
            }
        }
        if (value.IndexOf(" ", StringComparison.Ordinal) > 0)
            value = $"\"{value}\"";
        if (!string.IsNullOrEmpty(Key))
        {
            value = $"{Key} {value}";
        }
        return value;
    }

    public static string GetArgumentsString(IEnumerable<AppArgument> arguments, bool isDemo)
    {
        string cmd = "";
        foreach (var argument in arguments)
        {
            cmd += argument.GetArgumentString(isDemo) + " ";
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


    public static Tuple<bool, string> CheckName(List<AppArgument> argumentList, string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Tuple<bool, string>(false, $"`{IoC.Get<ILanguageService>().Translate("Name")}` {IoC.Get<ILanguageService>().Translate("Can not be empty!")}");
        }

        if (argumentList?.Any(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase)) == true)
        {
            return new Tuple<bool, string>(false, IoC.Get<ILanguageService>().Translate("XXX is already existed!", name));
        }

        return new Tuple<bool, string>(true, "");
    }

    public static Tuple<bool, string> CheckKey(List<AppArgument> argumentList, string key)
    {
        key = key.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new Tuple<bool, string>(true, "");
        }

        if (argumentList?.Any(x => string.Equals(x.Key, key, StringComparison.CurrentCultureIgnoreCase)) == true)
        {
            return new Tuple<bool, string>(false, IoC.Get<ILanguageService>().Translate("XXX is already existed!", key));
        }

        return new Tuple<bool, string>(true, "");
    }

    public bool IsValueEqualTo(in AppArgument newValue)
    {
        if (this.Type != newValue.Type) return false;
        if (this.Name != newValue.Name) return false;
        if (this.Key != newValue.Key) return false;
        if (this.Value != newValue.Value) return false;
        if (this.Selections.Count != Selections.Count) return false;
        foreach (var selection in Selections)
        {
            if (!newValue.Selections.Contains(selection)) return false;
        }
        return true;
    }
}