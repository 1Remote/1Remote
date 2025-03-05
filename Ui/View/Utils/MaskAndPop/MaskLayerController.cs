using System;
using System.Windows;
using _1RM.View.Utils.MaskAndPop;
using Stylet;

namespace _1RM.View.Utils;

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
            if (layerContainer.TopLevelViewModel?.IsNeedRemoval(layerId ?? DateTimeOffset.Now.ToUnixTimeMilliseconds()) == true)
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

    public static bool? ShowDialogWithMask(PopupBase viewModel, object? ownerViewModel = null, bool doNotHideMaskIfReturnTrue = false)
    {
        IMaskLayerContainer? layerContainer = null;
        var mainWindowViewModel = IoC.TryGet<MainWindowViewModel>();
        if (ownerViewModel is IMaskLayerContainer mlc)
        {
            layerContainer = mlc;
        }
        else if (ownerViewModel == null && mainWindowViewModel?.View is Window { ShowInTaskbar: true })
        {
            layerContainer = mainWindowViewModel;
        }

        long layerId = 0;
        if (layerContainer != null)
            layerId = MaskLayerController.ShowProcessingRing(assignLayerContainer: layerContainer);

        var ret = viewModel.ShowDialog(IoC.Get<MainWindowViewModel>());
        if (layerContainer != null)
        {
            if (ret == true && doNotHideMaskIfReturnTrue == true)
            {
            }
            else
            {
                MaskLayerController.HideMask(layerId, layerContainer);
            }
        }

        return ret;
    }

    public static void ShowWindowWithMask(PopupBase viewModel, object? ownerViewModel = null, bool doNotHideMaskIfReturnTrue = false, Action? onCloseWithTrue = null)
    {
        IMaskLayerContainer? layerContainer = null;
        var mainWindowViewModel = IoC.TryGet<MainWindowViewModel>();
        if (ownerViewModel is IMaskLayerContainer mlc)
        {
            layerContainer = mlc;
        }
        else if (ownerViewModel == null && mainWindowViewModel?.View is Window { ShowInTaskbar: true })
        {
            layerContainer = mainWindowViewModel;
        }

        long layerId = 0;
        if (layerContainer != null)
            layerId = MaskLayerController.ShowProcessingRing(assignLayerContainer: layerContainer);

        viewModel.ShowWindow(IoC.Get<MainWindowViewModel>());
        if (layerContainer != null && doNotHideMaskIfReturnTrue != true)
        {
            viewModel.Closed += (sender, args) =>
            {
                MaskLayerController.HideMask(layerId, layerContainer);
                if (viewModel.DialogResult == true)
                    onCloseWithTrue?.Invoke();
            };
        }
    }
}