using Spi;
using Spi.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MoveDetector
{
    class Program
    {
        static void Main(string[] args)
        {
        }
        private static void RunMoveDetector(SortedList<Win32.FIND_DATA, List<string>> newFiles, SortedList<Win32.FIND_DATA, List<string>> delFiles)
        {
            using (var writer = new StreamWriter(@".\moved.txt", append: false, encoding: Encoding.UTF8))
            using (var errWriter = new StreamWriter(@".\movedError.txt", append: false, encoding: Encoding.UTF8))
            {
                Console.Error.Write("detecting possible moves...\r");
                ulong possibleFileMoves = 0;
                ulong possibleFileMovesFileSizes = 0;
                MoveDetector.Run(newFiles, delFiles, errWriter,
                    (filename, FromDir, ToDir, filesize) =>
                    {
                        possibleFileMoves += 1;
                        possibleFileMovesFileSizes += filesize;
                        writer.WriteLine($"{filename}\t{FromDir}\t{ToDir}");
                    });
                Console.Out.WriteLine(
                $"moved files\t{possibleFileMoves,12:N0}\t{Misc.GetPrettyFilesize(possibleFileMovesFileSizes)}");
            }
        }

    }
}
