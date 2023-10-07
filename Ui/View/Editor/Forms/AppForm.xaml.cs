using System;
using System.Globalization;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using _1RM.Utils;
using Google.Protobuf.WellKnownTypes;

namespace _1RM.View.Editor.Forms
{
    public partial class AppForm : FormBase
    {
        public AppForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            if (_vm is LocalApp app)
            {
                app.PropertyChanged += AppOnPropertyChanged;
            }
        }

        ~AppForm()
        {
            if (_vm is LocalApp app)
            {
                app.PropertyChanged -= AppOnPropertyChanged;
            }
        }

        private void AppOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is LocalApp app && e.PropertyName == nameof(LocalApp.ExePath))
            {
                var t = AppArgumentHelper.GetPresetArgumentList(app.ExePath);
                if (t != null)
                {
                    bool same = app.ArgumentList.Count == t.Item2.Count;
                    if (same)
                        for (int i = 0; i < app.ArgumentList.Count; i++)
                        {
                            if (!app.ArgumentList[i].IsConfigEqualTo(t.Item2[i]))
                            {
                                same = false;
                                break;
                            }
                        }
                    if (!same && (app.ArgumentList.All(x => x.IsDefaultValue())
                                  || MessageBoxHelper.Confirm("TXT: 用适配 path 的参数覆盖当前参数列表？")))
                    {
                        app.RunWithHosting = t.Item1;
                        app.ArgumentList = new ObservableCollection<AppArgument>(t.Item2);
                    }
                }
            }
        }

        public override bool CanSave()
        {
            if (_vm is LocalApp app)
            {
                if (!app.Verify())
                    return false;
                if (string.IsNullOrEmpty(app.ExePath))
                    return false;
            }
            return true;
        }
    }
}
