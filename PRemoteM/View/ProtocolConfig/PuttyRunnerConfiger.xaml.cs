using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PRM.Core.Model;
using PRM.Core.Protocol.Runner.Default;

namespace PRM.View.ProtocolConfig
{
    /// <summary>
    /// PuttyRunnerConfiger.xaml 的交互逻辑
    /// </summary>
    public partial class PuttyRunnerConfiger : UserControl
    {
        public PuttyRunnerConfiger()
        {
            InitializeComponent();
        }

        public void Init(PrmContext prmContext)
        {
            Debug.Assert(prmContext.ProtocolConfigurationService.ProtocolConfigs["SSH"].Runners.FirstOrDefault() is SshDefaultRunner);
            DataContext = prmContext.ProtocolConfigurationService.ProtocolConfigs["SSH"].Runners.FirstOrDefault();
        }
    }
}
