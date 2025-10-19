using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace _1RM.Utils
{
    /// <summary>
    /// Wrapper for Shawn.Utils.Wpf.FileSystem.SelectFileHelper that automatically provides owner window
    /// to fix issue #951: Pop-up window causing the main window to be hidden
    /// </summary>
    public static class SelectFileHelper
    {
        /// <summary>
        /// pop a file select dialog and return file path or null
        /// </summary>
        /// <param name="title"></param>
        /// <param name="selectedFileName"></param>
        /// <param name="initialDirectory"></param>
        /// <param name="filter">e.g. JPG (*.jpg,*.jpeg)|*.jpg;*.jpeg|txt files (*.txt)|*.txt|All files (*.*)|*.*</param>
        /// <param name="currentDirectoryForShowingRelativePath"></param>
        /// <param name="filterIndex">default filter index when filter have multiple values</param>
        /// <param name="checkFileExists"></param>
        /// <param name="owner">The window that owns the dialog. If not specified, attempts to find the active window automatically.</param>
        /// <returns></returns>
        public static string? OpenFile(string? title = null, string? selectedFileName = null, string? initialDirectory = null, string? filter = null, string? currentDirectoryForShowingRelativePath = null, int filterIndex = -1, bool checkFileExists = true, Window? owner = null)
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = checkFileExists,
                DereferenceLinks = true,
                ValidateNames = true,
            };
            if (filter != null)
            {
                dlg.Filter = filter;
                if (filterIndex >= 0)
                    dlg.FilterIndex = filterIndex;
            }
            if (initialDirectory != null && Directory.Exists(initialDirectory))
                dlg.InitialDirectory = initialDirectory;
            if (title != null)
                dlg.Title = title;
            if (selectedFileName != null)
            {
                dlg.FileName = selectedFileName;
            }

            try
            {
                var ownerWindow = owner ?? GetActiveWindow();
                if (ownerWindow != null && dlg.ShowDialog(ownerWindow) != true) return null;
                else if (ownerWindow == null && dlg.ShowDialog() != true) return null;
            }
            catch
            {
                return null;
            }

            return currentDirectoryForShowingRelativePath != null ? dlg.FileName.Replace(currentDirectoryForShowingRelativePath, ".") : dlg.FileName;
        }

        /// <summary>
        /// pop a file select dialog and return file path or null
        /// </summary>
        /// <param name="title"></param>
        /// <param name="selectedFileName"></param>
        /// <param name="initialDirectory"></param>
        /// <param name="filter">e.g. txt files (*.txt)|*.txt|All files (*.*)|*.*</param>
        /// <param name="currentDirectoryForShowingRelativePath"></param>
        /// <param name="filterIndex">default filter index when filter have multiple values</param>
        /// <param name="checkFileExists"></param>
        /// <param name="owner">The window that owns the dialog. If not specified, attempts to find the active window automatically.</param>
        /// <returns></returns>
        public static string? SaveFile(string? title = null, string? selectedFileName = null, string? initialDirectory = null, string? filter = null, string? currentDirectoryForShowingRelativePath = null, int filterIndex = -1, bool checkFileExists = false, Window? owner = null)
        {
            var dlg = new SaveFileDialog()
            {
                CheckFileExists = checkFileExists,
                DereferenceLinks = true,
                ValidateNames = true,
            };
            if (filter != null)
            {
                dlg.Filter = filter;
                if (filterIndex >= 0)
                    dlg.FilterIndex = filterIndex;
            }
            if (initialDirectory != null)
                dlg.InitialDirectory = initialDirectory;
            if (title != null)
                dlg.Title = title;
            if (selectedFileName != null)
                dlg.FileName = selectedFileName;

            var ownerWindow = owner ?? GetActiveWindow();
            if (ownerWindow != null && dlg.ShowDialog(ownerWindow) != true) return null;
            else if (ownerWindow == null && dlg.ShowDialog() != true) return null;

            return currentDirectoryForShowingRelativePath != null ? dlg.FileName.Replace(currentDirectoryForShowingRelativePath, ".") : dlg.FileName;
        }


        /// <summary>
        /// pop a file select dialog and return file path or null
        /// </summary>
        /// <param name="title"></param>
        /// <param name="initialDirectory"></param>
        /// <param name="filter">e.g. txt files (*.txt)|*.txt|All files (*.*)|*.*</param>
        /// <param name="currentDirectoryForShowingRelativePath"></param>
        /// <param name="filterIndex">default filter index when filter have multiple values</param>
        /// <param name="checkFileExists"></param>
        /// <param name="owner">The window that owns the dialog. If not specified, attempts to find the active window automatically.</param>
        /// <returns></returns>
        public static string[]? OpenFiles(string? title = null, string? initialDirectory = null, string? filter = null, string? currentDirectoryForShowingRelativePath = null, int filterIndex = -1, bool checkFileExists = true, Window? owner = null)
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = checkFileExists,
                DereferenceLinks = true,
                Multiselect = true,
                ValidateNames = true,
            };
            if (filter != null)
            {
                dlg.Filter = filter;
                if (filterIndex >= 0)
                    dlg.FilterIndex = filterIndex;
            }
            if (initialDirectory != null)
                dlg.InitialDirectory = initialDirectory;
            if (title != null)
                dlg.Title = title;

            var ownerWindow = owner ?? GetActiveWindow();
            if (ownerWindow != null && dlg.ShowDialog(ownerWindow) != true) return null;
            else if (ownerWindow == null && dlg.ShowDialog() != true) return null;

            if (currentDirectoryForShowingRelativePath != null)
            {
                var ret = dlg.FileNames.ToArray();
                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = ret[i].Replace(currentDirectoryForShowingRelativePath, ".");
                }
                return ret;
            }
            return dlg.FileNames;
        }

        public static void OpenInExplorer(string dirPath)
        {
            Shawn.Utils.Wpf.FileSystem.SelectFileHelper.OpenInExplorer(dirPath);
        }

        public static void OpenInExplorerAndSelect(string path)
        {
            Shawn.Utils.Wpf.FileSystem.SelectFileHelper.OpenInExplorerAndSelect(path);
        }

        /// <summary>
        /// Gets the currently active window from the application.
        /// Returns the topmost window or the main window if available.
        /// </summary>
        /// <returns>The active window, or null if no window is found</returns>
        private static Window? GetActiveWindow()
        {
            try
            {
                if (Application.Current == null)
                    return null;

                // Try to get the active window
                var activeWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive);
                
                if (activeWindow != null)
                    return activeWindow;

                // If no active window, get the topmost window
                var topmostWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.Topmost);
                
                if (topmostWindow != null)
                    return topmostWindow;

                // Fallback to main window
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
                    return Application.Current.MainWindow;

                // Last resort: return the first loaded window
                return Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsLoaded);
            }
            catch
            {
                return null;
            }
        }
    }
}
