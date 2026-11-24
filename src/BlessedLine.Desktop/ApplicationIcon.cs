using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlessedLine.Desktop;

internal static class ApplicationIcon
{
    // SIID_APPLICATION and SIID_NETWORKPC are stable Windows Shell stock identifiers.
    internal static System.Drawing.Icon Stock(int id)
    {
        var info = new StockInfo { Size = (uint)Marshal.SizeOf<StockInfo>() };
        if (SHGetStockIconInfo(id, 0x101, ref info) != 0 || info.Handle == 0)
            return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
        try { return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(info.Handle).Clone(); }
        finally { DestroyIcon(info.Handle); }
    }

    internal static ImageSource Image(System.Drawing.Icon icon)
    {
        var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        image.Freeze();
        return image;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StockInfo
    {
        public uint Size;
        public nint Handle;
        public int ImageIndex, IconIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Path;
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetStockIconInfo(int id, uint flags, ref StockInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelSize { public int Width, Height; }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageFactory
    {
        [PreserveSig] int GetImage(PixelSize size, uint flags, out nint bitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string name, nint bind, ref Guid iid, out IImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint bitmap);

    internal static ImageSource? Packaged(string appId)
    {
        var iid = typeof(IImageFactory).GUID;
        if (SHCreateItemFromParsingName(@"shell:AppsFolder\" + appId, 0, ref iid, out var factory) < 0) return null;
        try
        {
            if (factory.GetImage(new PixelSize { Width = 32, Height = 32 }, 4, out var bitmap) < 0 || bitmap == 0) return null;
            try
            {
                var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return image;
            }
            finally { DeleteObject(bitmap); }
        }
        finally { Marshal.ReleaseComObject(factory); }
    }

    public static ImageSource? Read(string? path)
    {
        if (path is null || SHGetFileInfo(path, 0, out var item, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x101) == 0)
            return null;

        if (item.Icon == 0)
            return null;

        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(item.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(20, 20));
            image.Freeze();
            return image;
        }
        finally
        {
            DestroyIcon(item.Icon);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(string path, uint fileAttributes, out ShellFileInfo info, uint infoSize, uint flags);
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);
}
