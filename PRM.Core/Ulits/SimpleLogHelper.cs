using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Ulits
{
    static class SimpleLogHelper
    {
        public static string LogFileName = "simple.log.md";

        public static Level PrintLogLevel = Level.Debug;
        public static Level WriteLogLevel = Level.Warning;
        public static bool CanWriteWarningLog = true;


        private static readonly object _obj = new object();

        public enum Level
        {
            Debug,
            Info,
            Warning,
            Error,
        }


        public static void Print(object o, Level level = Level.Debug, DateTime? dt = null)
        {
            if (level >= PrintLogLevel)
            {
                if (dt == null)
                    dt = DateTime.Now;
                Console.Write($"[{dt:u}]\t{level}\t\t\t");
                Console.WriteLine(o);
            }
        }

        public static void Log(object o)
        {
            Debug(o);
        }

        public static void Debug(object o)
        {
#if DEBUG
            var dt = DateTime.Now;
            Print(o, Level.Debug, dt);
            WriteLog(o, Level.Debug, dt);
#endif
        }

        public static void Info(object o)
        {
            var dt = DateTime.Now;
            Print(o, Level.Info, dt);
            WriteLog(o, Level.Info, dt);
        }

        public static void Warning(object o)
        {
            var dt = DateTime.Now;
            Print(o, Level.Warning, dt);
            WriteLog(o, Level.Warning, dt);
        }

        public static void Error(object o)
        {
            var dt = DateTime.Now;
            Print(o, Level.Error, dt);
            WriteLog(o, Level.Error, dt);
        }

        private static void WriteLog(object o, Level level, DateTime? dt = null)
        {

            if (dt == null)
                dt = DateTime.Now;
            lock (_obj)
            {
                using (StreamWriter sw = new StreamWriter(new FileStream(LogFileName, FileMode.Append)))
                {
                    sw.WriteLine($"\r\n---");
                    sw.WriteLine($"\r\n## {dt:u}\t\t{level}\t\t");
                    sw.WriteLine("\r\n```");
                    sw.WriteLine(o);
                    sw.WriteLine("\r\n```\r\n");
                }
            }
        }
    }
}