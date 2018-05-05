﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spi;
using Spi.Native;

namespace CmpTrees
{
    public delegate void MoveFileHandler(string Filename, string MoveFromPath, string MoveToPath);
    public class MoveDetector
    {
        public static void Run(SortedList<Win32.FIND_DATA, List<string>> newFiles, SortedList<Win32.FIND_DATA, List<string>> delFiles,
            ConsoleAndFileWriter errWriter,
            MoveFileHandler moveHandler)
        {
            Spi.Data.DiffSortedLists.Run(
                ListA: newFiles,
                ListB: delFiles,
                KeyComparer: (newFile, delFile) => delFile.Key.CompareTo(newFile.Key),
                AttributeComparer: null,
                checkSortOrder: false,
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
                    moveHandler(newFile.Key.cFileName, delFile.Value[0], newFile.Value[0]);
                });
        }
        private static bool FileAppearInMoreDirectories(Win32.FIND_DATA newFile, List<string> newDirs, Win32.FIND_DATA delFile, List<string> delDirs, ConsoleAndFileWriter errWriter)
        {
            if ( newDirs.Count == 1 && delDirs.Count == 1 )
            {
                return true;
            }
            WriteErrorWhenFileIsInMoreThanOneDirectory(newFile.cFileName, newDirs, "b", errWriter);
            WriteErrorWhenFileIsInMoreThanOneDirectory(delFile.cFileName, delDirs, "a", errWriter);

            return false;
        }
        private static void WriteErrorWhenFileIsInMoreThanOneDirectory(string filename, List<string> dirs, string side, ConsoleAndFileWriter errWriter)
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
