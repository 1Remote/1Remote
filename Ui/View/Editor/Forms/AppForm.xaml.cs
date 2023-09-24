using System;
using System.Globalization;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Data;

namespace _1RM.View.Editor.Forms
{
    public partial class AppForm : FormBase
    {
        public AppForm(ProtocolBase vm) : base(vm)
        {
            InitializeComponent();
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
