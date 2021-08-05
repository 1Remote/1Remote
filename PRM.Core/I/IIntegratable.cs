using PRM.Core.Model;

namespace PRM.Core.I
{
    public interface IIntegratable
    {
        string GetExeFullPath();
        string GetExeArguments(PrmContext context);
    }
}