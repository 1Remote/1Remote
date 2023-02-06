using System;
using System.Windows;
using Shawn.Utils;

namespace _1RM.View.Utils;

public abstract class MaskLayer : NotifyPropertyChangedBase
{
    public long LayerId { get; set; }

    public bool CanDelete(long latestLayerId)
    {
        return latestLayerId >= LayerId;
    }
}

public interface IMaskLayerContainer
{
    public MaskLayer? TopLevelViewModel { get; set; }

    void ShowProcessingRing(long layerId, Visibility visibility, string msg);
}

public static class MaskLayerController
{
    /// <summary>
    /// Invoke to show up processing ring
    /// cmd id = arg1
    /// alert info = arg3
    /// </summary>
    public static Action<long, Visibility, string>? ProcessingRingInvoke { get; set; } = null;

    public static long ShowProcessingRingMainWindow(string msg = "")
    {
        var vm = IoC.TryGet<MainWindowViewModel>();
        if (vm == null)
        {
            return -1;
        }
        return ShowProcessingRing(msg: msg, layerContainer: vm);
    }

    public static long ShowProcessingRing(IMaskLayerContainer layerContainer, string msg = "")
    {
        var id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        layerContainer.ShowProcessingRing(id, Visibility.Visible, msg);
        //if (layerContainer != null)
        //{
        //}
        //else
        //{
        //    ProcessingRingInvoke?.Invoke(id, Visibility.Visible, msg);
        //}
        return id;
    }

    public static void HideProcessingRing(long layerId, IMaskLayerContainer? layerContainer = null)
    {
        if (layerContainer != null)
        {
            layerContainer.ShowProcessingRing(layerId, Visibility.Collapsed, "");
        }
        ProcessingRingInvoke?.Invoke(layerId, Visibility.Collapsed, "");
    }
}