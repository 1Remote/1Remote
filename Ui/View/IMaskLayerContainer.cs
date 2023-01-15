using System.ComponentModel;
using System.Windows;
using Shawn.Utils;
using Stylet;

namespace _1RM.View;

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