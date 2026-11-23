using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlessedLine.Desktop;

internal static class ApplicationIcon
{
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
