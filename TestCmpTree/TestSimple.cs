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
        public readonly Win32.WIN32_FIND_DATA a;
        public readonly Win32.WIN32_FIND_DATA b;

        public DiffData(DIFF_STATE state, string baseDir, Win32.WIN32_FIND_DATA a, Win32.WIN32_FIND_DATA b)
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
            (string dira, string dirb) = CreateTwoDirs();

            bool error = false;
            bool diff = false;

            var enumOpts = new CmpTrees.EnumOptions()
            {
                diffHandler = (DIFF_STATE state, string basedir, Win32.WIN32_FIND_DATA find_data_a, Win32.WIN32_FIND_DATA find_data_b) => { diff = true; },
                errorHandler = (rc, msg) => { error = true;  },
                followJunctions = false,
                maxDepth = -1
            };
            var isFinished = new ManualResetEvent(false);

            new CmpTrees.CmpDirsParallel(dira, dirb, enumOpts, new ManualResetEvent(false), isFinished).Start();
            while ( ! isFinished.WaitOne(1000) )
            { }
            Assert.IsFalse(error);
            Assert.IsFalse(diff);
        }
        [TestMethod]
        public void DirAndFileWithSameName()
        {
            (string dira, string dirb) = CreateTwoDirs();

            Directory.CreateDirectory(  Path.Combine(dira, "x"));
            File.WriteAllText(          Path.Combine(dirb, "x"), "content");

            var result = RunCmp(dira, dirb);
            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result.Any(r => r.state == DIFF_STATE.DELETE && r.a.cFileName.Equals("x") &&  Misc.IsDirectoryFlagSet(r.a.dwFileAttributes)));
            Assert.IsTrue(result.Any(r => r.state == DIFF_STATE.NEW    && r.b.cFileName.Equals("x") && !Misc.IsDirectoryFlagSet(r.b.dwFileAttributes)));
        }
        // --------------------------------------------------------------------
        IList<DiffData> RunCmp(string dira, string dirb)
        {
            bool error = false;
            List<DiffData> result = new List<DiffData>();

            var enumOpts = new CmpTrees.EnumOptions()
            {
                diffHandler = (DIFF_STATE state, string basedir, Win32.WIN32_FIND_DATA find_data_a, Win32.WIN32_FIND_DATA find_data_b) => 
                {
                    result.Add(new DiffData(state,basedir,find_data_a,find_data_b));
                },
                errorHandler = (rc, msg) => { error = true; },
                followJunctions = false,
                maxDepth = -1
            };
            var isFinished = new ManualResetEvent(false);

            new CmpTrees.CmpDirsParallel(dira, dirb, enumOpts, new ManualResetEvent(false), isFinished).Start();
            while (!isFinished.WaitOne(1000))
            { }
            Assert.IsFalse(error);

            return result;
        }
        (string dira, string dirb) CreateTwoDirs()
        {
            string baseDir = Path.Combine(System.IO.Path.GetTempPath(),"TestCmpTree-" + Guid.NewGuid().ToString());
            string dira = Directory.CreateDirectory(Path.Combine(baseDir, "a")).FullName;
            string dirb = Directory.CreateDirectory(Path.Combine(baseDir, "b")).FullName;

            return (dira, dirb);
        }
    }
}
