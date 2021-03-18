using System.Diagnostics;

namespace Shawn.Utils
{
    public static class CmdRunner
    {
        /// <summary>
        /// cmd by cmd.exe
        /// </summary>
        /// <returns>[0] = output info，[1] = ret code</returns>
        public static string[] RunCmdSync(string cmd, bool createNoWindow = false)
        {
            var pro = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = createNoWindow
                }
            };
            //pro.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            pro.Start();
            pro.StandardInput.WriteLine(cmd);
            pro.StandardInput.WriteLine("echo %ERRORLEVEL%");// add a symble for exit code
            pro.StandardInput.WriteLine("exit");// add a symble for exit code
            pro.StandardInput.AutoFlush = true;
            var output = pro.StandardOutput.ReadToEnd();
            pro.WaitForExit();
            pro.Close();

            var content = output.Substring(output.IndexOf(cmd + "\r\n") + (cmd + "\r\n").Length);
            content = content.Substring(0, content.IndexOf("echo %ERRORLEVEL%\r\n"));
            content = content.Substring(0, content.LastIndexOf("\r\n")).Trim(new[] { '\r', '\n', ' ' });

            var retCode = output.Substring(output.LastIndexOf("echo %ERRORLEVEL%\r\n") + "echo %ERRORLEVEL%\r\n".Length);
            retCode = retCode.Substring(0, retCode.IndexOf("\r\n")).Trim(new[] { '\r', '\n', ' ' });
            return new[] { content, retCode };
        }

        /// <summary>
        /// cmd by cmd.exe
        /// </summary>
        public static void RunCmdAsync(string cmd, bool createNoWindow = false)
        {
            var pro = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = createNoWindow
                }
            };
            pro.Start();
            pro.StandardInput.WriteLine(cmd);
        }

        public static void RunExeAsync(string cmd, bool createNoWindow = false)
        {
            Process.Start(cmd);
        }
    }
}