using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using _1RM.Model.Protocol;
using _1RM.Service;
using _1RM.Utils;
using Google.Protobuf.WellKnownTypes;
using ICSharpCode.AvalonEdit.Editing;
using Newtonsoft.Json;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Enum = System.Enum;

namespace _1RM.View.Editor.Forms
{
    public class AppFormViewModel : NotifyPropertyChangedBaseScreen, IDataErrorInfo
    {
        private readonly LocalApp _localApp;
        public LocalApp New => _localApp;
        public AppFormViewModel(LocalApp localApp)
        {
            _localApp = localApp;
            CheckHPUP();
        }

        public Visibility SelectionsVisibility { get; set; } = Visibility.Collapsed;
        public bool IsConst { get; set; } = false;

        public string Address
        {
            get => New.Address;
            set
            {
                var v = value;
                if (!RequiredHostName)
                    v = "";
                else if (New.Address != v)
                {
                    New.Address = v;
                    RaisePropertyChanged();
                }
            }
        }

        public string Port
        {
            get => New.Port;
            set
            {
                var v = value;
                if (!RequiredPort)
                    v = "";
                else if (New.Port != v)
                {
                    New.Port = v;
                    RaisePropertyChanged();
                }
            }
        }

        public string UserName
        {
            get => New.UserName;
            set
            {
                var v = value;
                if (!RequiredUserName)
                    v = "";
                else if (New.UserName != v)
                {
                    New.UserName = v;
                    RaisePropertyChanged();
                }
            }
        }
        public string Password
        {
            get => New.Password;
            set
            {
                var v = value;
                if (!RequiredPassword)
                    v = "";
                else if (New.Password != v)
                {
                    New.Password = v;
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


        #region IDataErrorInfo
        public bool RequiredHostName { get; set; } = false;
        public bool RequiredPort { get; set; } = false;
        public bool RequiredUserName { get; set; } = false;
        public bool RequiredPassword { get; set; } = false;
        public void CheckHPUP()
        {
            RequiredHostName = false;
            RequiredPort = false;
            RequiredUserName = false;
            RequiredPassword = false;
            foreach (var argument in New.ArgumentList)
            {
                if (!RequiredHostName && argument.Value.IndexOf("%HOSTNAME%", StringComparison.Ordinal) >= 0)
                {
                    RequiredHostName = true;
                }
                if (!RequiredPort && argument.Value.IndexOf("%PORT%", StringComparison.Ordinal) >= 0)
                {
                    RequiredPort = true;
                }
                if (!RequiredUserName && argument.Value.IndexOf("%USERNAME%", StringComparison.Ordinal) >= 0)
                {
                    RequiredUserName = true;
                }
                if (!RequiredPassword && argument.Value.IndexOf("%PASSWORD%", StringComparison.Ordinal) >= 0)
                {
                    RequiredPassword = true;
                }
            }

            if (RequiredHostName) Address = "";
            if (RequiredPort) Port = "";
            if (RequiredUserName) UserName = "";
            if (RequiredPassword) Address = "";
            RaisePropertyChanged(nameof(RequiredHostName));
            RaisePropertyChanged(nameof(RequiredPort));
            RaisePropertyChanged(nameof(RequiredUserName));
            RaisePropertyChanged(nameof(RequiredPassword));
        }

        [JsonIgnore] public string Error => "";

        [JsonIgnore]
        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Address):
                        {
                            if (RequiredHostName && string.IsNullOrWhiteSpace(Address))
                            {
                                return $"`{IoC.Get<ILanguageService>().Translate("Hostname")}` {IoC.Get<ILanguageService>().Translate(LanguageService.CAN_NOT_BE_EMPTY)}";
                            }
                            break;
                        }
                    case nameof(Port):
                        {
                            if (RequiredPort)
                            {
                                if (string.IsNullOrWhiteSpace(Port))
                                    return $"`{IoC.Get<ILanguageService>().Translate("Port")}` {IoC.Get<ILanguageService>().Translate(LanguageService.CAN_NOT_BE_EMPTY)}";
                                if (long.TryParse(Port, out _))
                                    return "TXT: not a number";
                            }

                            break;
                        }
                    case nameof(UserName):
                        {
                            if (RequiredUserName && string.IsNullOrWhiteSpace(UserName))
                            {
                                return $"`{IoC.Get<ILanguageService>().Translate("User")}` {IoC.Get<ILanguageService>().Translate(LanguageService.CAN_NOT_BE_EMPTY)}";
                            }
                            break;
                        }
                    case nameof(Password):
                        {
                            if (RequiredPassword && string.IsNullOrWhiteSpace(Password))
                            {
                                return $"`{IoC.Get<ILanguageService>().Translate("Password")}` {IoC.Get<ILanguageService>().Translate(LanguageService.CAN_NOT_BE_EMPTY)}";
                            }
                            break;
                        }
                        //default:
                        //    {
                        //        return New[columnName];
                        //    }
                }
                return "";
            }
        }
        #endregion
    }
}
