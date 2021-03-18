using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace Shawn.Utils
{
    public class SystemIconHelper
    {
        #region SHGetFileInfo

        //Struct used by SHGetFileInfo function
        [StructLayout(LayoutKind.Sequential)]
        protected struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        //Import SHGetFileInfo function
        [DllImport("shell32.dll")]
        protected static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi,
            uint cbSizeFileInfo, uint uFlags);

        [DllImport("User32.dll", EntryPoint = "DestroyIcon")]
        public static extern int DestroyIcon(IntPtr hIcon);

        #endregion SHGetFileInfo

        public static Bitmap GetFolderIcon(string path)
        {
            //Constants flags for SHGetFileInfo
            const uint SHGFI_ICON = 0x100;
            const uint SHGFI_LARGEICON = 0x0; // 'Large icon
            if (Directory.Exists(path))
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                SHGetFileInfo(
                    path,
                    0, ref shinfo, (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_LARGEICON);

                using (Icon i = System.Drawing.Icon.FromHandle(shinfo.hIcon))
                {
                    return i.ToBitmap();
                    ////Convert icon to a Bitmap source
                    //var img = Imaging.CreateBitmapSourceFromHIcon(
                    //    i.Handle,
                    //    new Int32Rect(0, 0, i.Width, i.Height),
                    //    BitmapSizeOptions.FromEmptyOptions());
                }
            }

            return null;
        }

        public static Bitmap GetFileIcon(string path)
        {
            if (File.Exists(path))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                var bmp = icon.ToBitmap();
                return bmp;
            }

            return null;
        }

        public enum FileInfoFlags : uint
        {
            SHGFI_ICON = 0x000000100, //  get icon
            SHGFI_DISPLAYNAME = 0x000000200, //  get display name
            SHGFI_TYPENAME = 0x000000400, //  get type name
            SHGFI_ATTRIBUTES = 0x000000800, //  get attributes
            SHGFI_ICONLOCATION = 0x000001000, //  get icon location
            SHGFI_EXETYPE = 0x000002000, //  return exe type
            SHGFI_SYSICONINDEX = 0x000004000, //  get system icon index
            SHGFI_LINKOVERLAY = 0x000008000, //  put a link overlay on icon
            SHGFI_SELECTED = 0x000010000, //  show icon in selected state
            SHGFI_ATTR_SPECIFIED = 0x000020000, //  get only specified attributes
            SHGFI_LARGEICON = 0x000000000, //  get large icon
            SHGFI_SMALLICON = 0x000000001, //  get small icon
            SHGFI_OPENICON = 0x000000002, //  get open icon
            SHGFI_SHELLICONSIZE = 0x000000004, //  get shell size icon
            SHGFI_PIDL = 0x000000008, //  pszPath is a pidl
            SHGFI_USEFILEATTRIBUTES = 0x000000010, //  use passed dwFileAttribute
            SHGFI_ADDOVERLAYS = 0x000000020, //  apply the appropriate overlays
            SHGFI_OVERLAYINDEX = 0x000000040 //  Get the index of the overlay
        }

        public enum FileAttributeFlags : uint
        {
            FILE_ATTRIBUTE_READONLY = 0x00000001,
            FILE_ATTRIBUTE_HIDDEN = 0x00000002,
            FILE_ATTRIBUTE_SYSTEM = 0x00000004,
            FILE_ATTRIBUTE_DIRECTORY = 0x00000010,
            FILE_ATTRIBUTE_ARCHIVE = 0x00000020,
            FILE_ATTRIBUTE_DEVICE = 0x00000040,
            FILE_ATTRIBUTE_NORMAL = 0x00000080,
            FILE_ATTRIBUTE_TEMPORARY = 0x00000100,
            FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200,
            FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400,
            FILE_ATTRIBUTE_COMPRESSED = 0x00000800,
            FILE_ATTRIBUTE_OFFLINE = 0x00001000,
            FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000,
            FILE_ATTRIBUTE_ENCRYPTED = 0x00004000
        }

        /// <summary>
        /// 获取文件夹图标
        /// </summary>
        /// <returns>图标</returns>
        public static Bitmap GetFolderIcon(string path = "", bool isLargeIcon = false)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                return GetFolderIcon(path);
            var shfi = new SHFILEINFO();
            IntPtr iconIntPtr;
            if (isLargeIcon)
                iconIntPtr = SHGetFileInfo(@"", 0, ref shfi, (uint)Marshal.SizeOf(shfi),
                    ((uint)FileInfoFlags.SHGFI_ICON | (uint)FileInfoFlags.SHGFI_LARGEICON));
            else
                iconIntPtr = SHGetFileInfo(@"", 0, ref shfi, (uint)Marshal.SizeOf(shfi),
                    ((uint)FileInfoFlags.SHGFI_ICON | (uint)FileInfoFlags.SHGFI_SMALLICON));
            if (iconIntPtr.Equals(IntPtr.Zero)) return null;
            var icon = System.Drawing.Icon.FromHandle(shfi.hIcon).Clone() as Icon;
            DestroyIcon(shfi.hIcon);
            var bmp = icon?.ToBitmap();
            return bmp;
        }

        /// <summary>
        /// 获取文件类型的关联图标，
        /// var icon = SystemIconHelper.GetFileIconByExt(".jpg");
        /// </summary>
        /// <param name="fileName">文件类型的扩展名或文件的绝对路径</param>
        /// <param name="isLargeIcon">是否返回大图标</param>
        /// <returns>获取到的图标</returns>
        public static Bitmap GetFileIconByExt(string fileName, bool isLargeIcon = false)
        {
            var shfi = new SHFILEINFO();
            IntPtr iconIntPtr;

            if (isLargeIcon)
                iconIntPtr = SHGetFileInfo(fileName, 0, ref shfi, (uint)Marshal.SizeOf(shfi),
                    (uint)FileInfoFlags.SHGFI_ICON | (uint)FileInfoFlags.SHGFI_USEFILEATTRIBUTES |
                    (uint)FileInfoFlags.SHGFI_LARGEICON);
            else
                iconIntPtr = SHGetFileInfo(fileName, 0, ref shfi, (uint)Marshal.SizeOf(shfi),
                    (uint)FileInfoFlags.SHGFI_ICON | (uint)FileInfoFlags.SHGFI_USEFILEATTRIBUTES |
                    (uint)FileInfoFlags.SHGFI_SMALLICON);

            var icon = Icon.FromHandle(shfi.hIcon).Clone() as Icon;

            DestroyIcon(shfi.hIcon);
            var bmp = icon?.ToBitmap();
            return bmp;
        }

        public static Bitmap GetIcon(string path)
        {
            if (Directory.Exists(path))
            {
                return GetFolderIcon(path);
            }
            else if (File.Exists(path))
            {
                return GetFileIcon(path);
            }
            else
                return GetFileIconByExt(path, false);
        }
    }
}