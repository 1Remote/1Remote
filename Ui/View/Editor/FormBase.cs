using System.Windows.Controls;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;

namespace _1RM.View.Editor
{
    public abstract class FormBase : UserControl
    {
        protected FormBase()
        {
        }
        /// <summary>
        /// validate whether all fields are correct to save
        /// </summary>
        /// <returns></returns>
        public virtual bool CanSave()
        {
            return true;
            //// TODO 重构
            //if (_vm.GetType().IsSubclassOf(typeof(ProtocolBaseWithAddressPort)))
            //{
            //    var protocol = (ProtocolBaseWithAddressPort)_vm;
            //    if (string.IsNullOrEmpty(protocol.Address?.Trim()))
            //        return false;
            //    if (protocol.GetPort() <= 0 || protocol.GetPort() >= 65536)
            //        return false;
            //}


            //if (!_vm.Verify())
            //    return false;

            //return true;
        }
    }
}
