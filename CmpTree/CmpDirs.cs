using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Spi.Native;
using Spi.Data;

namespace CmpTrees
{
    class CmpDirs
    {
        public static void Run(string dira, string dirb, Action<DIFF_STATE, Win32.WIN32_FIND_DATA, Win32.WIN32_FIND_DATA> DiffCallback, ErrorHandler errorHandler, ManualResetEvent Cancel)
        {
            IEnumerable<Win32.WIN32_FIND_DATA> itemsA = EnumDir.Entries(dira, errorHandler, Cancel);
            IEnumerable<Win32.WIN32_FIND_DATA> itemsB = EnumDir.Entries(dirb, errorHandler, Cancel);

            Spi.Data.Diff.DiffSortedEnumerables(
                ListA: itemsA,
                ListB: itemsB,
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
