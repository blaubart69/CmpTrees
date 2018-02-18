using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;


using Spi.Native;

namespace CmpTrees
{
    public class EnumDir
    {
        public static IEnumerable<Spi.Native.Win32.WIN32_FIND_DATA> Entries(string FullDirname, ErrorHandler errorHandler, ManualResetEvent Cancel)
        {
            using (SafeFindHandle SearchHandle = Win32.FindFirstFile(FullDirname + "\\*", out Win32.WIN32_FIND_DATA find_data))
            {
                if (SearchHandle.IsInvalid)
                {
                    int LastWinError = Marshal.GetLastWin32Error();
                    if (LastWinError == Win32.ERROR_PATH_NOT_FOUND)
                    {
                        yield break;
                    }
                    else
                    {
                        errorHandler?.Invoke(LastWinError, FullDirname);
                    }
                }
                else
                {
                    do
                    {
                        if (Cancel.WaitOne(0))
                        {
                            yield break;
                        }
                        if (Spi.Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                        {
                            continue;
                        }
                        yield return find_data;
                    }
                    while (Win32.FindNextFile(SearchHandle, out find_data));
                }
            }
        }
    }
}
