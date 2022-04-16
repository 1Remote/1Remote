using System.Windows.Controls;
using PRM.Model.Protocol.Base;

namespace PRM.View.Editor
{
    public abstract class FormBase : UserControl
    {
        protected readonly ProtocolBase _vm = null!;


        protected FormBase(ProtocolBase protocol)
        {
            _vm = protocol;
            DataContext = protocol;
        }
        /// <summary>
        /// validate whether all fields are correct to save
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSave()
        {
            if (_vm.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)))
            {
                var protocol = (ProtocolBaseWithAddressPort)_vm;
                if (!string.IsNullOrEmpty(protocol.Address?.Trim())
                    && protocol.GetPort() > 0 && protocol.GetPort() < 65536)
                    return true;
                return false;
            }
            if (_vm.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPortUserPwd)))
            {
                var protocol = (ProtocolBaseWithAddressPortUserPwd)_vm;
                if (!string.IsNullOrEmpty(protocol.UserName?.Trim())
                    && protocol.GetPort() > 0 && protocol.GetPort() < 65536)
                    return true;
                return false;
            }
            return false;
        }
    }
}
