using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;


using Spi.Native;
using System.Text;

namespace CmpTrees
{
    public class EnumDir
    {
        public static IEnumerable<Spi.Native.Win32.FIND_DATA> Entries(StringBuilder FullDirname, ErrorHandler errorHandler)
        {
            FullDirname.Append("\\*");
            using (SafeFindHandle SearchHandle = Win32.FindFirstFile(FullDirname.ToString(), out Win32.FIND_DATA find_data))
            {
                FullDirname.Length -= 2;
                if (SearchHandle.IsInvalid)
                {
                    int LastWinError = Marshal.GetLastWin32Error();
                    if (LastWinError == Win32.ERROR_PATH_NOT_FOUND || LastWinError == Win32.ERROR_DIRECTORY)
                    {
                        yield break;
                    }
                    else
                    {
                        errorHandler?.Invoke(LastWinError, "FindFirstFile, " + FullDirname);
                    }
                }
                else
                {
                    do
                    {
                        if (Spi.Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                        {
                            continue;
                        }
                        yield return find_data;
                    }
                    while (Win32.FindNextFile(SearchHandle, out find_data));

                    if (Marshal.GetLastWin32Error() != Win32.ERROR_NO_MORE_FILES)
                    {
                        errorHandler?.Invoke(Marshal.GetLastWin32Error(), "FindNextFile, " + FullDirname);
                    }
                }
            }
        }
        public static IEnumerable<Spi.Native.Win32.FIND_DATA> Entries_IntPtr(StringBuilder FullDirname, ErrorHandler errorHandler)
        {
            Win32.FIND_DATA find_data = new Win32.FIND_DATA();
            FullDirname.Append("\\*");

            IntPtr SearchHandle = IntPtr.Zero;
            try
            {
                SearchHandle = Win32.FindFirstFile_IntPtr(FullDirname.ToString(), ref find_data);
                FullDirname.Length -= 2;

                if (SearchHandle == Win32.INVALID_HANDLE_VALUE)
                {
                    int LastWinError = Marshal.GetLastWin32Error();
                    if (LastWinError == Win32.ERROR_PATH_NOT_FOUND || LastWinError == Win32.ERROR_DIRECTORY)
                    {
                        yield break;
                    }
                    else
                    {
                        errorHandler?.Invoke(LastWinError, "FindFirstFile, " + FullDirname);
                    }
                }
                else
                {
                    do
                    {
                        if (Spi.Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                        {
                            continue;
                        }
                        yield return find_data;
                    }
                    while (Win32.FindNextFile_IntPtr(SearchHandle, ref find_data));

                    if (Marshal.GetLastWin32Error() != Win32.ERROR_NO_MORE_FILES)
                    {
                        errorHandler?.Invoke(Marshal.GetLastWin32Error(), "FindNextFile, " + FullDirname);
                    }
                }
            }
            finally
            {
                if (SearchHandle != IntPtr.Zero)
                {
                    Win32.FindClose_IntPtr(SearchHandle);
                }
            }
        }

        public static IEnumerable<Spi.Native.Win32.FIND_DATA> EntriesEx(StringBuilder FullDirname, ErrorHandler errorHandler)
        {
            Win32.FIND_DATA find_data = new Win32.FIND_DATA();
            FullDirname.Append("\\*");

            IntPtr SearchHandle = IntPtr.Zero;
            try
            {
                SearchHandle = Win32.FindFirstFileEx(
                    FullDirname.ToString()
                    , Win32.FINDEX_INFO_LEVELS.FindExInfoBasic
                    , ref find_data
                    , Win32.FINDEX_SEARCH_OPS.FindExSearchNameMatch
                    , IntPtr.Zero
                    , Win32.FINDEX_ADDITIONAL_FLAGS.FIND_FIRST_EX_LARGE_FETCH);
                FullDirname.Length -= 2;

                if (SearchHandle == Win32.INVALID_HANDLE_VALUE)
                {
                    int LastWinError = Marshal.GetLastWin32Error();
                    if (LastWinError == Win32.ERROR_PATH_NOT_FOUND || LastWinError == Win32.ERROR_DIRECTORY)
                    {
                        yield break;
                    }
                    else
                    {
                        errorHandler?.Invoke(LastWinError, "FindFirstFile, " + FullDirname);
                    }
                }
                else
                {
                    do
                    {
                        if (Spi.Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                        {
                            continue;
                        }
                        yield return find_data;
                    }
                    while (Win32.FindNextFile_IntPtr(SearchHandle, ref find_data));

                    if (Marshal.GetLastWin32Error() != Win32.ERROR_NO_MORE_FILES)
                    {
                        errorHandler?.Invoke(Marshal.GetLastWin32Error(), "FindNextFile, " + FullDirname);
                    }
                }
            }
            finally
            {
                if (SearchHandle != IntPtr.Zero)
                {
                    Win32.FindClose_IntPtr(SearchHandle);
                }
            }
        }
    }
}