using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PRM.Annotations;
using PRM.Core.Model;
using Shawn.Utils;
using Shawn.Utils.PageHost;

namespace PRM.View
{
    /// <summary>
    /// GuidanceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GuidanceWindow : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        protected bool NotifyPropertyChangedEnabled = true;

        public void SetNotifyPropertyChangedEnabled(bool isEnabled)
        {
            NotifyPropertyChangedEnabled = isEnabled;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (NotifyPropertyChangedEnabled)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void SetAndNotifyIfChanged<T>(string propertyName, ref T oldValue, T newValue)
        {
            if (oldValue == null && newValue == null) return;
            if (oldValue != null && oldValue.Equals(newValue)) return;
            if (newValue != null && newValue.Equals(oldValue)) return;
            oldValue = newValue;
            RaisePropertyChanged(propertyName);
        }
        #endregion

        public GuidanceWindow(SystemConfig config)
        {
            InitializeComponent();

            // stop auto saving configs.
            SystemConfig = config;
            SystemConfig.StopAutoSaveConfig = true;

            _step = 0;
            Grid1.Visibility = Visibility.Visible;
            Grid2.Visibility = Visibility.Collapsed;

            WinTitleBar.PreviewMouseDown += WinTitleBar_MouseDown;
            WinTitleBar.MouseUp += WinTitleBar_OnMouseUp;
            WinTitleBar.PreviewMouseMove += WinTitleBar_OnPreviewMouseMove;

            DataContext = this;



            // set default language
            CultureInfo ci = CultureInfo.CurrentCulture;
            Console.WriteLine("CultureInfo.CurrentCulture");
            Console.WriteLine(CultureInfo.CurrentCulture);
            Console.WriteLine("CultureInfo.CurrentUICulture");
            Console.WriteLine(CultureInfo.CurrentUICulture);
            Console.WriteLine("CultureInfo.DefaultThreadCurrentCulture");
            Console.WriteLine(CultureInfo.DefaultThreadCurrentCulture);
            Console.WriteLine("CultureInfo.DefaultThreadCurrentUICulture");
            Console.WriteLine(CultureInfo.DefaultThreadCurrentUICulture);

            Console.WriteLine("Default Language Info:");
            Console.WriteLine("* Name: {0}", ci.Name);
            Console.WriteLine("* Display Name: {0}", ci.DisplayName);
            Console.WriteLine("* English Name: {0}", ci.EnglishName);
            Console.WriteLine("* 2-letter ISO Name: {0}", ci.TwoLetterISOLanguageName);
            Console.WriteLine("* 3-letter ISO Name: {0}", ci.ThreeLetterISOLanguageName);
            Console.WriteLine("* 3-letter Win32 API Name: {0}", ci.ThreeLetterWindowsLanguageName);
            string code = ci.Name.ToLower();
            if (SystemConfig.Instance.Language.LanguageCode2Name.ContainsKey(code))
            {
                SystemConfig.Instance.Language.CurrentLanguageCode = code;
            }
            else
            {
                // use default english
            }


            // saving config when this window close
            Closed += (sender, args) =>
            {
                SystemConfig.StopAutoSaveConfig = false;
                SystemConfig.Save();
            };
        }



        public SystemConfig SystemConfig { get; set; }

        #region DragMove
        private bool _isLeftMouseDown = false;
        private bool _isDragging = false;
        private void WinTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _isLeftMouseDown = false;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isLeftMouseDown = true;
                var th = new Thread(new ThreadStart(() =>
                {
                    Thread.Sleep(50);
                    if (_isLeftMouseDown)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _isDragging = true;
                        });
                    }
                }));
                th.Start();
            }
        }
        private void WinTitleBar_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isLeftMouseDown = false;
            _isDragging = false;
        }
        private void WinTitleBar_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _isDragging)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    var p = ScreenInfoEx.GetMouseVirtualPosition();
                    var top = p.Y;
                    var left = p.X;
                    this.Top = top - 15;
                    this.Left = left - this.Width / 2;
                    this.WindowState = WindowState.Normal;
                    this.Top = top - 15;
                    this.Left = left - this.Width / 2;
                }
                this.DragMove();
            }
        }
        #endregion

        private int _step = 0;
        public int Step
        {
            get => _step;
            set => SetAndNotifyIfChanged(nameof(Step), ref _step, value);
        }


        private void ButtonNext_OnClick(object sender, RoutedEventArgs e)
        {
            if (Step == 0)
            {
                Step = 1;
                Grid2.Visibility = Visibility.Visible;
                var sb = AnimationPage.GetInOutStoryboard(0.5,
                    AnimationPage.InOutAnimationType.SlideToLeft,
                    this.ActualWidth, this.ActualHeight);
                sb.Begin(Grid1);
                var sb2 = AnimationPage.GetInOutStoryboard(0.5,
                    AnimationPage.InOutAnimationType.SlideFromRight,
                    this.ActualWidth, this.ActualHeight);
                sb2.Begin(Grid2);
            }
        }

        private void ButtonPrevious_OnClick(object sender, RoutedEventArgs e)
        {
            if (Step == 1)
            {
                Step = 0;
                Grid2.Visibility = Visibility.Visible;
                var sb = AnimationPage.GetInOutStoryboard(0.5,
                    AnimationPage.InOutAnimationType.SlideFromLeft,
                    this.ActualWidth, this.ActualHeight);
                sb.Begin(Grid1);
                var sb2 = AnimationPage.GetInOutStoryboard(0.5,
                    AnimationPage.InOutAnimationType.SlideToRight,
                    this.ActualWidth, this.ActualHeight);
                sb2.Begin(Grid2);
            }
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
