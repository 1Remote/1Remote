using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Shawn.Utils
{
    public static class SelectFileHelper
    {
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
        /// <returns></returns>
        public static string OpenFile(string title = null, string selectedFileName = null, string initialDirectory = null, string filter = null, string currentDirectoryForShowingRelativePath = null, int filterIndex = -1, bool checkFileExists = true)
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
            if (initialDirectory != null)
                dlg.InitialDirectory = initialDirectory;
            if (title != null)
                dlg.Title = title;
            if (selectedFileName != null)
                dlg.FileName = selectedFileName;

            if (dlg.ShowDialog() != true) return null;

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
        /// <returns></returns>
        public static string SaveFile(string title = null, string selectedFileName = null, string initialDirectory = null, string filter = null, string currentDirectoryForShowingRelativePath = null, int filterIndex = -1, bool checkFileExists = false)
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
            if (initialDirectory != null)
                dlg.InitialDirectory = initialDirectory;
            if (title != null)
                dlg.Title = title;
            if (selectedFileName != null)
                dlg.FileName = selectedFileName;

            if (dlg.ShowDialog() != true) return null;

            return currentDirectoryForShowingRelativePath != null ? dlg.FileName.Replace(currentDirectoryForShowingRelativePath, ".") : dlg.FileName;
        }


        /// <summary>
        /// pop a file select dialog and return file path or null
        /// </summary>
        /// <param name="initialDirectory"></param>
        /// <param name="filter">e.g. txt files (*.txt)|*.txt|All files (*.*)|*.*</param>
        /// <param name="currentDirectoryForShowingRelativePath"></param>
        /// <param name="filterIndex">default filter index when filter have multiple values</param>
        /// <param name="checkFileExists"></param>
        /// <returns></returns>
        public static string[] OpenFiles
            (string title = null, string initialDirectory = null, string filter = null, string currentDirectoryForShowingRelativePath = null, int filterIndex = -1, bool checkFileExists = true)
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

            if (dlg.ShowDialog() != true) return null;

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
    }
}
