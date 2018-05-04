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
        public long FilesNewBytes;
        public long FilesMod;
        public long FilesModBytes;
        public long FilesDel;
        public long FilesDelBytes;
        public long DirsNew;
        public long DirsDel;
    }
    class Opts
    {
        public string DirA;
        public string DirB;
        public bool progress;
        public bool FollowJunctions = false;
        public int Depth = -1;
        public int MaxThreads = 32;
    }
    class DiffCallbackContext
    {
        public Stats stats;
        public DiffWriter writers;
    }
    class Program
    {
        static readonly string ErrFilename = Path.Combine(Environment.GetEnvironmentVariable("temp"), "cmptrees.err.txt");
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
                Console.Error.WriteLine($"could not set SE_BACKUP_PRIVILEGE [{ex.Message}]");
            }

            try
            {
                ManualResetEvent CtrlCEvent = new ManualResetEvent(false);
                StartBackgroudQuitPressedThread(CtrlCEvent);
                RunCompare(opts, CtrlCEvent);
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
        private static void RunCompare(Opts opts, ManualResetEvent CtrlCEvent)
        {
            DateTime start = DateTime.Now;
            using (var errWriter = new ConsoleAndFileWriter(Console.Error, ErrFilename))
            using (DiffWriter diffWriters = new DiffWriter())
            {
                Stats stats = new Stats();
                var enumOpts = new EnumOptions()
                {
                    followJunctions = opts.FollowJunctions,
                    maxDepth = opts.Depth
                };

                var paraCmp = new CmpDirsParallel<DiffCallbackContext>(
                    Spi.Long.GetLongFilenameNotation(opts.DirA),
                    Spi.Long.GetLongFilenameNotation(opts.DirB),
                    enumOpts,
                    DiffCallback,
                    new DiffCallbackContext() { stats = stats, writers = diffWriters },
                    (int RetCode, string Message) => errWriter.WriteLine($"E: rc={RetCode}\t{Message}"),
                    CtrlCEvent);
                paraCmp.Start(opts.MaxThreads);

                StatusLineWriter statWriter = new StatusLineWriter();
                while (!paraCmp.WaitOne(2000))
                {
                    WriteProgress(stats, paraCmp.Queued, paraCmp.Running, paraCmp.Done, statWriter);
                }
                WriteProgress(stats, paraCmp.Queued, paraCmp.Running, paraCmp.Done, statWriter);
                WriteStatistics(new TimeSpan(DateTime.Now.Ticks - start.Ticks), paraCmp.Done, stats);
                if (errWriter.hasDataWritten())
                {
                    Console.Error.WriteLine("\nerrors were logged to file [{0}]", ErrFilename);
                }
            }
        }
        private static void DiffCallback(DIFF_STATE state, string basedir, ref Win32.WIN32_FIND_DATA find_data_a, ref Win32.WIN32_FIND_DATA find_data_b, DiffCallbackContext ctx)
        {
            if (state == DIFF_STATE.SAMESAME)
            {
                return;
            }
            string baseDirToPrint = basedir == null ? String.Empty : basedir + "\\";
            string filenameToPrint = (state == DIFF_STATE.NEW) ? find_data_b.cFileName : find_data_a.cFileName;
            Win32.WIN32_FIND_DATA? dataToPrint = null;
            TextWriter toWriteTo;
            switch (state)
            {
                default:
                    throw new Exception($"internal error. no such writer for this kind of state. [{state.ToString()}]");
                case DIFF_STATE.NEW:
                    if (Spi.Misc.IsDirectoryFlagSet(find_data_b))
                    {
                        toWriteTo = ctx.writers.newDirWriter;
                        Interlocked.Increment(ref ctx.stats.DirsNew);
                    }
                    else
                    {
                        toWriteTo = ctx.writers.newWriter;
                        Interlocked.Increment(ref ctx.stats.FilesNew);
                        Interlocked.Add(ref ctx.stats.FilesNewBytes, (long)Misc.GetFilesize(find_data_b));
                        dataToPrint = find_data_b;
                    }
                    break;
                case DIFF_STATE.MODIFY:
                    toWriteTo = ctx.writers.modWriter;
                    Interlocked.Increment(ref ctx.stats.FilesMod);
                    Interlocked.Add(ref ctx.stats.FilesModBytes, (long)Misc.GetFilesize(find_data_b) -
                                                              (long)Misc.GetFilesize(find_data_a));
                    dataToPrint = find_data_b;
                    break;
                case DIFF_STATE.DELETE:
                    if (Spi.Misc.IsDirectoryFlagSet(find_data_a))
                    {
                        toWriteTo = ctx.writers.delDirWriter;
                        Interlocked.Increment(ref ctx.stats.DirsDel);
                    }
                    else
                    {
                        toWriteTo = ctx.writers.delWriter;
                        Interlocked.Increment(ref ctx.stats.FilesDel);
                        Interlocked.Add(ref ctx.stats.FilesDelBytes, (long)Misc.GetFilesize(find_data_a));
                        dataToPrint = find_data_a;
                    }
                    break;

            }
            if (dataToPrint.HasValue)
            {
                var data = dataToPrint.Value;
                toWriteTo.WriteLine(
                      $"{Misc.GetFilesize(data)}"
                    + $"\t{Misc.FiletimeToString(data.ftCreationTime)}"
                    + $"\t{Misc.FiletimeToString(data.ftLastWriteTime)}"
                    + $"\t{Misc.FiletimeToString(data.ftLastAccessTime)}"
                    + $"\t{baseDirToPrint}{filenameToPrint}");
            }
            else
            {
                toWriteTo.WriteLine($"{baseDirToPrint}{filenameToPrint}");
            }

        }
        private static void WriteProgress(Stats stats, long queued, long running, long cmpsDone, StatusLineWriter statWriter)
        {
            Process currProc = null;
            try
            {
                currProc = System.Diagnostics.Process.GetCurrentProcess();
            }
            catch { }

            string privMem = currProc == null ? "n/a" : Misc.GetPrettyFilesize(currProc.PrivateMemorySize64);
            string threadcount = currProc == null ? "n/a" : currProc.Threads.Count.ToString();

            statWriter.WriteWithDots($"dirs queued/running/done: {queued}/{running}/{cmpsDone}"
                + $" | new/mod/del: {stats.FilesNew}/{stats.FilesMod}/{stats.FilesDel}"
                + $" | GC.Total: {Misc.GetPrettyFilesize(GC.GetTotalMemory(forceFullCollection: false))}"
                + $" | PrivateMemory: {privMem}"
                + $" | Threads: {threadcount}");
        }
        private static void WriteStatistics(TimeSpan ProgramDuration, long comparesDone, Stats stats)
        {
            Console.Error.WriteLine($"\n{comparesDone} dirs compared in {Spi.Misc.NiceDuration(ProgramDuration)}");
            if (ProgramDuration.Ticks > 0)
            {
                double cmpsPerMilli = (double)comparesDone / (double)ProgramDuration.TotalMilliseconds;
                Console.Error.WriteLine("{0:0.##} cmp/s | {1:0.##} cmp/m | {2:0.##} cmp/h",
                    cmpsPerMilli * 1000,
                    cmpsPerMilli * 1000 * 60,
                    cmpsPerMilli * 1000 * 60 * 60);
            }
            Console.Error.WriteLine(
                $"\nnew files\t{stats.FilesNew,12:N0}\t{Misc.GetPrettyFilesize(stats.FilesNewBytes)}"
              + $"\nmod files\t{stats.FilesMod,12:N0}\t{Misc.GetPrettyFilesize(stats.FilesModBytes)}"
              + $"\ndel files\t{stats.FilesDel,12:N0}\t{Misc.GetPrettyFilesize(stats.FilesDelBytes)}"
              + $"\nnew dirs \t{stats.DirsNew,12:N0}"
              + $"\ndel dirs \t{stats.DirsDel,12:N0}");
        }
        static void StartBackgroudQuitPressedThread(ManualResetEvent CtrlCPressed)
        {
            new Thread(new ThreadStart(() =>
            {
                while (true)
                {
                    if (Console.ReadKey().KeyChar == 'q')
                    {
                        Console.Error.WriteLine("\ngoing down...\n");
                        CtrlCPressed.Set();
                        break;
                    }
                }
            }))
            { IsBackground = true }.Start();

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
