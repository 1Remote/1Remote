using _1RM.Model.Protocol.Base;
using _1RM.Utils;

namespace _1RM.View.Editor.Forms.AlternativeCredential;

public class AlternativeCredentialListViewModel : NotifyPropertyChangedBaseScreen
{
    public ProtocolBaseWithAddressPort New { get; }
    public AlternativeCredentialListViewModel(ProtocolBaseWithAddressPort protocol)
    {
        New = protocol;
    }
}