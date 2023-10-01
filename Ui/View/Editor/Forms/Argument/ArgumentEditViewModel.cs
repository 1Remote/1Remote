using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using Newtonsoft.Json.Converters;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;

namespace _1RM.View.Editor.Forms.Argument
{
    public class ArgumentEditViewModel : NotifyPropertyChangedBaseScreen
    {
        private readonly LocalApp _localApp;
        private readonly Model.Protocol.AppArgument? _org = null;
        private readonly List<Model.Protocol.AppArgument> _existedArguments;
        public Model.Protocol.AppArgument New { get; } = new Model.Protocol.AppArgument();
        public List<Model.Protocol.AppArgumentType> ArgumentTypes { get; } = new List<AppArgumentType>();
        public ArgumentEditViewModel(LocalApp localApp, List<Model.Protocol.AppArgument> existedArguments, Model.Protocol.AppArgument? org = null)
        {
            _localApp = localApp;
            _org = org;
            _existedArguments = new List<Model.Protocol.AppArgument>(existedArguments);
            foreach (AppArgumentType value in Enum.GetValues(typeof(AppArgumentType)))
            {
                ArgumentTypes.Add(value);
            }

            if (_org != null && _existedArguments.Contains(_org))
                _existedArguments.Remove(_org);

            // Edit mode
            if (_org != null)
            {
                New = (Model.Protocol.AppArgument)_org.Clone();
            }

            Selections = string.Join('\n', New.Selections);
        }

        public AppArgumentType Type
        {
            get => New.Type;
            set
            {
                if (New.Type != value)
                {
                    New.Type = value;
                    RaisePropertyChanged();

                    // TODO set visibility
                    SelectionsVisibility = Visibility.Collapsed;
                    switch (value)
                    {
                        case AppArgumentType.Normal:
                            SelectionsVisibility = Visibility.Visible;
                            break;
                        case AppArgumentType.File:
                            break;
                        case AppArgumentType.Secret:
                            break;
                        case AppArgumentType.Flag:
                            break;
                        case AppArgumentType.Selection:
                            SelectionsVisibility = Visibility.Visible;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(value), value, null);
                    }

                    RaisePropertyChanged(nameof(SelectionsVisibility));
                }
            }
        }

        public Visibility SelectionsVisibility { get; set; } = Visibility.Collapsed;


        public string Name
        {
            get => New.Name.Trim();
            set
            {
                if (New.Name != value)
                {
                    New.Name = value.Trim();
                    RaisePropertyChanged();
                    var t = Model.Protocol.AppArgument.CheckName(_existedArguments, value.Trim());
                    if (t.Item1 == false)
                    {
                        // TODO 改为 IDataErrorInfo 实现
                        throw new ArgumentException(t.Item2);
                    }
                }
            }
        }

        public string Key
        {
            get => New.Key.Trim();
            set
            {
                if (New.Key != value)
                {
                    New.Key = value.Trim();
                    RaisePropertyChanged();
                    var t = Model.Protocol.AppArgument.CheckKey(_existedArguments, value.Trim());
                    if (t.Item1 == false)
                    {
                        throw new ArgumentException(t.Item2);
                    }
                }
            }
        }


        private string _selections = "";
        public string Selections
        {
            get => _selections;
            set
            {
                if (SetAndNotifyIfChanged(ref _selections, value))
                {
                    var t = CheckSelections(value.Trim());
                    if (t.Item1 == false)
                    {
                        throw new ArgumentException(t.Item2);
                    }
                }
            }
        }

        private Tuple<bool, string> CheckSelections(string selections)
        {
            if (Type == AppArgumentType.Selection)
            {
                if (selections.Any(x => x != ' ' && x != '\r' && x != '\n' && x != '\t'))
                {

                }
                else
                {
                    return new Tuple<bool, string>(false, $"`{IoC.Get<ILanguageService>().Translate("TXT: Selections")}` {IoC.Get<ILanguageService>().Translate(LanguageService.CAN_NOT_BE_EMPTY)}");
                }
            }

            return new Tuple<bool, string>(true, "");
        }


        private RelayCommand? _cmdSave;
        public RelayCommand CmdSave
        {
            get
            {
                return _cmdSave ??= new RelayCommand((_) =>
                {
                    {
                        var t = Model.Protocol.AppArgument.CheckName(_existedArguments, Name);
                        if (t.Item1 == false)
                        {
                            MessageBoxHelper.Warning(t.Item2);
                            return;
                        }
                    }

                    {
                        var t = Model.Protocol.AppArgument.CheckKey(_existedArguments, Key);
                        if (t.Item1 == false)
                        {
                            MessageBoxHelper.Warning(t.Item2);
                            return;
                        }
                    }

                    {
                        var t = CheckSelections(Selections);
                        if (t.Item1 == false)
                        {
                            MessageBoxHelper.Warning(t.Item2);
                            return;
                        }
                    }


                    {
                        var dictionary = new Dictionary<string, string>();
                        var strReader = new StringReader(Selections);
                        while (true)
                        {
                            var line = strReader.ReadLine();
                            if (line != null)
                            {
                                if (line.Split('|').Length == 2)
                                {
                                    var items = line.Split('|');
                                    if (!dictionary.ContainsKey(items[0].Trim()))
                                        dictionary.Add(items[0].Trim(), items[1].Trim());
                                    else
                                        dictionary[items[0].Trim()] = items[1].Trim();
                                }
                                else
                                {
                                    if (!dictionary.ContainsKey(line.Trim()))
                                        dictionary.Add(line.Trim(), line.Trim());
                                    else
                                        dictionary[line.Trim()] = line.Trim();
                                }
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
                        _localApp.ArgumentList.Remove(_org);
                        _localApp.ArgumentList.Insert(i, New);
                    }
                    else
                    {
                        // add
                        _localApp.ArgumentList.Add(New);
                    }
                    RequestClose(true);
                }, o => Model.Protocol.AppArgument.CheckName(_existedArguments, Name).Item1 && Model.Protocol.AppArgument.CheckKey(_existedArguments, Key).Item1 && CheckSelections(Selections).Item1);
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
    }
}
