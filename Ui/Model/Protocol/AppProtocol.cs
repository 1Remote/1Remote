using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.FileSystem;
using _1RM.View;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Shapes;
using System.Collections.Generic;

namespace _1RM.Model.Protocol
{
    // TODO 改为 ProtocolBaseWithAddressPortUserPwd 并且用 %1RM_PASSWORD% 替代默认密码
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


        private string _appSubTitle = "";
        public string AppSubTitle
        {
            get => _appSubTitle;
            set => SetAndNotifyIfChanged(ref _appSubTitle, value);
        }

        private string _exePath = "";
        public string ExePath
        {
            get => _exePath;
            set => SetAndNotifyIfChanged(ref _exePath, value);
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
            return string.IsNullOrEmpty(AppSubTitle) ? $"{this.ExePath}" : AppSubTitle;
        }

        public override double GetListOrder()
        {
            return 100;
        }

        public string GetArguments()
        {
            return AppArgument.GetArgumentsString(ArgumentList, false, this);
        }


        public string GetDemoArguments()
        {
            return AppArgument.GetArgumentsString(ArgumentList, true, this);
        }

        public override bool Verify()
        {
            return ArgumentList.All(argument => string.IsNullOrEmpty(argument[nameof(AppArgument.Value)]));
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
    }
}
