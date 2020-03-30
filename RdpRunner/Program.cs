using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxMSTSCLib;
using Newtonsoft.Json;
using Shawn.Ulits.RDP;

namespace RdpRunner
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length > 0)
                {
                    var config = JsonConvert.DeserializeObject<ServerRDP>(Encoding.UTF8.GetString(Convert.FromBase64String(args[0])));
                    Application.Run(new RDPForm(config));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                MessageBox.Show(e.Message);
            }
        }

        public static void ShowMsg(string msg, string title)
        {
            MessageBox.Show(msg, title);
        }
    }
}
