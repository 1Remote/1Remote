using System.Windows;

namespace PRM.View.Host
{
    public interface ITabWindow
    {
        Size GetTabContentSize(bool colorIsTransparent);
    }
}