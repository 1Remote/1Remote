using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Utils.MaskAndPop;
using Newtonsoft.Json;
using Shawn.Utils.Wpf;
using Enum = System.Enum;

namespace _1RM.View.Editor.Forms.Argument
{
    public class ArgumentEditViewModel : PopupBase, IDataErrorInfo
    {
        private readonly LocalApp _localApp;
        private readonly AppArgument? _org = null;
        private readonly List<string> _existedArguments;
        public AppArgument New { get; } = new AppArgument();
        public List<AppArgumentType> ArgumentTypes { get; } = new List<AppArgumentType>();
        public ArgumentEditViewModel(LocalApp localApp, List<string> existedArguments, AppArgument? org = null)
        {
            _localApp = localApp;
            _org = org;
            _existedArguments = new List<string>(existedArguments);
            foreach (AppArgumentType value in Enum.GetValues(typeof(AppArgumentType)))
            {
                ArgumentTypes.Add(value);
            }

            if (_org != null && _existedArguments.Contains(_org.Name))
                _existedArguments.Remove(_org.Name);

            // Edit mode
            if (_org != null)
            {
                New = (AppArgument)_org.Clone();
            }

            Type = New.Type;
            var diff = ProtocolBase.ServerEditorStaticDifferentOptions;
            if (Type == AppArgumentType.Selection)
            {
                var ss = New.Selections.Where(x => x.Key != diff).Select(x => string.IsNullOrEmpty(x.Value) ? x.Key : x.Key + "|" + x.Value).Where(x => !string.IsNullOrWhiteSpace(x));
                Selections = string.Join("\n", ss);
            }
            else
            {
                var ss = New.Selections.Where(x => x.Key != diff).Select(x => x.Key).Where(x => !string.IsNullOrWhiteSpace(x));
                Selections = string.Join("\n", ss);
            }
        }

        public AppArgumentType Type
        {
            get => New.Type;
            set
            {
                New.Type = value;
                RaisePropertyChanged();

                SelectionsVisibility = Visibility.Collapsed;
                IsNullableVisibility = Visibility.Visible;
                IsConst = false;
                switch (value)
                {
                    case AppArgumentType.Const:
                        SelectionsVisibility = Visibility.Collapsed;
                        IsConst = true;
                        break;
                    case AppArgumentType.Normal:
                        SelectionsVisibility = Visibility.Visible;
                        break;
                    case AppArgumentType.Selection:
                        SelectionsVisibility = Visibility.Visible;
                        break;
                    case AppArgumentType.Flag:
                        New.IsNullable = false;
                        IsNullableVisibility = Visibility.Collapsed;
                        break;
                    case AppArgumentType.Int:
                    case AppArgumentType.Float:
                    case AppArgumentType.File:
                    case AppArgumentType.Secret:
                    default:
                        break;
                }

                RaisePropertyChanged(nameof(Value));
                RaisePropertyChanged(nameof(SelectionsVisibility));
                RaisePropertyChanged(nameof(IsNullableVisibility));
                RaisePropertyChanged(nameof(SelectionsTag));
                RaisePropertyChanged(nameof(Key));
                RaisePropertyChanged(nameof(IsConst));
            }
        }

        public Visibility SelectionsVisibility { get; set; } = Visibility.Collapsed;
        public Visibility IsNullableVisibility { get; set; } = Visibility.Visible;
        public bool IsConst { get; set; } = false;

