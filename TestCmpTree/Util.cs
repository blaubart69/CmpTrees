using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestnCmpTree
{
    public class Util
    {
        public static string GetTempDirname()
        {
            return Path.Combine(System.IO.Path.GetTempPath(), "TestCmpTree-" + Guid.NewGuid().ToString());
        }
        public static (string dira, string dirb) CreateTwoDirs()
        {
            return CreateTwoDirs(GetTempDirname());
        }
        public static (string dira, string dirb) CreateTwoDirs(string baseDir)
        {
            string dira = Directory.CreateDirectory(Path.Combine(baseDir, "a")).FullName;
            string dirb = Directory.CreateDirectory(Path.Combine(baseDir, "b")).FullName;

            return (dira, dirb);
        }
    }
}
