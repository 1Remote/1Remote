using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MSTSCLib;

namespace Shawn.Utils.RDP
{
    public static class AxMsRdpClient9NotSafeForScriptingExAdd
    {
        public static void SetExtendedProperty(this AxHost axHost, string propertyName, object value)
        {
            try
            {
                ((IMsRdpExtendedSettings)axHost.GetOcx()).set_Property(propertyName, ref value);
            }
            catch (Exception ee)
            {
                SimpleLogHelper.Error(ee);
            }
        }
    }
    public class AxMsRdpClient9NotSafeForScriptingEx : AxMSTSCLib.AxMsRdpClient9NotSafeForScripting
    {
        public AxMsRdpClient9NotSafeForScriptingEx() : base()
        {
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            // Fix for the missing focus issue on the rdp client component
            if (m.Msg == 0x0021) // WM_MOUSEACTIVATE
            {
                if (!this.ContainsFocus)
                {
                    this.Focus();
                }
            }
            base.WndProc(ref m);
        }
    }
}
