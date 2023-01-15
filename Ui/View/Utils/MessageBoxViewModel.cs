using Stylet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace _1RM.View.Utils
{
    /// <summary>
    /// Default implementation of IMessageBoxViewModel, and is therefore the ViewModel shown by default by ShowMessageBox
    /// </summary>
    public class MessageBoxViewModel : Screen, IMessageBoxViewModel
    {
        private MessageBoxPageViewModel? _pageViewModel;

        public MessageBoxPageViewModel? PageViewModel
        {
            get => _pageViewModel;
            set => SetAndNotify(ref _pageViewModel, value);
        }

        /// <summary>
        /// Setup the MessageBoxViewModel with the information it needs
        /// </summary>
        /// <param name="messageBoxText">A <see cref="System.String"/> that specifies the text to display.</param>
        /// <param name="caption">A <see cref="System.String"/> that specifies the title bar caption to display.</param>
        /// <param name="buttons">A <see cref="System.Windows.MessageBoxButton"/> value that specifies which button or buttons to display.</param>
        /// <param name="icon">A <see cref="System.Windows.MessageBoxImage"/> value that specifies the icon to display.</param>
        /// <param name="defaultResult">A <see cref="System.Windows.MessageBoxResult"/> value that specifies the default result of the message box.</param>
        /// <param name="cancelResult">A <see cref="System.Windows.MessageBoxResult"/> value that specifies the cancel result of the message box</param>
        /// <param name="buttonLabels">A dictionary specifying the button labels, if desirable</param>
        /// <param name="flowDirection">The <see cref="System.Windows.FlowDirection"/> to use, overrides the <see cref="MessageBoxViewModel.DefaultFlowDirection"/></param>
        /// <param name="textAlignment">The <see cref="System.Windows.TextAlignment"/> to use, overrides the <see cref="MessageBoxViewModel.DefaultTextAlignment"/></param>
        public void Setup(
            string messageBoxText,
            string? caption = null,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None,
            MessageBoxResult cancelResult = MessageBoxResult.None,
            IDictionary<MessageBoxResult, string>? buttonLabels = null,
            FlowDirection? flowDirection = null,
            TextAlignment? textAlignment = null)
        {
            PageViewModel = new MessageBoxPageViewModel();
            PageViewModel.Setup(messageBoxText, caption, buttons, icon, defaultResult, cancelResult, buttonLabels, flowDirection, textAlignment,
                () => { this.RequestClose(true); });
            this.DisplayName = PageViewModel.DisplayName;
        }

        /// <summary>
        /// Gets or sets which button the user clicked, once they've clicked a button
        /// </summary>
        public virtual MessageBoxResult ClickedButton => _pageViewModel?.ClickedButton ?? MessageBoxResult.None;
    }
}
