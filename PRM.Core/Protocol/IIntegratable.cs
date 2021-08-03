using PRM.Core.Model;

namespace PRM.Core.Protocol
{
    public interface IIntegratable
    {
        string GetExeFullPath();
        string GetExeArguments(PrmContext context);
    }
}