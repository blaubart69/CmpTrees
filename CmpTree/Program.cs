using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Spi;
using Spi.Native;
using Spi.Data;

namespace CmpTrees
{
    public class Stats
    {
        public long FilesA;
        public long FilesB;
        public long FilesNew;
        public long FilesMod;
        public long FilesDel;
    }
    class Opts
    {
        public string DirA;
        public string DirB;
        public bool progress;
        public bool FollowJunctions = false;
        public int Depth = -1;
        public int MaxThreads = -1;
    }
    class Program
    {
        static readonly string ErrFilename = Path.Combine(Environment.GetEnvironmentVariable("temp"), "cmptree.err.txt");
        static int Main(string[] args)
        {
            Opts opts;
            if ((opts = GetOpts(args)) == null)
            {
                return 8;
            }

            try
            {
                Spi.Native.PrivilegienStadl.TryToSetBackupPrivilege();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("could not set SE_BACKUP_PRIVILEGE");
            }

            try
            {
                ManualResetEvent CrtlCEvent = new ManualResetEvent(false);
                new Thread(new ThreadStart(() =>
                {
                    while (true)
                    {
                        if (Console.ReadKey().KeyChar == 'q')
                        {
                            Console.Error.WriteLine("\ngoing down...\n");
                            CrtlCEvent.Set();
                            break;
                        }
                    }
                })){ IsBackground = true }.Start();

                DateTime start = DateTime.Now;
                using (var errWriter        = new ConsoleAndFileWriter(Console.Error, ErrFilename))
                using (TextWriter newWriter = TextWriter.Synchronized(new StreamWriter(@".\new.txt", append: false, encoding: Encoding.UTF8)))
                using (TextWriter modWriter = TextWriter.Synchronized(new StreamWriter(@".\mod.txt", append: false, encoding: Encoding.UTF8)))
                using (TextWriter delWriter = TextWriter.Synchronized(new StreamWriter(@".\del.txt", append: false, encoding: Encoding.UTF8)))
                {
                    Stats stats = new Stats();
                    DiffHandler diff = (DIFF_STATE state, string basedir, ref Win32.WIN32_FIND_DATA find_data_a, ref Win32.WIN32_FIND_DATA find_data_b) =>
                    {
                        if (state == DIFF_STATE.SAMESAME)
                        {
                            return;
                        }
                        string baseDirToPrint = basedir == null ? String.Empty : basedir + "\\";
                        string filenameToPrint = (state == DIFF_STATE.NEW) ? find_data_b.cFileName : find_data_a.cFileName;
                        //Console.Out.WriteLine($"{GetDiffPrefix(state)} {baseDirToPrint}{filenameToPrint}");
                        TextWriter toWriteTo;
                        switch (state)
                        {
                            case DIFF_STATE.NEW:    toWriteTo = newWriter; Interlocked.Increment(ref stats.FilesNew); break;
                            case DIFF_STATE.MODIFY: toWriteTo = modWriter; Interlocked.Increment(ref stats.FilesMod); break;
                            case DIFF_STATE.DELETE: toWriteTo = delWriter; Interlocked.Increment(ref stats.FilesDel); break;
                            default: throw new Exception($"internal error. no such writer for this kind of state. [{state.ToString()}]");
                        }
                        toWriteTo.WriteLine($"{baseDirToPrint}{filenameToPrint}");
                    };

                    var enumOpts = new EnumOptions()
                    {
                        diffHandler = diff,
                        errorHandler = (int RetCode, string Message) => errWriter.WriteLine($"E: rc={RetCode} {Message}"),
                        followJunctions = opts.FollowJunctions,
                        maxDepth = opts.Depth
                    };

                    ManualResetEvent cmpFinished = new ManualResetEvent(false);

                    CmpDirsParallel paraCmp = new CmpDirsParallel(opts.DirA, opts.DirB, enumOpts, CrtlCEvent, cmpFinished);
                    paraCmp.Start(opts.MaxThreads);

                    StatusLineWriter statWriter = new StatusLineWriter();
                    ulong cmpsDone = 0;
                    while (!cmpFinished.WaitOne(2000))
                    {
                        Process currProc = null;
                        try
                        {
                            currProc = System.Diagnostics.Process.GetCurrentProcess();
                        }
                        catch { }

                        string privMem = currProc == null ? "n/a" : Misc.GetPrettyFilesize(currProc.PrivateMemorySize64);
                        string threadcount = currProc == null ? "n/a" : currProc.Threads.Count.ToString();

                        paraCmp.GetCounter(out ulong queued, out ulong running, out cmpsDone);
                        statWriter.WriteWithDots($"dirs queued/running/done: {queued}/{running}/{cmpsDone}"
                            + $" | new/mod/del: {stats.FilesNew}/{stats.FilesMod}/{stats.FilesDel}"
                            + $" | GC.Total: {Misc.GetPrettyFilesize(GC.GetTotalMemory(forceFullCollection: false))}"
                            + $" | PrivateMemory: {privMem}"
                            + $" | Threads: {threadcount}");
                    }
                    if (errWriter.hasDataWritten())
                    {
                        Console.Error.WriteLine("\nerrors were logged to file [{0}]", ErrFilename);
                    }
                    WriteStatistics(new TimeSpan(DateTime.Now.Ticks - start.Ticks), cmpsDone);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Hoppala. Call 555-D.R.S.P.I.N.D.L.E.R");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 12;
            }
            return 0;

        }
        private static void WriteStatistics(TimeSpan ProgramDuration, ulong comparesDone)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{comparesDone} dirs compared in {Spi.Misc.NiceDuration(ProgramDuration)}");
            if (ProgramDuration.Ticks > 0)
            {
                double cmpsPerMilli = (double)comparesDone / (double)ProgramDuration.TotalMilliseconds;
                Console.Error.WriteLine("{0:0.##} cmp/s | {1:0.##} cmp/m | {2:0.##} cmp/h",
                    cmpsPerMilli * 1000,
                    cmpsPerMilli * 1000 * 60,
                    cmpsPerMilli * 1000 * 60 * 60);
            }
        }
        private static string GetDiffPrefix(DIFF_STATE state)
        {
            switch (state)
            {
                case DIFF_STATE.NEW: return "+";
                case DIFF_STATE.DELETE: return "-";
                case DIFF_STATE.MODIFY: return "%";
                case DIFF_STATE.SAMESAME: return "S";
            }
            throw new Exception($"wrong state [{state.ToString()}]");
        }

