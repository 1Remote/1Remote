using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PRM.Core.Protocol
{
    public abstract class ProtocolServerFormBase : UserControl
    {
        private ProtocolServerBase _vm = null;
        public ProtocolServerFormBase(ProtocolServerBase protocol)
        {
            _vm = protocol;
        }
        /// <summary>
        /// validate whether all fields are correct to save
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSave()
        {
            if (_vm.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortBase)))
            {
                var protocol = (ProtocolServerWithAddrPortBase)_vm;
                if (!string.IsNullOrEmpty(protocol.Address?.Trim())
                    && protocol.GetPort() > 0 && protocol.GetPort() < 65536)
                    return true;
                return false;
            }
            if (_vm.GetType().IsSubclassOf(typeof(ProtocolServerWithAddrPortUserPwdBase)))
            {
                var protocol = (ProtocolServerWithAddrPortUserPwdBase)_vm;
                if (!string.IsNullOrEmpty(protocol.UserName?.Trim())
                    && protocol.GetPort() > 0 && protocol.GetPort() < 65536)
                    return true;
                return false;
            }
            return false;
        }
    }
}
