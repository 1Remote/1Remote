using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using PRM.Core.Protocol.RDP;


namespace PRM.Core.Protocol.VNC
{
    public partial class ProtocolServerVNCForm : ProtocolServerFormBase
    {
        public readonly ProtocolServerBase Vm;
        public ProtocolServerVNCForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = vm;
        }
    }


    public class ConverterEVncWindowResizeMode : IValueConverter
    {
        #region IValueConverter 成员  
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((int)((ProtocolServerVNC.EVncWindowResizeMode)value)).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (ProtocolServerVNC.EVncWindowResizeMode)(int.Parse(value.ToString()));
        }
        #endregion
    }

}
