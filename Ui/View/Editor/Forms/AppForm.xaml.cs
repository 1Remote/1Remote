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
#if DEBUG
                if (app.ArgumentList.Count == 0)
                {
                    var argumentList = new List<Model.Protocol.AppArgument>();
                    if (!string.IsNullOrWhiteSpace(app.Arguments))
                    {
                        argumentList.Add(new Model.Protocol.AppArgument()
                        {
                            Name = "org",
                            Key = "",
                            Type = AppArgumentType.Normal,
                            Value = app.Arguments,
                            IsNullable = false
                        });
                    }

                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key1",
                        Key = "--key1",
                        Type = AppArgumentType.Normal,
                        Selections = new Dictionary<string, string>() { { "ABC", "" }, { "ZZW1", "" }, { "CAWE", "" } },
                    });
                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key1.1",
                        Key = "--key1.1",
                        Type = AppArgumentType.Int,
                        IsNullable = false,
                    });
                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key2",
                        Key = "--key2",
                        Type = AppArgumentType.Secret,
                    });
                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key3",
                        Key = "--key3",
                        Type = AppArgumentType.File,
                    });
                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key4",
                        Key = "--key4",
                        Type = AppArgumentType.Selection,
                        Selections = new Dictionary<string, string>() { { "ABC", "模式1" }, { "ZZW1", "模式2" }, { "CAWE", "模式3" } },
                    });
                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key5",
                        Key = "--key5",
                        Type = AppArgumentType.Flag,
                    });
                    app.ArgumentList = new ObservableCollection<Model.Protocol.AppArgument>(argumentList);
                }
#endif
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
