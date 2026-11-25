using System.Reflection;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using Xunit;

namespace Tests.Model
{
    /// <summary>
    /// The Test button of the "script after disconnected" field used to name the path of the "script before connect".
    /// </summary>
    public class ProtocolBaseScriptNoticeTests
    {
        private const string BeforeScript = @"C:\no_such_dir\before.bat";
        private const string AfterScript = @"C:\no_such_dir\after.bat";

        private static string GetNotice(ProtocolBase server, bool isBeforeConnect)
        {
            var method = typeof(ProtocolBase).GetMethod("GetScriptTestRunNotice",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.True(method != null, "GetScriptTestRunNotice not found in ProtocolBase");
            return (string)method!.Invoke(server, new object[] { isBeforeConnect })!;
        }

        private static Dummy NewServer(string beforeScript, string afterScript)
        {
            return new Dummy { CommandBeforeConnected = beforeScript, CommandAfterDisconnected = afterScript };
        }

        [Fact]
        public void Notice_OfAfterDisconnectedScript_NamesTheAfterScript()
        {
            var server = NewServer(BeforeScript, AfterScript);
            Assert.Equal($"We will run: '{AfterScript}'", GetNotice(server, isBeforeConnect: false));
        }

        [Fact]
        public void Notice_OfBeforeConnectScript_NamesTheBeforeScript()
        {
            var server = NewServer(BeforeScript, AfterScript);
            Assert.Equal($"We will run: '{BeforeScript}'", GetNotice(server, isBeforeConnect: true));
        }

        [Fact]
        public void Notice_OfScriptWithParameters_NamesFileAndParameters()
        {
            var server = NewServer(BeforeScript, $@"""{AfterScript}"" -x");
            Assert.Equal($"We will run: '{AfterScript}' with parameters '-x'", GetNotice(server, isBeforeConnect: false));
        }
    }
}
