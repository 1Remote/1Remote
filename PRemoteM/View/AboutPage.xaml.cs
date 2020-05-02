﻿using System;
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
using PRM.ViewModel;

namespace PRM.View
{
    /// <summary>
    /// AboutPage.xaml 的交互逻辑
    /// </summary>
    public partial class AboutPage : UserControl
    {
        public readonly VmMain Vm;
        public AboutPage(VmMain vm)
        {
            InitializeComponent();
            Vm = vm;
            DataContext = Vm;
        }
    }
}
