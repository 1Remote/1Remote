using System.Windows;

namespace PRM.I
{
    public interface ITab
    {
        Size GetTabContentSize(bool colorIsTransparent);
    }
}