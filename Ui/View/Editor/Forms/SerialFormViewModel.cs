using _1RM.Model.Protocol;

namespace _1RM.View.Editor.Forms;

public class SerialFormViewModel : ProtocolBaseFormViewModel
{
    public new Serial New { get; }
    public SerialFormViewModel(Serial protocolBase) : base(protocolBase)
    {
        New = protocolBase;
    }

    public override bool CanSave()
    {
        if (!string.IsNullOrEmpty(New[nameof(New.SerialPort)]))
            return false;
        if (!string.IsNullOrEmpty(New[nameof(New.BitRate)]))
            return false;
        return base.CanSave();
    }
}