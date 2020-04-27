using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Shawn.Ulits
{
    public static class ConsoleManager
    {
        private const string Kernel32_DllName = "kernel32.dll";
        [DllImport(Kernel32_DllName)]
        private static extern bool AllocConsole();
        [DllImport(Kernel32_DllName)]
        private static extern bool FreeConsole();
        [DllImport(Kernel32_DllName)]
        private static extern IntPtr GetConsoleWindow();
        [DllImport(Kernel32_DllName)]
        private static extern int GetConsoleOutputCP();
        public static bool HasConsole
        {
            get { return GetConsoleWindow() != IntPtr.Zero; }
        }
        /// Creates a new console instance if the process is not attached to a console already.    
        public static void Show()
        {
#if DEBUG
            if (!HasConsole)
            {
                AllocConsole();
                InvalidateOutAndError();
            }
#endif
        }
        /// If the process has a console attached to it, it will be detached and no longer visible. Writing to the System.Console is still possible, but no output will be shown.     
        public static void Hide()
        {
#if DEBUG
            if (HasConsole)
            {
                SetOutAndErrorNull();
                FreeConsole();
            }
#endif
        }
        public static void Toggle()
        {
            if (HasConsole)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }
        static void InvalidateOutAndError()
        {
            Type type = typeof(System.Console);
            System.Reflection.FieldInfo _out = type.GetField("_out",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.FieldInfo _error = type.GetField("_error",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.MethodInfo _InitializeStdOutError = type.GetMethod("InitializeStdOutError",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            System.Diagnostics.Debug.Assert(_out != null);
            System.Diagnostics.Debug.Assert(_error != null);
            System.Diagnostics.Debug.Assert(_InitializeStdOutError != null);
            _out.SetValue(null, null);
            _error.SetValue(null, null);
            _InitializeStdOutError.Invoke(null, new object[] { true });
        }
        static void SetOutAndErrorNull()
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }
    }
}
