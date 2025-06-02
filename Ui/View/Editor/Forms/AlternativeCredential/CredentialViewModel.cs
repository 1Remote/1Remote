using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using MySqlX.Protocol;
using System.Collections.Generic;

namespace _1RM.View.Editor.Forms.AlternativeCredential;

public class CredentialViewModel : NotifyPropertyChangedBaseScreen
{
    public ProtocolBaseWithAddressPort New { get; }
    public List<ProtocolBaseWithAddressPortUserPwd> InheritableProtocols { get; }
    public CredentialViewModel(ProtocolBaseWithAddressPort protocol)
    {
        New = protocol;
        var list = new List<ProtocolBaseWithAddressPortUserPwd>();
        foreach (var vm in IoC.Get<GlobalData>().VmItemList)
        {
            if (vm.Server is ProtocolBaseWithAddressPortUserPwd { IsUserPswInheritedFromElseWhere: false } p && p.Id != protocol.Id)
            {
                list.Add(p);
            }
        }
        InheritableProtocols = list;
        RaisePropertyChanged(nameof(InheritableProtocols));
    }
}