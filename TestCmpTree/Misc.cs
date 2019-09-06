using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Spi;

namespace TestnCmpTree
{
    [TestClass]
    public class Misc
    {
        [TestMethod]
        public void FileSize0byte()
        {
            Assert.AreEqual("0 bytes", Spi.Misc.GetPrettyFilesize(0));
        }
        [TestMethod]
        public void NegativeFileSize1byte()
        {
            Assert.AreEqual("-1 bytes", Spi.Misc.GetPrettyFilesize(-1));
        }

        [TestMethod]
        public void NegativeFileSize1KB()
        {
            Assert.AreEqual("-1,00 KB", Spi.Misc.GetPrettyFilesize(-1024));
        }
        [TestMethod]
        public void NegativeFileSize10MB()
        {
            Assert.AreEqual("-10,0 MB", Spi.Misc.GetPrettyFilesize(-1024 * 1024 * 10));
        }

    }
}
