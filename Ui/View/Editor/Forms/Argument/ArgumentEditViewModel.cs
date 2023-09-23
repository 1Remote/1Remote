using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
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
        private readonly Model.Protocol.Argument? _org = null;
        private readonly List<Model.Protocol.Argument> _existedArguments;
        public Model.Protocol.Argument New { get; } = new Model.Protocol.Argument();
        public List<Model.Protocol.ArgumentType> ArgumentTypes { get; } = new List<ArgumentType>();
        public ArgumentEditViewModel(LocalApp localApp, List<Model.Protocol.Argument> existedArguments, Model.Protocol.Argument? org = null)
        {
            _localApp = localApp;
            _org = org;
            _existedArguments = new List<Model.Protocol.Argument>(existedArguments);
            foreach (ArgumentType value in Enum.GetValues(typeof(ArgumentType)))
            {
                ArgumentTypes.Add(value);
            }

            if (_org != null && _existedArguments.Contains(_org))
                _existedArguments.Remove(_org);

            // Edit mode
            if (_org != null)
            {
                New = (Model.Protocol.Argument)_org.Clone();
            }
        }

        public ArgumentType Type
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
                        case ArgumentType.Normal:
                            break;
                        case ArgumentType.File:
                            break;
                        case ArgumentType.Secret:
                            break;
                        case ArgumentType.Flag:
                            break;
                        case ArgumentType.Selection:
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
                    var t = Model.Protocol.Argument.CheckName(_existedArguments, value.Trim());
                    if (t.Item1 == false)
                    {
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
                    var t = Model.Protocol.Argument.CheckKey(_existedArguments, value.Trim());
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
            if (Type == ArgumentType.Selection)
            {
                if (selections.Any(x => x != ' ' && x != '\r' && x != '\n' && x != '\t'))
                {

                }
                else
                {
                    return new Tuple<bool, string>(false, $"`{IoC.Get<ILanguageService>().Translate("TXT: Selections")}` {IoC.Get<ILanguageService>().Translate("Can not be empty!")}");
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
                        var t = Model.Protocol.Argument.CheckName(_existedArguments, Name);
                        if (t.Item1 == false)
                        {
                            MessageBoxHelper.Warning(t.Item2);
                            return;
                        }
                    }

                    {
                        var t = Model.Protocol.Argument.CheckKey(_existedArguments, Key);
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
                        var list = new List<string>();
                        var strReader = new StringReader(Selections);
                        while (true)
                        {
                            var line = strReader.ReadLine();
                            if (line != null)
                            {
                                list.Add(line.Trim());
                            }
                            else
                            {
                                break;
                            }
                        }
                        New.Selections = list.Distinct().ToList();
                        if (Type == ArgumentType.Selection && New.Selections.Count == 0)
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
                }, o => Model.Protocol.Argument.CheckName(_existedArguments, Name).Item1 && Model.Protocol.Argument.CheckKey(_existedArguments, Key).Item1 && CheckSelections(Selections).Item1);
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
