using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using _1RM.View.Host.ProtocolHosts;
using Xunit;

namespace Tests.View.Host
{
    /// <summary>
    /// The retry loop of the RDP host hides the text block that explains an error, and nothing made
    /// it visible again. After one retry loop the final screen and every later error showed their
    /// buttons without any text.
    /// </summary>
    public class AxMsRdpClient09HostMessageTests
    {
        [Fact]
        public void ShowMessage_MakesTheTextVisibleAgain()
        {
            StaThread.Run(() =>
            {
                // the host is built without running a constructor, only its text block is needed
                var host = RuntimeHelpers.GetUninitializedObject(typeof(AxMsRdpClient09Host));
                GC.SuppressFinalize(host);
                var textBlock = new TextBlock { Visibility = Visibility.Collapsed };
                var textBlockField = typeof(AxMsRdpClient09Host)
                    .GetField("TbMessage", BindingFlags.NonPublic | BindingFlags.Instance)!;
                textBlockField.SetValue(host, textBlock);
                var showMessage = typeof(AxMsRdpClient09Host)
                    .GetMethod("ShowMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.True(showMessage != null, "AxMsRdpClient09Host.ShowMessage was not found");

                showMessage!.Invoke(host, new object[] { "the reason" });

                Assert.Equal("the reason", textBlock.Text);
                Assert.Equal(Visibility.Visible, textBlock.Visibility);
            });
        }
    }
}
