using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

using Spi;
using Spi.Data;
using Spi.Native;

namespace TestnCmpTree
{
    class DiffData
    {
        public readonly DIFF_STATE state;
        public readonly string baseDir;
        public readonly Win32.FIND_DATA a;
        public readonly Win32.FIND_DATA b;

        public DiffData(DIFF_STATE state, string baseDir, Win32.FIND_DATA a, Win32.FIND_DATA b)
        {
            this.state = state;
            this.baseDir = baseDir;
            this.a = a;
            this.b = b;
        }
    }
    [TestClass]
    public class TestSimple
    {
        [TestMethod]
        public void TwoEmptyDirs()
        {
            (string dira, string dirb) = Util.CreateTwoDirs();

            bool error = false;
            bool diff = false;

            var c = new CmpTrees.CmpDirsParallel(dira, dirb, new CmpTrees.EnumOptions(),
                (DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b) => { diff = true; },
                (rc, msg) => { error = true; },
                new ManualResetEvent(false));
            c.Start();
            while ( ! c.IsFinished.WaitOne(1000) )
            { }
            Assert.IsFalse(error);
            Assert.IsFalse(diff);
        }
        [TestMethod]
        public void DirAndFileWithSameName()
        {
            (string dira, string dirb) = Util.CreateTwoDirs();

            Directory.CreateDirectory(  Path.Combine(dira, "x"));
            File.WriteAllText(          Path.Combine(dirb, "x"), "content");

            var result = RunCmp(dira, dirb);
            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result.Any(r => r.state == DIFF_STATE.DELETE && r.a.cFileName.Equals("x") &&  Misc.IsDirectoryFlagSet(r.a.dwFileAttributes)));
            Assert.IsTrue(result.Any(r => r.state == DIFF_STATE.NEW    && r.b.cFileName.Equals("x") && !Misc.IsDirectoryFlagSet(r.b.dwFileAttributes)));
        }
        [TestMethod]
        public void TwoSameSameFiles()
        {
            (string dira, string dirb) = Util.CreateTwoDirs();

            string srcFilename = Path.Combine(dirb, "x");
            string trgFilename = Path.Combine(dira, "x");
            File.WriteAllText(srcFilename, "content");
            File.Copy(srcFilename, trgFilename);

            var result = RunCmp(dira, dirb);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.All(r => r.state == DIFF_STATE.SAMESAME && r.a.cFileName.Equals("x") && !Misc.IsDirectoryFlagSet(r.a.dwFileAttributes)));
        }
        [TestMethod]
        public void TwoSameSameFilesInSubDir()
        {
            (string dira, string dirb) = Util.CreateTwoDirs();

            string sub1 = Directory.CreateDirectory(Path.Combine(dira, "subdir")).FullName;
            string sub2 = Directory.CreateDirectory(Path.Combine(dirb, "subdir")).FullName;
            string srcFilename = Path.Combine(sub1, "x");
            string trgFilename = Path.Combine(sub2, "x");
            File.WriteAllText(srcFilename, "content");
            File.Copy(srcFilename, trgFilename);

            var result = RunCmp(dira, dirb);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Where(i => !Misc.IsDirectoryFlagSet(i.a.dwFileAttributes)).All(r => r.state == DIFF_STATE.SAMESAME && r.a.cFileName.Equals("x")));
            Assert.IsTrue(result.Where(i =>  Misc.IsDirectoryFlagSet(i.a.dwFileAttributes)).All(r => r.state == DIFF_STATE.SAMESAME && r.a.cFileName.Equals("subdir")));
        }
        // --------------------------------------------------------------------
        IList<DiffData> RunCmp(string dira, string dirb)
        {
            bool error = false;
            List<DiffData> result = new List<DiffData>();

            var c = new CmpTrees.CmpDirsParallel(dira, dirb, new CmpTrees.EnumOptions(),
                (DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b) =>
                {
                    result.Add(new DiffData(state, basedir, find_data_a, find_data_b));
                },
                (rc, msg) => { error = true; },
                new ManualResetEvent(false));
            c.Start();
            while (!c.IsFinished.WaitOne(1000))
            { }
            Assert.IsFalse(error);

            return result;
        }
        
    }
}
