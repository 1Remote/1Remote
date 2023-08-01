﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _1RM.Utils.Windows
{
    /// <summary>
    ///  https://stackoverflow.com/questions/2819934/detect-windows-version-in-net
    /// +------------------------------------------------------------------------------+
    /// |                    |   PlatformID    |   Major version   |      osBuild      |
    /// +------------------------------------------------------------------------------+
    /// | Windows 95         |  Win32Windows   |         4         |                   |
    /// | Windows 98         |  Win32Windows   |         4         |                   |
    /// | Windows Me         |  Win32Windows   |         4         |                   |
    /// | Windows NT 4.0     |  Win32NT        |         4         |                   |
    /// | Windows 2000       |  Win32NT        |         5         |                   |
    /// | Windows XP         |  Win32NT        |         5         |                   |
    /// | Windows 2003       |  Win32NT        |         5         |                   |
    /// | Windows Vista      |  Win32NT        |         6         |                   |
    /// | Windows 2008       |  Win32NT        |         6         |                   |
    /// | Windows 7          |  Win32NT        |         6         |                   |
    /// | Windows 2008 R2    |  Win32NT        |         6         |                   |
    /// | Windows 8          |  Win32NT        |         6         |                   |
    /// | Windows 8.1        |  Win32NT        |         6         |                   |
    /// +------------------------------------------------------------------------------+
    /// | Windows 10         |  Win32NT        |        10         |          0        |
    /// | Windows 10 1909    |  Win32NT        |        10         |       18363       |
    /// | Windows 10 2004    |  Win32NT        |        10         |       19041       |
    /// | Windows 10 20H2    |  Win32NT        |        10         |       19042       |
    /// | Windows 10 21H2    |  Win32NT        |        10         |       19043       |
    /// | Windows 11         |  Win32NT        |        10         |       22000       |
    /// | Windows 11 22H2    |  Win32NT        |        10         |       22600       |
    /// +------------------------------------------------------------------------------+
    /// </summary>
    public static class WindowsVersionHelper
    {
        // disabled for OS under Win10
        public static bool IsWindows7OrLower()
        {
            int versionMajor = Environment.OSVersion.Version.Major;
            int versionMinor = Environment.OSVersion.Version.Minor;
            double version = versionMajor + (double)versionMinor / 10;
            return version <= 6.1;
        }
        public static bool IsLowerThanWindows10()
        {
            return Environment.OSVersion.Version.Major < 10;
        }
        public static bool IsWindows11OrHigher()
        {
            int versionMajor = Environment.OSVersion.Version.Major;
            int versionMinor = Environment.OSVersion.Version.Minor;
            double version = versionMajor + (double)versionMinor / 10;
            return version <= 6.1;
        }
        public static bool IsWindows1122H2OrHigher()
        {
            return Environment.OSVersion.Version.Build >= 22600; // Win11 22H2
        }
    }
}
