using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using Shawn.Utils;
using System.Collections.ObjectModel;
using System.Linq;
using _1RM.Service;

namespace _1RM.Model.Protocol
{
    public class LocalApp : ProtocolBaseWithAddressPortUserPwd
    {
        public LocalApp() : base("APP", "APP.V1", "APP")
        {
            base.Address = "";
            base.Port = "";
            base.UserName = "";
            base.Password = "";
            base.PrivateKey = "";
            base.IsPingBeforeConnect = false;
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        private string _exePath = "";
        public string ExePath
        {
            get => _exePath;
            set
            {
                if (SetAndNotifyIfChanged(ref _exePath, value))
                    RaisePropertyChanged(nameof(SubTitle));
            }
        }

        private string _appProtocolDisplayName = "";
        public string AppProtocolDisplayName
        {
            get => _appProtocolDisplayName;
            set => SetAndNotifyIfChanged(ref _appProtocolDisplayName, value);
        }

        public override string GetProtocolDisplayName()
        {
            if (string.IsNullOrEmpty(_appProtocolDisplayName))
                return base.GetProtocolDisplayName();
            return _appProtocolDisplayName;
        }

        [Obsolete]
        private string _arguments = "";
        [Obsolete]
        public string Arguments
        {
            get => _arguments;
            set => SetAndNotifyIfChanged(ref _arguments, value);
        }

        private ObservableCollection<AppArgument> _argumentList = new ObservableCollection<AppArgument>();
        public ObservableCollection<AppArgument> ArgumentList
        {
            get => _argumentList;
            set => SetAndNotifyIfChanged(ref _argumentList, value);
        }


        private bool _runWithHosting = false;
        public bool RunWithHosting
        {
            get => _runWithHosting;
            set => SetAndNotifyIfChanged(ref _runWithHosting, value);
        }


        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var app = JsonConvert.DeserializeObject<LocalApp>(jsonString);
                return app;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }


        protected override string GetSubTitle()
        {
            if (!string.IsNullOrEmpty(Address))
            {
                if (!string.IsNullOrEmpty(Port))
                {
                    return string.IsNullOrEmpty(UserName) ? $"{Address}:{Port}" : $"{Address}:{Port}({UserName})";
                }
                return Address;
            }
            return System.IO.Path.GetFileName(ExePath) + " " + GetArguments(true);
        }

        public override double GetListOrder()
        {
            return 100;
        }

        public string GetExePath()
        {
            var path = OtherNameAttributeExtensions.Replace(this, ExePath);
            path = Environment.ExpandEnvironmentVariables(path);
            return path;
        }

        public string GetArguments(bool isDemo)
        {
            return AppArgument.GetArgumentsString(ArgumentList, isDemo, this);
        }

        public override ProtocolBase Clone()
        {
            var clone = base.Clone() as LocalApp;
            clone!.ArgumentList = new ObservableCollection<AppArgument>(ArgumentList.Select(x => x.Clone() as AppArgument)!);
            return clone;
        }



        public override bool ShowAddressInput()
        {
            foreach (var argument in ArgumentList)
                if (argument.Value.IndexOf(MACRO_HOST_NAME, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            return false;
        }


        public override bool ShowPortInput()
        {
            foreach (var argument in ArgumentList)
                if (argument.Value.IndexOf(MACRO_PORT, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            return false;
        }


        public override bool ShowUserNameInput()
        {
            foreach (var argument in ArgumentList)
                if (argument.Value.IndexOf(MACRO_USERNAME, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            return false;
        }

        public override bool ShowPasswordInput()
        {
            foreach (var argument in ArgumentList)
                if (argument.Value.IndexOf(MACRO_PASSWORD, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            return false;
        }

        public override bool ShowPrivateKeyInput()
        {
            return true;
        }


        #region IDataErrorInfo
        [JsonIgnore]
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(ExePath):
                        {
                            if (string.IsNullOrWhiteSpace(ExePath))
                            {
                                return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                            }
                            break;
                        }
                    default:
                        return base[columnName];
                }
                return "";
            }
        }
        #endregion

        public override string GetHelpUrl()
        {
            return "https://1remote.org/usage/protocol/especial/app/";
        }
    }
}
