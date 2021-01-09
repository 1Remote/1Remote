using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PRM.Core.Model;


namespace PRM.Core.Protocol.RDP
{
    public partial class ProtocolServerRDPAppForm : ProtocolServerFormBase
    {
        public ProtocolServerRDPApp Vm;
        public ProtocolServerRDPAppForm(ProtocolServerBase vm) : base(vm)
        {
            InitializeComponent();
            Vm = (ProtocolServerRDPApp)vm;
            DataContext = vm;
        }
    }
}