        public string Name
        {
            get => New.Name;
            set
            {
                if (New.Name != value)
                {
                    New.Name = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string Key
        {
            get => New.Key;
            set
            {
                if (New.Key != value)
                {
                    New.Key = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string Value
        {
            get => New.Value;
            set
            {
                if (New.Value != value)
                {
                    New.Value = value;
                    RaisePropertyChanged();
                }
            }
        }


        private string _selections = "";
        public string Selections
        {
            get => _selections;
            set => SetAndNotifyIfChanged(ref _selections, value);
        }

        public string SelectionsTag => Type == AppArgumentType.Selection
            ? IoC.Translate("One option per line") + ". " + IoC.Translate("Use vertical lines to separate descriptions") + "\r\ne.g. \r\n1|Yes\r\n0|No"
            : IoC.Translate("One option per line") + ". " + "\r\ne.g. \r\nApple\r\nBanana";


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((_) =>
                {
                    {
                        Name = Name.Trim();
                        var t = AppArgument.CheckName(_existedArguments, Name);
                        if (t.Item1 == false)
                        {
                            MessageBoxHelper.Warning(t.Item2);
                            return;
                        }
                    }

                    //{
                    //    var t = AppArgument.CheckValue(New.Value, New.IsNullable, New.Type);
                    //    if (t.Item1 == false)
                    //    {
                    //        MessageBoxHelper.Warning(t.Item2);
                    //        return;
                    //    }
                    //}

                    {
                        var t = CheckSelections(Selections);
                        if (t.Item1 == false)
                        {
                            MessageBoxHelper.Warning(t.Item2);
                            return;
                        }
                    }

                    New.Selections = new Dictionary<string, string>();
                    if (Type != AppArgumentType.Const)
                    {
                        var dictionary = new Dictionary<string, string>();
                        var strReader = new StringReader(Selections);
                        while (true)
                        {
                            var line = strReader.ReadLine();
                            if (line != null)
                            {
                                string key = "";
                                string value = "";
                                if (line.Split('|').Length == 2)
                                {
                                    var items = line.Split('|');
                                    key = items[0];
                                    value = items[1];
                                }
                                else
                                {
                                    key = line;
                                    value = line;
                                }

                                if (key == ProtocolBase.ServerEditorStaticDifferentOptions)
                                {
                                    continue;
                                }

                                if (string.IsNullOrEmpty(key) && !New.IsNullable)
                                {
                                    continue;
                                }

                                if (!dictionary.ContainsKey(key))
                                    dictionary.Add(key, value);
                                else
                                    dictionary[key] = value;
                            }
                            else
                            {
                                break;
                            }
                        }

                        New.Selections = dictionary;
                        if (Type == AppArgumentType.Selection && New.Selections.Count == 0)
                        {
                            return;
                        }
                    }


                    if (_org != null && _localApp.ArgumentList.Any(x => x.Equals(_org)))
                    {
                        // edit
                        var i = _localApp.ArgumentList.IndexOf(_org);
                        _localApp.ArgumentList[i] = New;
                    }
                    else
                    {
                        // add
                        _localApp.ArgumentList.Add(New);
                    }

                    RequestClose(true);
                }, o => AppArgument.CheckName(_existedArguments, Name).Item1
                        && CheckSelections(Selections).Item1
                        && (New.Type != AppArgumentType.Const || AppArgument.CheckValue(New.Value, New.IsNullable, New.Type).Item1));
            }
        }



        private RelayCommand? _cmdCancel;
        public RelayCommand CmdCancel
        {
            get
            {
                return _cmdCancel ??= new RelayCommand((o) =>
                {
                    RequestClose(false);
                });
            }
        }



        #region IDataErrorInfo

        private Tuple<bool, string> CheckSelections(string selections)
        {
            if (Type == AppArgumentType.Selection)
            {
                if (selections.Any(x => x != ' ' && x != '\r' && x != '\n' && x != '\t'))
                {

                }
                else
                {
                    return new Tuple<bool, string>(false, $"`{IoC.Translate("Selections")}` {IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY)}");
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
                    case nameof(Name):
                        {
                            var t = AppArgument.CheckName(_existedArguments, Name);
                            if (t.Item1 == false)
                            {
                                return t.Item2;
                            }
                            break;
                        }
                    case nameof(Selections):
                        {
                            var t = CheckSelections(Selections);
                            if (t.Item1 == false)
                            {
                                return t.Item2;
                            }
                            break;
                        }
                    case nameof(Key):
                        {
                            if (Type == AppArgumentType.Flag && string.IsNullOrEmpty(Key))
                            {
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            }
                            break;
                        }
                    case nameof(Value):
                        {
                            if (New.Type == AppArgumentType.Const)
                            {
                                var t = AppArgument.CheckValue(Value, true, New.Type);
                                if (t.Item1 == false)
                                {
                                    return t.Item2;
                                }
                            }
                            break;
                        }
                    default:
                        {
                            return New[columnName];
                        }
                }
                return "";
            }
        }
        #endregion
    }
}
