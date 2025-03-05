using System.Windows;

namespace _1RM.View.Utils;

/****
 遮罩层控制器 IMaskLayerContainer MaskLayerContainerScreenBase

 遮罩层容器 MaskLayerController

 遮罩层，MaskLayer

 遮罩层容器(window view)继承 MaskLayerContainerScreenBase, 启动后订阅到 MaskLayerController.ProcessingRingInvoke
 使用 MaskLayerController.OnShowProcessingRing 即可控制所有遮罩层容器的遮罩层显示
 */

public interface IMaskLayerContainer
{
    public MaskLayer? TopLevelViewModel { get; set; }

    void OnShowProcessingRing(long layerId, Visibility visibility, string msg);
    void ShowOrHideMask(long layerId, MaskLayer? layer);
}