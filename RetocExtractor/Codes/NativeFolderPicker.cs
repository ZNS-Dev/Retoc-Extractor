using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RetocExtractor.RetocExtractor.Codes
{

    internal static class NativeFolderPicker
    {
        private const uint FosNoChangeDir = 0x00000008;
        private const uint FosPickFolders = 0x00000020;
        private const uint FosForceFileSystem = 0x00000040;
        private const uint FosPathMustExist = 0x00000800;
        private const uint SigdnFileSystemPath = 0x80058000;
        private const int HResultCancelled = unchecked((int)0x800704C7);

        private static readonly Guid ShellItemInterfaceId =
            new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        private static readonly Guid DialogClientId =
            new("9A276A2D-1D4A-4B2D-9B4F-6B0C9ED9A2F1");

        public static string Show(IWin32Window owner, string title, string initialFolder)
        {
            IFileOpenDialog dialog = null;
            IShellItem initialItem = null;
            IShellItem selectedItem = null;
            IntPtr pathPointer = IntPtr.Zero;

            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialogComObject();

                Check(dialog.GetOptions(out uint options));
                options |= FosPickFolders | FosForceFileSystem | FosPathMustExist | FosNoChangeDir;
                Check(dialog.SetOptions(options));
                Check(dialog.SetTitle(title ?? "Select folder"));
                Check(dialog.SetOkButtonLabel("Select folder"));
                Guid clientGuid = DialogClientId;
                Check(dialog.SetClientGuid(ref clientGuid));

                if (!string.IsNullOrWhiteSpace(initialFolder))
                {
                    Guid shellItemIid = ShellItemInterfaceId;
                    int createResult = SHCreateItemFromParsingName(
                        initialFolder,
                        IntPtr.Zero,
                        ref shellItemIid,
                        out initialItem);

                    if (createResult >= 0 && initialItem != null)
                        dialog.SetFolder(initialItem);
                }

                IntPtr ownerHandle = owner == null ? IntPtr.Zero : owner.Handle;
                int showResult = dialog.Show(ownerHandle);
                if (showResult == HResultCancelled)
                    return null;
                Check(showResult);

                Check(dialog.GetResult(out selectedItem));
                Check(selectedItem.GetDisplayName(SigdnFileSystemPath, out pathPointer));
                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                if (pathPointer != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pathPointer);
                ReleaseComObject(selectedItem);
                ReleaseComObject(initialItem);
                ReleaseComObject(dialog);
            }
        }

        private static void Check(int hResult)
        {
            if (hResult < 0)
                Marshal.ThrowExceptionForHR(hResult);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try { Marshal.FinalReleaseComObject(value); }
                catch { }
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            out IShellItem shellItem);

        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        [ClassInterface(ClassInterfaceType.None)]
        private class FileOpenDialogComObject
        {
        }

        // COM method order must match the native IFileDialog/IFileOpenDialog vtable.
        [ComImport]
        [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            [PreserveSig] int SetFileTypes(uint count, IntPtr filters);
            [PreserveSig] int SetFileTypeIndex(uint index);
            [PreserveSig] int GetFileTypeIndex(out uint index);
            [PreserveSig] int Advise(IntPtr events, out uint cookie);
            [PreserveSig] int Unadvise(uint cookie);
            [PreserveSig] int SetOptions(uint options);
            [PreserveSig] int GetOptions(out uint options);
            [PreserveSig] int SetDefaultFolder([MarshalAs(UnmanagedType.Interface)] IShellItem folder);
            [PreserveSig] int SetFolder([MarshalAs(UnmanagedType.Interface)] IShellItem folder);
            [PreserveSig] int GetFolder(out IShellItem folder);
            [PreserveSig] int GetCurrentSelection(out IShellItem item);
            [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            [PreserveSig] int GetFileName(out IntPtr name);
            [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            [PreserveSig] int GetResult(out IShellItem item);
            [PreserveSig] int AddPlace([MarshalAs(UnmanagedType.Interface)] IShellItem item, uint location);
            [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            [PreserveSig] int Close(int hResult);
            [PreserveSig] int SetClientGuid(ref Guid guid);
            [PreserveSig] int ClearClientData();
            [PreserveSig] int SetFilter(IntPtr filter);
            [PreserveSig] int GetResults(out IntPtr results);
            [PreserveSig] int GetSelectedItems(out IntPtr items);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig] int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
            [PreserveSig] int GetParent(out IShellItem parent);
            [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr displayName);
            [PreserveSig] int GetAttributes(uint mask, out uint attributes);
            [PreserveSig] int Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);
        }
    }
}
