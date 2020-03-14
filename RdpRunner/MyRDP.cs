namespace RdpRunner
{
    public class MyRDP : AxMSTSCLib.AxMsRdpClient2NotSafeForScripting
    {
        public MyRDP() : base()
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
