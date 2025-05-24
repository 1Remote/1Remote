
using _1RM.Model.Protocol.Base;

namespace _1RM.Model.ProtocolRunner.Default
{
    public abstract class InternalRunner(string ownerProtocolName) : Runner("Internal runner", ownerProtocolName)
    {
    }

    /// <summary>
    /// for rdp \ vnc \ sftp
    /// </summary>
    /// <param name="ownerProtocolName"></param>
    public class InternalDefaultRunner(string ownerProtocolName) : InternalRunner(ownerProtocolName)
    {
    }

    /// <summary>
    /// for built-in putty \ kitty
    /// </summary>
    /// <param name="ownerProtocolName"></param>
    public abstract class InternalExeRunner(string ownerProtocolName) : InternalDefaultRunner(ownerProtocolName)
    {
        /// <summary>
        /// install the runner exe to the path. if path is empty, use the default path.
        /// </summary>
        public abstract void Install(string path = "");

        /// <summary>
        /// the runner exe path. if path is empty, then the runner is a built-in runner.
        /// </summary>
        public abstract string GetExeInstallPath();

        /// <summary>
        /// get the startup arguments for the runner on the protocol.
        /// </summary>
        public abstract string GetExeArguments(ProtocolBase protocol);
    }
}
