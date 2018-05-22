using System;
using System.Collections.Generic;
using System.Threading;

using Spi.Native;
using Spi.Data;
using Spi;
using System.Text;

namespace CmpTrees
{
    class CmpDirs
    {
        public static void Run(StringBuilder dira, StringBuilder dirb, Action<DIFF_STATE, Win32.FIND_DATA, Win32.FIND_DATA> DiffCallback, ErrorHandler errorHandler)
        {
            IEnumerable<Win32.FIND_DATA> sortedItemsA = EnumDir.Entries(dira, errorHandler);
            IEnumerable<Win32.FIND_DATA> sortedItemsB = EnumDir.Entries(dirb, errorHandler);

            Spi.Data.DiffSortedLists.Run<Win32.FIND_DATA>(
                ListA: sortedItemsA,
                ListB: sortedItemsB,
                checkSortOrder: true,
                ReportSameSame: true,
                OnCompared: DiffCallback,
                KeyComparer: (a,b) =>
                {
                    int cmp = String.Compare(a.cFileName, b.cFileName, StringComparison.OrdinalIgnoreCase);
                    if ( cmp != 0 )
                    {
                        return cmp;
                    }
                    bool aIsDir = Spi.Misc.IsDirectoryFlagSet(a.dwFileAttributes);
                    bool bIsDir = Spi.Misc.IsDirectoryFlagSet(b.dwFileAttributes);

                    return aIsDir == bIsDir 
                                ?  0    // two directories OR two files --> same name --> return 0 
                                : -1;   // one dir AND one file         --> same name --> return -1 to represent the difference
                },
                AttributeComparer: (a,b) =>
                {
                    if ( Misc.IsDirectoryFlagSet(a) && Misc.IsDirectoryFlagSet(b) )
                    {
                        return 0;
                    }
                    long cmp;
                    if ((cmp = CmpFileTimes(a.ftLastWriteTime, b.ftLastWriteTime)) != 0)
                    {
                        return (int)cmp;
                    }
                    if ((cmp = (long)(a.FileSize - b.FileSize) ) != 0)
                    {
                        return (int)cmp;
                    }

                    return 0;
                });
        }
        public static long CmpFileTimes(System.Runtime.InteropServices.ComTypes.FILETIME a, System.Runtime.InteropServices.ComTypes.FILETIME b)
        {
            return Spi.Misc.FiletimeToLong(a) - Spi.Misc.FiletimeToLong(b);
        }
    }
}
