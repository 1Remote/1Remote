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
        public enum Level
        {
            Debug,
            Info,
            Warning,
            Error,
            Fatal,
        }

        public static string LogFileName
        {
            get => _simpleLogHelper.LogFileName;
            set => _simpleLogHelper.LogFileName = value;
        }

        public static Level PrintLogLevel
        {
            get => _simpleLogHelper.PrintLogLevel;
            set => _simpleLogHelper.PrintLogLevel = value;
        }

        public static Level WriteLogLevel
        {
            get => _simpleLogHelper.WriteLogLevel;
            set => _simpleLogHelper.WriteLogLevel = value;
        }
        public static long LogFileMaxSizeMb
        {
            get => _simpleLogHelper.LogFileMaxSizeMb;
            set => _simpleLogHelper.LogFileMaxSizeMb = value;
        }


        private static SimpleLogHelperObject _simpleLogHelper = new SimpleLogHelperObject();



        public static void Debug(params object[] o)
        {
            _simpleLogHelper.Debug(o);
        }

        public static void Info(params object[] o)
        {
            _simpleLogHelper.Info(o);
        }

        public static void Warning(params object[] o)
        {
            _simpleLogHelper.Warning(o);
        }

        public static void Error(params object[] o)
        {
            _simpleLogHelper.Error(o);
        }

        public static void Fatal(params object[] o)
        {
            _simpleLogHelper.Fatal(o);
        }
    }


    public class SimpleLogHelperObject
    {
        public SimpleLogHelperObject(string logFileName = "")
        {
            LogFileName = logFileName;
        }

        public string LogFileName = "simple.log.md";

        public SimpleLogHelper.Level PrintLogLevel = SimpleLogHelper.Level.Debug;
        public SimpleLogHelper.Level WriteLogLevel = SimpleLogHelper.Level.Info;
        public long LogFileMaxSizeMb = 100;


        private readonly object _obj = new object();

        public void Debug(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            var dt = DateTime.Now;
            Print(SimpleLogHelper.Level.Debug, dt, o);
            WriteLog(SimpleLogHelper.Level.Debug, dt, o);
        }

        public void Info(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            var dt = DateTime.Now;
            Print(SimpleLogHelper.Level.Info, dt, o);
            WriteLog(SimpleLogHelper.Level.Info, dt, o);
        }

        public void Warning(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            var dt = DateTime.Now;
            Print(SimpleLogHelper.Level.Warning, dt, o);
            WriteLog(SimpleLogHelper.Level.Warning, dt, o);
        }

        public void Error(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            var dt = DateTime.Now;
            Print(SimpleLogHelper.Level.Error, dt, o);
            WriteLog(SimpleLogHelper.Level.Error, dt, o);
        }

        public void Fatal(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Red;
            var dt = DateTime.Now;
            Print(SimpleLogHelper.Level.Fatal, dt, o);
            WriteLog(SimpleLogHelper.Level.Fatal, dt, o);
        }

        private void Print(SimpleLogHelper.Level level, DateTime? dt = null, params object[] o)
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
            Console.ResetColor();
        }

        private void WriteLog(SimpleLogHelper.Level level, DateTime? dt = null, params object[] o)
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
                            var lines = File.ReadAllLines(LogFileName);
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