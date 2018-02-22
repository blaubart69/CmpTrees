using System;
using System.Collections.Generic;
using System.Threading;

using Spi.Native;
using Spi.Data;
using Spi;

namespace CmpTrees
{
    internal class Win32FinddataComparer : IComparer<Win32.WIN32_FIND_DATA>
    {
        public int Compare(Win32.WIN32_FIND_DATA a, Win32.WIN32_FIND_DATA b)
        {
            return String.CompareOrdinal(a.cFileName, b.cFileName);
        }
    }

    //public delegate void DiffCallbackHandler(DIFF_STATE state, ref Win32.WIN32_FIND_DATA a, ref Win32.WIN32_FIND_DATA b);
    class CmpDirs
    {
        static Win32FinddataComparer finddataComparer = new Win32FinddataComparer();

        public static void Run(string dira, string dirb, Action<DIFF_STATE, Win32.WIN32_FIND_DATA, Win32.WIN32_FIND_DATA> DiffCallback, ErrorHandler errorHandler, ManualResetEvent Cancel)
        {
            /*
            CalcSortedDirectoryList(
                EnumDir.Entries(dira, errorHandler, Cancel), 
                EnumDir.Entries(dirb, errorHandler, Cancel), 
                out List<Win32.WIN32_FIND_DATA> sortedItemsA, 
                out List<Win32.WIN32_FIND_DATA> sortedItemsB);
            */
            IEnumerable<Win32.WIN32_FIND_DATA> sortedItemsA = EnumDir.Entries(dira, errorHandler, Cancel);
            IEnumerable<Win32.WIN32_FIND_DATA> sortedItemsB = EnumDir.Entries(dirb, errorHandler, Cancel);

            if ( Cancel.WaitOne(0) )
            {
                return;
            }

            Spi.Data.Diff.DiffSortedEnumerables(
                ListA: sortedItemsA,
                ListB: sortedItemsB,
                checkSortOrder: true,
                KeyComparer: (a,b) =>
                {
                    int cmp = String.Compare(a.cFileName, b.cFileName, StringComparison.OrdinalIgnoreCase);
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
                    if ((cmp = (long)(Misc.GetFilesize(a) - Misc.GetFilesize(b)) ) != 0)
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
        /*
        private static void CalcSortedDirectoryList(IEnumerable<Win32.WIN32_FIND_DATA> entriesA, IEnumerable<Win32.WIN32_FIND_DATA> entriesB, out List<Win32.WIN32_FIND_DATA> sortedEntriesA, out List<Win32.WIN32_FIND_DATA> sortedEntriesB)
        {
            using (ManualResetEvent finishedA = new ManualResetEvent(false))
            {
                Exception sortAException = null;

                List<Win32.WIN32_FIND_DATA> TMP_sortedEntriesA = null;
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        GetSortedDirectoryList(entriesA, out TMP_sortedEntriesA);
                    }
                    catch (Exception ex)
                    {
                        sortAException = ex;
                    }
                    finally
                    {
                        finishedA.Set();
                    }
                });

                GetSortedDirectoryList(entriesB, out sortedEntriesB);
                finishedA.WaitOne();

                if (sortAException != null)
                {
                    throw sortAException;
                }

                sortedEntriesA = TMP_sortedEntriesA;
            }
        }
        private static void GetSortedDirectoryList(IEnumerable<Win32.WIN32_FIND_DATA> entries, out List<Win32.WIN32_FIND_DATA> sortedEntries)
        {
            sortedEntries = new List<Win32.WIN32_FIND_DATA>();
            foreach (Win32.WIN32_FIND_DATA finddata in entries )
            {
                Spi.ListExtension.AddSorted(sortedEntries, finddata, finddataComparer);
            }
        }
        */
    }
}
