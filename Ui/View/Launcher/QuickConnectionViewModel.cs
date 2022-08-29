using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using _1RM.Controls.NoteDisplay;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.PageHost;
using Stylet;

namespace _1RM.View.Launcher
{
    public class QuickConnectionViewModel : NotifyPropertyChangedBaseScreen
    {
        public TextBox TbKeyWord { get; private set; } = new TextBox();
        private readonly LauncherWindowViewModel _launcherWindowViewModel;
        private readonly SolidColorBrush _highLightBrush = new SolidColorBrush(Color.FromArgb(80, 239, 242, 132));
        public List<string> Protocols { get; }
        public QuickConnectionViewModel(LauncherWindowViewModel launcherWindowViewModel)
        {
            _launcherWindowViewModel = launcherWindowViewModel;

            var protocolList = ProtocolBase.GetAllSubInstance();
            var names = protocolList.Select(x => x.Protocol);
            Protocols = names.ToList();
        }

        protected override void OnViewLoaded()
        {
            if (this.View is QuickConnectionView window)
            {
                TbKeyWord = window.TbKeyWord;
                TbKeyWord.Focus();
            }
        }

        private string _selectedProtocol = RDP.ProtocolName;
        public string SelectedProtocol
        {
            get => _selectedProtocol;
            set => SetAndNotifyIfChanged(ref _selectedProtocol, value);
        }

        private string _filter = "";
        public string Filter
        {
            get => _filter;
            set => SetAndNotifyIfChanged(ref _filter, value);
        }


        private ObservableCollection<ProtocolAction> _actions = new ObservableCollection<ProtocolAction>();
        public ObservableCollection<ProtocolAction> Actions
        {
            get => _actions;
            set => SetAndNotifyIfChanged(ref _actions, value);
        }
    }
}
