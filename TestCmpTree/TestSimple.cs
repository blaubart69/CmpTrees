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
        public readonly Win32.FIND_DATA src;
        public readonly Win32.FIND_DATA trg;

        public DiffData(DIFF_STATE state, string baseDir, Win32.FIND_DATA src, Win32.FIND_DATA trg)
        {
            this.state = state;
            this.baseDir = baseDir;
            this.src = src;
            this.trg = trg;
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
                new CancellationTokenSource().Token, 3);
            c.Start();
            c.Finished.WaitOne();
            Assert.IsFalse(error);
            Assert.IsFalse(diff);
        }
        [TestMethod]
        public void DirAndFileWithSameName()
        {
            (string src, string trg) = Util.CreateTwoDirs();

            Directory.CreateDirectory(  Path.Combine(src, "x"));
            File.WriteAllText(          Path.Combine(trg, "x"), "content");

            var result = RunCmp(src, trg);
            Assert.AreEqual(2, result.Count);

            var SrcDelDir = result.First(r => r.state == DIFF_STATE.NEW);
            Assert.IsTrue(SrcDelDir.src.cFileName.Equals("x")  &&  Misc.IsDirectory(SrcDelDir.src.dwFileAttributes) );

            var TrgAddFile = result.First(r => r.state == DIFF_STATE.DELETE);
            Assert.IsTrue(TrgAddFile.trg.cFileName.Equals("x") && !Misc.IsDirectory(TrgAddFile.trg.dwFileAttributes));

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
            
            Assert.AreEqual(0, result.Count); // should be 0 as we reporting no SAMESAME files
            //Assert.IsTrue(result.All(r => r.state == DIFF_STATE.SAMESAME && r.a.cFileName.Equals("x") && !Misc.IsDirectoryFlagSet(r.a.dwFileAttributes)));
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
            Assert.AreEqual(0, result.Count);   // should be 0 as we reporting no SAMESAME files
            //Assert.IsTrue(result.Where(i => !Misc.IsDirectoryFlagSet(i.a.dwFileAttributes)).All(r => r.state == DIFF_STATE.SAMESAME && r.a.cFileName.Equals("x")));
            //Assert.IsTrue(result.Where(i =>  Misc.IsDirectoryFlagSet(i.a.dwFileAttributes)).All(r => r.state == DIFF_STATE.SAMESAME && r.a.cFileName.Equals("subdir")));
        }
        [TestMethod]
        public void OneNewFile()
        {
            (string src, string trg) = Util.CreateTwoDirs();

            string srcFilename = Path.Combine(src, "neu.txt");
            File.WriteAllText(srcFilename, "content");

            IList<DiffData> r = RunCmp(src, trg);
            Assert.AreEqual(1, r.Count);
            Assert.IsTrue(r.All(i => i.state == DIFF_STATE.NEW));
            Assert.AreEqual("neu.txt", r.First().src.cFileName);

        }
        [TestMethod]
        public void OneFileDeleted()
        {
            (string src, string trg) = Util.CreateTwoDirs();

            string trgFilename = Path.Combine(trg, "toomuch.txt");
            File.WriteAllText(trgFilename, ".");

            IList<DiffData> r = RunCmp(src, trg);
            Assert.AreEqual(1, r.Count);
            Assert.IsTrue(r.All(i => i.state == DIFF_STATE.DELETE));
            Assert.AreEqual("toomuch.txt", r.First().trg.cFileName);

        }
        [TestMethod]
        public void OneFileNewOneFileDel()
        {
            (string src, string trg) = Util.CreateTwoDirs();

            string s = "neichImSource.txt";
            string t = "zvüImTarget.txt";
            string srcFilename = Path.Combine(src, s);
            string trgFilename = Path.Combine(trg, t);
            File.WriteAllText(srcFilename, ".");
            File.WriteAllText(trgFilename, ".");

            IList<DiffData> r = RunCmp(src, trg);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual(s, r.First(i => i.state == DIFF_STATE.NEW)   .src.cFileName);
            Assert.AreEqual(t, r.First(i => i.state == DIFF_STATE.DELETE).trg.cFileName);
        }
        // --------------------------------------------------------------------
        IList<DiffData> RunCmp(string src, string trg)
        {
            bool error = false;
            List<DiffData> result = new List<DiffData>();

            var c = new CmpTrees.CmpDirsParallel(src, trg, new CmpTrees.EnumOptions(),
                (DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b) =>
                {
                    result.Add(new DiffData(state, basedir, find_data_a, find_data_b));
                },
                (rc, msg) => { error = true; },
                new CancellationTokenSource().Token, 4);
            c.Start();
            c.Finished.WaitOne();
            Assert.IsFalse(error);

            return result;
        }
        
    }
}
