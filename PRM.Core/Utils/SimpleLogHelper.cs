using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shawn.Utils
{
    public static class SimpleLogHelper
    {
        public static string LogFileName = "simple.log.md";

        public static Level PrintLogLevel = Level.Debug;
        public static Level WriteLogLevel = Level.Info;
        public static bool CanWriteWarningLog = true;
        public static long LogFileMaxSizeMb = 10;


        private static readonly object _obj = new object();

        public enum Level
        {
            Debug,
            Info,
            Warning,
            Error,
            Fatal,
        }

        public static void Debug(params object[] o)
        {
#if DEBUG
            var dt = DateTime.Now;
            Print(Level.Debug, dt, o);
            WriteLog(Level.Debug, dt, o);
#endif
        }

        public static void Info(params object[] o)
        {
            var dt = DateTime.Now;
            Print(Level.Info, dt, o);
            WriteLog(Level.Info, dt, o);
        }

        public static void Warning(params object[] o)
        {
            var dt = DateTime.Now;
            Print(Level.Warning, dt, o);
            WriteLog(Level.Warning, dt, o);
        }

        public static void Error(params object[] o)
        {
            var dt = DateTime.Now;
            Print(Level.Error, dt, o);
            WriteLog(Level.Error, dt, o);
        }

        public static void Fatal(params object[] o)
        {
            var dt = DateTime.Now;
            Print(Level.Fatal, dt, o);
            WriteLog(Level.Fatal, dt, o);
        }

        public static void Print(Level level, DateTime? dt = null, params object[] o)
        {
            if (level >= PrintLogLevel)
            {
                if (dt == null)
                    dt = DateTime.Now;
                Console.Write($"[{dt:o}]\t{level}\t\t\t");
                foreach (var obj in o)
                {
                    Console.WriteLine(obj);
                }
            }
        }

        private static void WriteLog(Level level, DateTime? dt = null, params object[] o)
        {
            try
            {
                if (dt == null)
                    dt = DateTime.Now;
                lock (_obj)
                {
                    if (File.Exists(LogFileName))
                    {
                        var fi = new FileInfo(LogFileName);
                        if (fi.Length > 1024 * 1024 * LogFileMaxSizeMb)
                        {
                            var lines =File.ReadAllLines(LogFileName);
                            File.WriteAllLines(LogFileName, lines.Skip(lines.Length / 3).ToArray());
                        }
                    }
                    using (StreamWriter sw = new StreamWriter(new FileStream(LogFileName, FileMode.Append)))
                    {
                        sw.WriteLine($"\r\n---");
                        sw.WriteLine($"\r\n## {dt:o}\t\t{level}\t\t");
                        sw.WriteLine("\r\n```");
                        foreach (var obj in o)
                        {
                            sw.WriteLine(obj);
                        }
                        sw.WriteLine("\r\n```\r\n");
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}