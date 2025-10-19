using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.IO;

namespace RandomGamePicker.Converters
{
    public class FileIconConverter : IValueConverter
    {
        // Cache: path -> ImageSource
        private static readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            // Use cache
            if (_cache.TryGetValue(path, out var cached))
                return cached;

            try
            {
                // For .lnk try to resolve the target if possible
                string iconPath = path;
                var ext = Path.GetExtension(path);
#if USE_WSH
                if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var shell = new WshShell();
                        var lnk = (IWshShortcut)shell.CreateShortcut(path);
                        if (!string.IsNullOrWhiteSpace(lnk.IconLocation))
                        {
                            // IconLocation may be "file,index"
                            var parts = lnk.IconLocation.Split(',');
                            if (File.Exists(parts[0])) iconPath = parts[0];
                        }
                        else if (!string.IsNullOrWhiteSpace(lnk.TargetPath) && File.Exists(lnk.TargetPath))
                        {
                            iconPath = lnk.TargetPath;
                        }
                    }
                    catch { /* fallback below */ }
                }
#endif
                var img = GetLargeIconAsImageSource(iconPath);
                _cache[path] = img;
                return img;
            }
            catch
            {
                _cache[path] = null;
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

        // --- Native icon extraction via SHGetFileInfo (fast & reliable) ---
        private static ImageSource? GetLargeIconAsImageSource(string existingPath)
        {
            var flags = SHGFI_ICON | SHGFI_LARGEICON;
            var res = SHGetFileInfo(existingPath, 0, out var shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze(); // for cross-thread use/caching
                return src;
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }

        #region P/Invoke
        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000; // 32x32
        // private const uint SHGFI_SMALLICON = 0x000000001; // 16x16 if you prefer

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        #endregion
    }
}
