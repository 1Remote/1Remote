using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Shawn.Utils
{
    public static class SimpleLogHelper
    {
        public enum EnumLogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Fatal,
        }

        public enum EnumLogFileType
        {
            Txt,
            MarkDown,
        }

        public static string LogFileName
        {
            get => _simpleLogHelper.DebugFileName;
            set
            {
                _simpleLogHelper.DebugFileName = value;
                _simpleLogHelper.InfoFileName = value;
                _simpleLogHelper.WarningFileName = value;
                _simpleLogHelper.ErrorFileName = value;
                _simpleLogHelper.FatalFileName = value;
            }
        }

        //public static string DebugFileName
        //{
        //    get => _simpleLogHelper.DebugFileName;
        //    set => _simpleLogHelper.DebugFileName = value;
        //}
        //public static string InfoFileName
        //{
        //    get => _simpleLogHelper.InfoFileName;
        //    set => _simpleLogHelper.InfoFileName = value;
        //}
        //public static string WarningFileName
        //{
        //    get => _simpleLogHelper.WarningFileName;
        //    set => _simpleLogHelper.WarningFileName = value;
        //}
        //public static string ErrorFileName
        //{
        //    get => _simpleLogHelper.ErrorFileName;
        //    set => _simpleLogHelper.ErrorFileName = value;
        //}
        //public static string FatalFileName
        //{
        //    get => _simpleLogHelper.FatalFileName;
        //    set => _simpleLogHelper.FatalFileName = value;
        //}

        public static EnumLogLevel PrintLogEnumLogLevel
        {
            get => _simpleLogHelper.PrintLogLevel;
            set => _simpleLogHelper.PrintLogLevel = value;
        }

        public static EnumLogLevel WriteLogEnumLogLevel
        {
            get => _simpleLogHelper.WriteLogLevel;
            set => _simpleLogHelper.WriteLogLevel = value;
        }

        public static long LogFileMaxSizeMb
        {
            get => _simpleLogHelper.LogFileMaxSizeMegabytes;
            set => _simpleLogHelper.LogFileMaxSizeMegabytes = value;
        }

        public static EnumLogFileType LogFileType
        {
            get => _simpleLogHelper.LogFileType;
            set => _simpleLogHelper.LogFileType = value;
        }

        private static readonly SimpleLogHelperObject _simpleLogHelper = new SimpleLogHelperObject();

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

        public static string GetLog(int lastLineCount = 50)
        {
            // append date
            var withOutExtension = LogFileName.Substring(0, LogFileName.LastIndexOf(".", StringComparison.Ordinal));
            var logFileName = $"{withOutExtension}_{DateTime.Now.ToString("yyyyMMdd")}{new FileInfo(LogFileName).Extension}";
            return _simpleLogHelper.GetLog(logFileName, lastLineCount);
        }
    }

    public class SimpleLogHelperObject
    {
        public SimpleLogHelperObject(string logFileName = "")
        {
            if (!string.IsNullOrWhiteSpace(logFileName))
            {
                DebugFileName = logFileName;
                InfoFileName = logFileName;
                WarningFileName = logFileName;
                ErrorFileName = logFileName;
                FatalFileName = logFileName;
            }
        }

        public SimpleLogHelperObject(
            string debugLogFileName,
            string infoLogFileName,
            string warningLogFileName,
            string errorLogFileName,
            string fatalLogFileName)
        {
            if (!string.IsNullOrWhiteSpace(debugLogFileName))
                DebugFileName = debugLogFileName;
            if (!string.IsNullOrWhiteSpace(infoLogFileName))
                InfoFileName = infoLogFileName;
            if (!string.IsNullOrWhiteSpace(warningLogFileName))
                WarningFileName = warningLogFileName;
            if (!string.IsNullOrWhiteSpace(errorLogFileName))
                ErrorFileName = errorLogFileName;
            if (!string.IsNullOrWhiteSpace(fatalLogFileName))
                FatalFileName = fatalLogFileName;
        }

        public string DebugFileName { get; set; } = "simple.log.md";
        public string InfoFileName { get; set; } = "simple.log.md";
        public string WarningFileName { get; set; } = "simple.log.md";
        public string ErrorFileName { get; set; } = "simple.log.md";
        public string FatalFileName { get; set; } = "simple.log.md";

        /// <summary>
        /// if log file size over this vale, old log file XXXXX.log will be moved to XXXXX.001.log
        /// </summary>
        public long LogFileMaxSizeMegabytes { get; set; } = 10;

        public SimpleLogHelper.EnumLogLevel PrintLogLevel { get; set; } = SimpleLogHelper.EnumLogLevel.Debug;
        public SimpleLogHelper.EnumLogLevel WriteLogLevel { get; set; } = SimpleLogHelper.EnumLogLevel.Info;
        public SimpleLogHelper.EnumLogFileType LogFileType { get; set; } = SimpleLogHelper.EnumLogFileType.MarkDown;

        /// <summary>
        /// del log files created before LogFileMaxHistoryDays if LogFileMaxHistoryDays > 0
        /// </summary>
        public uint LogFileMaxHistoryDays { get; set; } = 60;

        private readonly object _obj = new object();

        public void Debug(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            var dt = DateTime.Now;
            var tid = Thread.CurrentThread.ManagedThreadId;
            Print(SimpleLogHelper.EnumLogLevel.Debug, tid, dt, o);
            WriteLog(SimpleLogHelper.EnumLogLevel.Debug, tid, dt, o);
        }

        public void Info(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            var dt = DateTime.Now;
            var tid = Thread.CurrentThread.ManagedThreadId;
            Print(SimpleLogHelper.EnumLogLevel.Info, tid, dt, o);
            WriteLog(SimpleLogHelper.EnumLogLevel.Info, tid, dt, o);
        }

        public void Warning(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            var dt = DateTime.Now;
            var tid = Thread.CurrentThread.ManagedThreadId;
            Print(SimpleLogHelper.EnumLogLevel.Warning, tid, dt, o);
            WriteLog(SimpleLogHelper.EnumLogLevel.Warning, tid, dt, o);
        }

        public void Error(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            var dt = DateTime.Now;
            var tid = Thread.CurrentThread.ManagedThreadId;
            Print(SimpleLogHelper.EnumLogLevel.Error, tid, dt, o);
            WriteLog(SimpleLogHelper.EnumLogLevel.Error, tid, dt, o);
        }

        public void Fatal(params object[] o)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Red;
            var dt = DateTime.Now;
            var tid = Thread.CurrentThread.ManagedThreadId;
            Print(SimpleLogHelper.EnumLogLevel.Fatal, tid, dt, o);
            WriteLog(SimpleLogHelper.EnumLogLevel.Fatal, tid, dt, o);
        }

        private void Print(SimpleLogHelper.EnumLogLevel enumLogLevel, int threadId, DateTime? dt = null, params object[] o)
        {
            if (enumLogLevel >= PrintLogLevel)
            {
                dt ??= DateTime.Now;
                Console.Write($"[{dt:o}][ThreadId:{threadId:D10}]\t{enumLogLevel}\t");
                foreach (var obj in o)
                {
                    Console.WriteLine(obj);
                    if (o[0] is Exception e)
                        Console.WriteLine(e.StackTrace);
                }
            }
            Console.ResetColor();
        }

        private string GetFileName(SimpleLogHelper.EnumLogLevel enumLogLevel)
        {
            var logFileName = enumLogLevel switch
            {
                SimpleLogHelper.EnumLogLevel.Debug => DebugFileName,
                SimpleLogHelper.EnumLogLevel.Info => InfoFileName,
                SimpleLogHelper.EnumLogLevel.Warning => WarningFileName,
                SimpleLogHelper.EnumLogLevel.Error => ErrorFileName,
                SimpleLogHelper.EnumLogLevel.Fatal => FatalFileName,
                _ => throw new ArgumentOutOfRangeException(nameof(enumLogLevel), enumLogLevel, null)
            };
            return logFileName;
        }

        private void CleanUpLogFiles(FileInfo fi)
        {
            // clean history
            if (LogFileMaxHistoryDays <= 0) return;
            var di = fi.Directory;
            var withOutExtension = fi.Name.Substring(0, fi.Name.LastIndexOf(".", StringComparison.Ordinal));
            var fis = di.GetFiles($"{withOutExtension}*{fi.Extension}");
            foreach (var fileInfo in fis)
            {
                try
                {
                    var dateStr = fileInfo.Name.Replace(fileInfo.Extension, "");
                    dateStr = dateStr.Substring(dateStr.LastIndexOf("_") + 1);
                    if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, DateTimeStyles.None, out var date)
                        && date < DateTime.Now.AddDays(-1 * LogFileMaxHistoryDays))
                    {
                        fileInfo.Delete();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private void MoveIfLogOverSize(string logFilePath)
        {
            if (!File.Exists(logFilePath)) return;
            var fi = new FileInfo(logFilePath);
            long maxLength = 1024 * 1024 * LogFileMaxSizeMegabytes;
            // over size then move to xxxx.md -> xxxx.001.md
            if (fi.Length <= maxLength) return;
            int i = 1;
            var d = Math.Max(i.ToString().Length, 3);
            string newName = "";
            while (true)
            {
                var withOutExtension = logFilePath.Substring(0, logFilePath.LastIndexOf(".", StringComparison.Ordinal));
                newName = $"{withOutExtension}_{i.ToString($"D{d}")}{fi.Extension}";

                if (!File.Exists($"{newName}"))
                {
                    break;
                }
                ++i;
            }
            File.Move(logFilePath, newName);
        }

        private string GetLogLevelString(SimpleLogHelper.EnumLogLevel enumLogLevel, SimpleLogHelper.EnumLogFileType type)
        {
            string levelString = enumLogLevel.ToString();
            //if (type == SimpleLogHelper.EnumLogFileType.MarkDown)
            //{
            //    levelString = enumLogLevel switch
            //    {
            //        SimpleLogHelper.EnumLogLevel.Debug => $"<font color=Green>{enumLogLevel}</font>",
            //        SimpleLogHelper.EnumLogLevel.Info => $"<font color=Blue>{enumLogLevel}</font>",
            //        SimpleLogHelper.EnumLogLevel.Warning => $"<font color=Yellow>{enumLogLevel}</font>",
            //        SimpleLogHelper.EnumLogLevel.Error => $"*<font color=Red>{enumLogLevel}</font>*",
            //        SimpleLogHelper.EnumLogLevel.Fatal => $"<u>**<font color=Red>{enumLogLevel}</font>**</u>",
            //        _ => throw new ArgumentOutOfRangeException(nameof(enumLogLevel), enumLogLevel, null)
            //    };
            //}
            return levelString;
        }

        private void WriteLog(SimpleLogHelper.EnumLogLevel enumLogLevel, int threadId, DateTime? dt = null, params object[] o)
        {
            try
            {
                dt ??= DateTime.Now;
                lock (_obj)
                {
                    string logFileName = GetFileName(enumLogLevel);

                    // append date
                    var withOutExtension = logFileName.Substring(0, logFileName.LastIndexOf(".", StringComparison.Ordinal));
                    logFileName = $"{withOutExtension}_{DateTime.Now.ToString("yyyyMMdd")}{new FileInfo(logFileName).Extension}";

                    var fi = new FileInfo(logFileName);
                    // craete Directory
                    if (!fi.Directory.Exists)
                        fi.Directory.Create();

                    // clean history
                    CleanUpLogFiles(fi);

                    // over size then move to xxxx.md -> xxxx.001.md
                    MoveIfLogOverSize(logFileName);

                    string levelString = GetLogLevelString(enumLogLevel, LogFileType);

                    using var sw = new StreamWriter(new FileStream(logFileName, FileMode.Append), Encoding.UTF8);
                    sw.Write($"{dt:o}[ThreadId:{threadId:D10}]\t\t{levelString}\t\t");
                    if (o.Length == 1)
                    {
                        sw.WriteLine(o[0]);
                        if (o[0] is Exception e)
                            sw.WriteLine(e.StackTrace);
                        if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                            sw.WriteLine();
                    }
                    else
                    {
                        sw.WriteLine();
                        if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                            sw.WriteLine("\r\n```\r\n");
                        foreach (var obj in o)
                        {
                            sw.WriteLine(obj);
                            if (o[0] is Exception e)
                                sw.WriteLine(e.StackTrace);
                        }
                        if (LogFileType == SimpleLogHelper.EnumLogFileType.MarkDown)
                            sw.WriteLine("\r\n```\r\n");
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public string GetLog(string logFilePath, int lastLineCount = 50)
        {
            if (!File.Exists(logFilePath))
                return null;

            var lines = File.ReadAllLines(logFilePath, Encoding.UTF8);
            var logs = new List<string>();
            for (int i = lines.Length - 1; i >= 0 && lastLineCount > 0; i--)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;

                logs.Add(lines[i]);
                if (lines[i].IndexOf("[ThreadId:", StringComparison.Ordinal) > 0)
                {
                    --lastLineCount;
                }
            }
            var ret = new StringBuilder();
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                ret.AppendLine(logs[i]);
            }
            return ret.ToString();
        }
    }
}