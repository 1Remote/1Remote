using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Utils;

namespace _1RM.View.Editor.Forms.Utils;

public class HostViewModel : NotifyPropertyChangedBaseScreen
{
    public ProtocolBaseWithAddressPort New { get; }
    public HostViewModel(ProtocolBaseWithAddressPort protocol)
    {
        New = protocol;
    }
}