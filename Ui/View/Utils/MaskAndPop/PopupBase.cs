using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using _1RM.Utils;
using Stylet;

namespace _1RM.View.Utils.MaskAndPop
{
    public abstract class PopupBase : MaskLayerContainerScreenBase
    {
        public bool? DialogResult { get; private set; }
        public bool IsClosed { get; private set; } = false;
        public bool IsShowWithDialog { get; private set; } = false;

        public bool? ShowDialog(IViewAware? ownerViewModel = null)
        {
            return IoC.Get<IWindowManager>().ShowDialog(this, ownerViewModel);
        }

        public void ShowWindow(IViewAware? ownerViewModel = null)
        {
            IoC.Get<IWindowManager>().ShowWindow(this, ownerViewModel);
        }

        public override void RequestClose(bool? dialogResult = null)
        {
            DialogResult = dialogResult;
            base.RequestClose(IsShowWithDialog ? dialogResult : null);
            IsClosed = true;
        }

        public async Task<bool?> WaitDialogResult()
        {
            while (!IsClosed)
            {
                await Task.Delay(100);
            }
            return DialogResult;
        }
    }
}
