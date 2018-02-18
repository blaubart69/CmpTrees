using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

using Spi.Data;
using Spi.Native;

namespace TestnCmpTree
{
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
        (string dira, string dirb) CreateTwoDirs()
        {
            string baseDir = Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString());
            string dira = Directory.CreateDirectory(Path.Combine(baseDir, "a")).FullName;
            string dirb = Directory.CreateDirectory(Path.Combine(baseDir, "b")).FullName;

            return (dira, dirb);
        }
    }
}
