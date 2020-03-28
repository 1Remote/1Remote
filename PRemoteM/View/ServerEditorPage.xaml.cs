using System;
using System.Collections.Generic;
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
using PRM.Core.Base;
using PRM.ViewModel;

namespace PRM.View
{
    /// <summary>
    /// ServerEditorPage.xaml 的交互逻辑
    /// </summary>
    public partial class ServerEditorPage : UserControl
    {
        private VmServerEditorPage _vmServerEditorPage;
        public ServerEditorPage(VmServerEditorPage vm)
        {
            InitializeComponent();
            _vmServerEditorPage = vm;
            DataContext = vm;
            if (vm?.Server != null && vm.Server.GetType() != typeof(NoneServer))
            {
                LogoSelector.SetImg(vm?.Server?.IconImg);
            }
            else
            {
                // TODO 选择随机 LOGO
            }
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (_vmServerEditorPage?.Server != null && _vmServerEditorPage.Server.GetType() != typeof(NoneServer))
            {
                _vmServerEditorPage.Server.IconImg = LogoSelector.Logo;
            }
            if (_vmServerEditorPage != null && _vmServerEditorPage.CmdSave.CanExecute(null))
                _vmServerEditorPage.CmdSave.Execute(null);
        }
    }
}
