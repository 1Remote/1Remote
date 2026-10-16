#if NETFRAMEWORK
// C# 9 record / init-only setters need this marker type, which does not exist on .NET Framework.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
