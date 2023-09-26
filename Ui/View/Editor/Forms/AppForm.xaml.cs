using System;
using System.Globalization;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace _1RM.View.Editor.Forms
{
    public partial class AppForm : FormBase
    {
        public AppForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
            if (_vm is LocalApp app)
            {
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
                        });
                    }

                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key1",
                        Key = "--key1",
                        Type = AppArgumentType.Normal,
                        Selections = new List<string>() { "ABC", "ZZW1", "CAWE" },
                    });
                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key1.1",
                        Key = "--key1.1",
                        Type = AppArgumentType.Normal,
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
                        Selections = new List<string>() { "ABC", "ZZW1", "CAWE" },
                    });
                    argumentList.Add(new Model.Protocol.AppArgument()
                    {
                        Name = "key5",
                        Key = "--key5",
                        Type = AppArgumentType.Flag,
                    });
                    app.ArgumentList = new ObservableCollection<Model.Protocol.AppArgument>(argumentList);
                }
            }
        }
        public override bool CanSave()
        {
            if (_vm is LocalApp app)
            {
                if (string.IsNullOrEmpty(app.ExePath) == false)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
