using PRM.Model;

namespace PRM.I
{
    public interface IIntegratable
    {
        string GetExeFullPath();
        string GetExeArguments(PrmContext context);
    }
}