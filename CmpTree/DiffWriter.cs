using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CmpTrees
{
    public class DiffWriter : IDisposable
    {
        public readonly TextWriter newWriter;
        public readonly TextWriter modWriter;
        public readonly TextWriter delWriter;
        public readonly TextWriter newDirWriter;
        public readonly TextWriter delDirWriter;

        public DiffWriter() : this(".")
        {
        }
        public DiffWriter(string Directoryname)
        {
            newWriter    = TextWriter.Synchronized(new StreamWriter($"{Directoryname}\\new.txt",       append: false, encoding: Encoding.UTF8));
            modWriter    = TextWriter.Synchronized(new StreamWriter($"{Directoryname}\\mod.txt",       append: false, encoding: Encoding.UTF8));
            delWriter    = TextWriter.Synchronized(new StreamWriter($"{Directoryname}\\del.txt",       append: false, encoding: Encoding.UTF8));
            newDirWriter = TextWriter.Synchronized(new StreamWriter($"{Directoryname}\\newDirs.txt",   append: false, encoding: Encoding.UTF8));
            delDirWriter = TextWriter.Synchronized(new StreamWriter($"{Directoryname}\\delDirs.txt",   append: false, encoding: Encoding.UTF8));
        }

        public void Dispose()
        {
            newWriter?.Close();
            modWriter?.Close();
            delWriter?.Close();
            newDirWriter?.Close();
            delWriter?.Close();
        }
    }
}
