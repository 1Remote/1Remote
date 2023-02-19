using System;
using System.Windows;
using _1RM.Utils;
using Shawn.Utils;
using Stylet;

namespace _1RM.View.Utils;

/****
 遮罩层控制器 IMaskLayerContainer MaskLayerContainerScreenBase

 遮罩层容器 MaskLayerController

 遮罩层，MaskLayer

 遮罩层容器(window view)继承 MaskLayerContainerScreenBase, 启动后订阅到 MaskLayerController.ProcessingRingInvoke
 使用 MaskLayerController.OnShowProcessingRing 即可控制所有遮罩层容器的遮罩层显示
 */

public abstract class MaskLayer : NotifyPropertyChangedBase
{
    public long LayerId { get; set; }

    /// <summary>
    /// 判断当前遮罩是否应当被移除
    /// </summary>
    /// <param name="latestLayerId"></param>
    /// <returns></returns>
    public bool NeedRemove(long latestLayerId)
    {
        return latestLayerId >= LayerId;
    }
}

public interface IMaskLayerContainer
{
    public MaskLayer? TopLevelViewModel { get; set; }

    void OnShowProcessingRing(long layerId, Visibility visibility, string msg);
    void ShowOrHideMask(long layerId, MaskLayer? layer);
}

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
            if (this.TopLevelViewModel?.NeedRemove(layerId) == true)
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
            else if (this.TopLevelViewModel?.NeedRemove(layerId) == true)
            {
                ShowOrHideMask(layerId);
            }
        });
    }
}

public static class MaskLayerController
{
    public static Action<long, MaskLayer?>? ShowMaskInvoke { get; set; } = null;

    public static long ShowProcessingRing(string msg = "", IMaskLayerContainer? assignLayerContainer = null)
    {
        var pvm = IoC.Get<ProcessingRingViewModel>();
        pvm.ProcessingRingMessage = msg;
        return ShowMask(pvm, assignLayerContainer);
    }

    public static long ShowMask(MaskLayer layer, IMaskLayerContainer? assignLayerContainer)
    {
        var id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        layer.LayerId = id;

        if (assignLayerContainer != null)
        {
            // 指定容器上显示 mask
            assignLayerContainer.ShowOrHideMask(id, layer);
        }
        else
        {
            // 所有容器上显示 mask
            ShowMaskInvoke?.Invoke(id, layer);
        }
        return id;
    }

    public static void HideMask(long? layerId = null, IMaskLayerContainer? layerContainer = null)
    {
        if (layerContainer != null)
        {
            // 关闭指定容器的 mask
            if (layerContainer.TopLevelViewModel?.NeedRemove(layerId ?? DateTimeOffset.Now.ToUnixTimeMilliseconds()) == true)
            {
                layerContainer.TopLevelViewModel = null;
            }
        }
        else
        {
            // 关闭所有容器的 mask
            ShowMaskInvoke?.Invoke(layerId ?? DateTimeOffset.Now.ToUnixTimeMilliseconds(), null);
        }
    }


    public static void HideMask(IMaskLayerContainer layerContainer)
    {
        HideMask(null, layerContainer);
    }
}