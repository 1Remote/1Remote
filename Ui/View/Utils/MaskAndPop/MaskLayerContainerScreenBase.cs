using System.Windows;
using _1RM.Utils;
using Stylet;

namespace _1RM.View.Utils;

public abstract class MaskLayerContainerScreenBase : NotifyPropertyChangedBaseScreen, IMaskLayerContainer
{
    private MaskLayer? _topLevelViewModel;
    public MaskLayer? TopLevelViewModel
    {
        get => _topLevelViewModel;
        set => SetAndNotifyIfChanged(ref _topLevelViewModel, value);
    }

    protected override void OnViewLoaded()
    {
        base.OnViewLoaded();
        MaskLayerController.ShowMaskInvoke += ShowOrHideMask;
    }

    protected override void OnClose()
    {
        base.OnClose();
        MaskLayerController.ShowMaskInvoke -= ShowOrHideMask;
    }

    public virtual void ShowOrHideMask(long layerId, MaskLayer? layer = null)
    {
        if (layer == null)
        {
            if (this.TopLevelViewModel?.IsNeedRemoval(layerId) == true)
            {
                this.TopLevelViewModel = null;
            }
        }
        else
        {
            if (layer.LayerId < layerId)
                layer.LayerId = layerId;
            this.TopLevelViewModel = layer;
        }
    }

    public virtual void OnShowProcessingRing(long layerId, Visibility visibility, string msg)
    {
        Execute.OnUIThread(() =>
        {
            if (visibility == Visibility.Visible)
            {
                var pvm = IoC.Get<ProcessingRingViewModel>();
                pvm.LayerId = layerId;
                pvm.ProcessingRingMessage = msg;
                ShowOrHideMask(layerId, pvm);
            }
            else if (this.TopLevelViewModel?.IsNeedRemoval(layerId) == true)
            {
                ShowOrHideMask(layerId);
            }
        });
    }
}