using System;
using System.Runtime.InteropServices;

namespace HowToHunt.Avatar
{
    /// <summary>
    /// The Windows "Open" dialog, to pick a picture for the mask (owner, 2026-11-25: import an image onto your mask).
    /// Blocks until closed (the game waits behind it). Null when cancelled, or off Windows.
    /// </summary>
    public static class ImageFileDialog
    {
        public const string Filter = "Pictures (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0All files\0*.*\0\0";
        private const int MaxPath = 1024;
        private const int OfnFileMustExist = 0x00001000, OfnPathMustExist = 0x00000800, OfnNoChangeDir = 0x00000008, OfnExplorer = 0x00080000;

        public static bool Supported =>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            true;
#else
            false;
#endif

        public static string Open(string title)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.hwndOwner = GetActiveWindow();
            ofn.lpstrFilter = Filter;
            ofn.lpstrFile = new string('\0', MaxPath);
            ofn.nMaxFile = MaxPath;
            ofn.lpstrTitle = title;
            ofn.Flags = OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnExplorer;
            try
            {
                if (!GetOpenFileName(ofn)) return null;
            }
            catch (Exception)
            {
                return null; // no common dialogs on this machine
            }
            string path = ofn.lpstrFile;
            int end = path.IndexOf('\0');
            return end >= 0 ? path.Substring(0, end) : path;
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
#endif
    }
}
