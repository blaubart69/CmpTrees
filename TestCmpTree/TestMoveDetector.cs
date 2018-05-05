using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using CmpTrees;
using System.Collections.Generic;
using Spi.Native;
using Spi;
using System.IO;

namespace TestnCmpTree
{
    struct MoveEntry
    {
        public string filename;
        public string from;
        public string to;

        public MoveEntry(string filename, string from, string to)
        {
            this.filename = filename;
            this.from = from;
            this.to = to;
        }
    }
    [TestClass]
    public class TestMoveDetector
    {
        [TestMethod]
        public void EmptyDirs()
        {
            string baseDir = Util.GetTempDirname();
            (string dira, string dirb) = Util.CreateTwoDirs(baseDir);
            var moves = CalcMoves(baseDir, dira, dirb);

            Assert.AreEqual(0, moves.Count);
        }
        [TestMethod]
        public void OneFileMovedFrom1to2()
        {
            string baseDir = Util.GetTempDirname();
            (string dira, string dirb) = Util.CreateTwoDirs(baseDir);

            string sub1 = Directory.CreateDirectory(Path.Combine(dira, "1")).FullName;
            string sub2 = Directory.CreateDirectory(Path.Combine(dirb, "2")).FullName;
            string File1 = Path.Combine(sub1, "movedFrom_1_to_2.txt");
            string File2 = Path.Combine(sub2, "movedFrom_1_to_2.txt");
            File.WriteAllText(File1, "content");
            File.Copy(File1, File2);

            var moves = CalcMoves(baseDir, dira, dirb);

            Assert.AreEqual(1, moves.Count);
        }
        private static List<MoveEntry> CalcMoves(string baseDir, string dira, string dirb)
        {
            bool err = false;
            DiffProcessing diffProc = new DiffProcessing(new Stats(), new DiffWriter(baseDir));
            var paraCmp = new CmpDirsParallel(dira, dirb, new EnumOptions(), diffProc.DiffCallback,
                (rc, msg) => { err = true; }, new System.Threading.ManualResetEvent(false));
            paraCmp.Start();
            Misc.WaitUtilSet(paraCmp.IsFinished, 2000, doEvery: null );

            IComparer<Win32.FIND_DATA> find_data_Comparer = new FindDataMoveComparer();
            var newFiles = new SortedList<Win32.FIND_DATA, List<string>>(diffProc.newFilesDic, find_data_Comparer);
            var delFiles = new SortedList<Win32.FIND_DATA, List<string>>(diffProc.delFilesDic, find_data_Comparer);

            List<MoveEntry> result = new List<MoveEntry>();

            MoveDetector.Run(newFiles: newFiles, delFiles: delFiles, errWriter: null,
                moveHandler: (filename, from, to) =>
                {
                    result.Add(new MoveEntry(filename, from, to));
                });

            return result;
        }
    }
}