        static void ShowHelp(Mono.Options.OptionSet p)
        {
            Console.WriteLine("Usage: CmpTrees [OPTIONS] {DirectoryA} {DirectoryB}");
            Console.WriteLine("compare dirA with dirB and writes out the differences");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
        static Opts GetOpts(string[] args)
        {
            bool show_help = false;
            Opts opts = new Opts();
            var p = new Mono.Options.OptionSet() {
                { "p|progress", "prints out little statistics",             v => opts.progress = (v != null) },
                { "d|depth=",   "max depth to go down",                     v => opts.Depth = Convert.ToInt32(v) },
                { "j|follow",   "follow junctions",                         v => opts.FollowJunctions = (v != null) },
                { "t|threads=",  "max enumeration threads parallel",        v => opts.MaxThreads = Convert.ToInt32(v) },
                { "h|help",     "show this message and exit",               v => show_help = v != null }            };

            try
            {
                List<string> dirs = p.Parse(args);
                if ( dirs.Count != 2)
                {
                    Console.Error.WriteLine("no two dir's given");
                    opts = null;
                    show_help = true;
                }
                else
                {
                    opts.DirA = dirs[0];
                    opts.DirB = dirs[1];
                }
            }
            catch (Mono.Options.OptionException oex)
            {
                Console.WriteLine(oex.Message);
                opts = null;
            }
            if (show_help)
            {
                ShowHelp(p);
                opts = null;
            }
            return opts;
        }

    }
}
