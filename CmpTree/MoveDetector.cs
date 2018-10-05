using System;
using System.Collections.Generic;
using System.IO;
using Spi;
using Spi.Native;

namespace CmpTrees
{
    public delegate void MoveFileHandler(string Filename, string MoveFromPath, string MoveToPath, ulong Filesize);

    public class FindDataComparer_Name_Size_Modified : IComparer<Win32.FIND_DATA>, IEqualityComparer<Win32.FIND_DATA>
    {
        public int Compare(Win32.FIND_DATA a, Win32.FIND_DATA b)
        {
            return MoveDetector.CompareFindData_Name_Size_Modified(a, b);
        }

        public bool Equals(Win32.FIND_DATA x, Win32.FIND_DATA y)
        {
            return
                x.dwFileAttributes == y.dwFileAttributes
             && EqualityComparer<System.Runtime.InteropServices.ComTypes.FILETIME>.Default.Equals(x.ftLastWriteTime, y.ftLastWriteTime)
             && x.FileSize == y.FileSize
             && x.cFileName == y.cFileName;
        }

        public int GetHashCode(Win32.FIND_DATA find_data)
        {
            var hashCode = -1111092689;

            hashCode = hashCode * -1521134295 + EqualityComparer<System.Runtime.InteropServices.ComTypes.FILETIME>.Default.GetHashCode(find_data.ftLastWriteTime);
            hashCode = hashCode * -1521134295 + find_data.FileSize.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(find_data.cFileName);
            hashCode = hashCode * -1521134295 + find_data.dwFileAttributes.GetHashCode();

            return hashCode;
        }
    }

    public class MoveDetector
    {
        public static int CompareFindData_Name_Size_Modified(Win32.FIND_DATA a, Win32.FIND_DATA b)
        {
            int cmp;
            if ((cmp = String.Compare(a.cFileName, b.cFileName)) != 0) return cmp;
            //if ((cmp = Misc.CompareULongsToInt(a.FileSize, b.FileSize)) != 0) return cmp;
            if ((cmp = Comparer<ulong>.Default.Compare(a.FileSize, b.FileSize)) != 0) return cmp;
            //if ((cmp = Misc.CmpFileTimes(a.ftCreationTime, b.ftCreationTime))   != 0) return cmp;
            if ((cmp = Misc.CmpFileTimes(a.ftLastWriteTime, b.ftLastWriteTime)) != 0) return cmp;

            return 0;
        }
        public static void Run(
            SortedList<Win32.FIND_DATA, List<string>> newFiles, 
            SortedList<Win32.FIND_DATA, List<string>> delFiles,
            TextWriter errWriter,
            MoveFileHandler moveHandler)
        {
            Spi.Data.DiffSortedLists.Run(
                ListA: newFiles,
                ListB: delFiles,
                KeyComparer: (newFile, delFile) =>
                {
                    return CompareFindData_Name_Size_Modified(newFile.Key, delFile.Key);
                },
                AttributeComparer: null,
                checkSortOrder: false,
                ReportSameSame: true,
                OnCompared: (state, newFile, delFile) =>
                {
                    if ( state != Spi.Data.DIFF_STATE.SAMESAME )
                    {
                        return;
                    }
                    if ( FileAppearInMoreDirectories(newFile.Key, newFile.Value, delFile.Key, delFile.Value, errWriter) )
                    {
                        return;
                    }
                    moveHandler(newFile.Key.cFileName, delFile.Value[0], newFile.Value[0], newFile.Key.FileSize);
                });
        }
        private static bool FileAppearInMoreDirectories(Win32.FIND_DATA newFile, List<string> newDirs, Win32.FIND_DATA delFile, List<string> delDirs, TextWriter errWriter)
        {
            if ( newDirs.Count == 1 && delDirs.Count == 1 )
            {
                return false;
            }

            WriteErrorWhenFileIsInMoreThanOneDirectory(newFile.cFileName, newDirs, "b", errWriter);
            WriteErrorWhenFileIsInMoreThanOneDirectory(delFile.cFileName, delDirs, "a", errWriter);

            return true;
        }
        private static void WriteErrorWhenFileIsInMoreThanOneDirectory(string filename, List<string> dirs, string side, TextWriter errWriter)
        {
            if (dirs.Count > 1)
            {
                string dirsToPrint = String.Join("\n", dirs);
                errWriter?.WriteLine(
                    $"list [{side}]: file appears in more than one directory with same size and timestamps [{filename}]"
                  + $"\n{dirsToPrint}");
            }
        }
    }
}
