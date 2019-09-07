using System;
using System.Collections.Generic;
using System.Linq;

using Spi.Native;
using Spi.Data;
using Spi;
using System.Text;

namespace CmpTrees
{
    class CmpDirs
    {
        /// <summary>
        /// A ... target directory
        /// B ... source directory
        /// </summary>
        public static void Run(StringBuilder sourceDir, StringBuilder targetDir, Action<DIFF_STATE, Win32.FIND_DATA, Win32.FIND_DATA> DiffCallback, 
            bool forceSortSource, bool forceSortTarget, ErrorHandler errorHandler)
        {
            IEnumerable<Win32.FIND_DATA> sortedItemsTrg = EnumDir.Entries_IntPtr(targetDir, errorHandler);
            IEnumerable<Win32.FIND_DATA> sortedItemsSrc = EnumDir.Entries_IntPtr(sourceDir, errorHandler);

            if ( forceSortTarget)
            {
                sortedItemsTrg = sortedItemsTrg.OrderBy(keySelector: k => k.cFileName, comparer: StringComparer.OrdinalIgnoreCase);
            }

            if (forceSortSource)
            {
                sortedItemsSrc = sortedItemsSrc.OrderBy(keySelector: k => k.cFileName, comparer: StringComparer.OrdinalIgnoreCase);
            }
            try
            {
                Spi.Data.DiffSortedLists.Run<Win32.FIND_DATA>(
                    ListA: sortedItemsTrg,
                    ListB: sortedItemsSrc,
                    checkSortOrder: true,
                    ReportSameSame: true,
                    OnCompared: (state, a, b) =>
                    {
                        DiffCallback(state, b, a);
                    },
                    KeyComparer: (a, b) =>
                    {
                        int cmp = String.Compare(a.cFileName, b.cFileName, StringComparison.OrdinalIgnoreCase);
                        if (cmp == 0)
                        {
                            bool aIsDir = Spi.Misc.IsDirectory(a.dwFileAttributes);
                            bool bIsDir = Spi.Misc.IsDirectory(b.dwFileAttributes);

                            cmp = (aIsDir == bIsDir)
                                ? 0    // two directories OR two files --> same name --> return 0 
                                : -1;  // one dir AND one file         --> same name --> return -1 to represent the difference
                    }
                        return cmp;
                    },
                    AttributeComparer: (a, b) =>
                    {
                        if (Misc.IsDirectory(a) && Misc.IsDirectory(b))
                        {
                            return 0;
                        }

                        long cmp;
                        if ((cmp = (long)(a.FileSize - b.FileSize)) != 0)
                        {
                            return (int)cmp;
                        }
                        if ((cmp = CmpFileTimes(a.ftLastWriteTime, b.ftLastWriteTime)) != 0)
                        {
                            return (int)cmp;
                        }

                        return 0;
                    });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("HINT: try using --sorts/--sortt to get the directories sorted for comparing.");
            }
        }
        public static long CmpFileTimes(System.Runtime.InteropServices.ComTypes.FILETIME a, System.Runtime.InteropServices.ComTypes.FILETIME b)
        {
            return Spi.Misc.FiletimeToLong(a) - Spi.Misc.FiletimeToLong(b);
        }
    }
}
