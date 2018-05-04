using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Spi.Native;

namespace CmpTrees
{
    public class FindDataNameComparer : IComparer<Win32.WIN32_FIND_DATA>
    {
        public int Compare(Win32.WIN32_FIND_DATA x, Win32.WIN32_FIND_DATA y)
        {
            return String.Compare(x.cFileName,y.cFileName);   
        }
    }
    public class MoveDetector
    {
        public static void Run(SortedList<Win32.WIN32_FIND_DATA, List<string>> newFiles, SortedList<Win32.WIN32_FIND_DATA, List<string>> delFiles)
        {
            Spi.Data.DiffSortedLists.Run( delFiles, newFiles,
                KeyComparer: (del, neu) => )
        }
    }
}
