using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Spi.Native;
using Spi.Data;
using Spi;

namespace CmpTrees
{
    //public delegate void DiffCallbackHandler(DIFF_STATE state, ref Win32.WIN32_FIND_DATA a, ref Win32.WIN32_FIND_DATA b);
    class CmpDirs
    {
        public static void Run(string dira, string dirb, Action<DIFF_STATE, Win32.WIN32_FIND_DATA, Win32.WIN32_FIND_DATA> DiffCallback, ErrorHandler errorHandler, ManualResetEvent Cancel)
        {
            IEnumerable<Win32.WIN32_FIND_DATA> itemsA = EnumDir.Entries(dira, errorHandler, Cancel).OrderBy(i => i.cFileName, StringComparer.Ordinal);
            IEnumerable<Win32.WIN32_FIND_DATA> itemsB = EnumDir.Entries(dirb, errorHandler, Cancel).OrderBy(i => i.cFileName, StringComparer.Ordinal);

            Spi.Data.Diff.DiffSortedEnumerables(
                ListA: itemsA,
                ListB: itemsB,
                checkSortOrder: true,
                KeyComparer: (a,b) =>
                {
                    int cmp = String.CompareOrdinal(a.cFileName, b.cFileName);
                    if ( cmp != 0 )
                    {
                        return cmp;
                    }
                    bool aIsDir = Spi.Misc.IsDirectoryFlagSet(a.dwFileAttributes);
                    bool bIsDir = Spi.Misc.IsDirectoryFlagSet(b.dwFileAttributes);

                    return aIsDir == bIsDir ? 0 : -1;  // MAGIC VALUE!!!! :-D
                },
                AttributeComparer: (a,b) =>
                {
                    if ( Misc.IsDirectoryFlagSet(a) && Misc.IsDirectoryFlagSet(b) )
                    {
                        return 0;
                    }
                    long cmp;
                    if ((cmp = CmpDirs.CmpFileTimes(a.ftLastWriteTime, b.ftLastWriteTime)) != 0)
                    {
                        return (int)cmp;
                    }

                    return 0;
                },
                OnCompared: DiffCallback);
        }
        public static long CmpFileTimes(System.Runtime.InteropServices.ComTypes.FILETIME a, System.Runtime.InteropServices.ComTypes.FILETIME b)
        {
            return Spi.Misc.FiletimeToLong(a) - Spi.Misc.FiletimeToLong(b);
        }
    }
}
