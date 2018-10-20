using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using Spi;
using Spi.Native;
using System.Text;

namespace CmpTrees
{
    public class Stats
    {
        public long FilesNew;
        public long FilesNewBytes;
        public long FilesMod;
        public long FilesModBytes;
        public long FilesDel;
        public long FilesDelBytes;
        public long DirsNew;
        public long DirsDel;
        public long Errors;
    }
    class Opts
    {
        public string sourceDir;
        public string targetDir;
        public bool FollowJunctions = false;
        public int Depth = -1;
        public int MaxThreads = 32;
        public bool forceSortSource = false;
        public bool forceSortTarget = false;
    }
    class Program
    {
        //static readonly string ErrFilename = Path.Combine(Environment.GetEnvironmentVariable("temp"), "cmptrees.err.txt");
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
                using (var CtrlCEvent = new CancellationTokenSource())
                using (var errWriter = TextWriter.Synchronized(
                    new StreamWriter(@".\err.txt", append: false, encoding: System.Text.Encoding.UTF8)))
                    //using (var errWriter = new ConsoleAndFileWriter(Console.Error, ErrFilename))
                    
                {
                    StartBackgroudQuitPressedThread(CtrlCEvent);
                    RunCompare(opts, CtrlCEvent.Token, errWriter);

                   //IComparer<Win32.FIND_DATA> find_data_Comparer = new FindDataComparer_Name_Size_Modified();
                   //RunMoveDetector(newFiles, delFiles);
                   
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

        private static void RunMoveDetector(SortedList<Win32.FIND_DATA, List<string>> newFiles, SortedList<Win32.FIND_DATA, List<string>> delFiles)
        {
            using (var writer    = new StreamWriter(@".\moved.txt",      append: false, encoding: Encoding.UTF8))
            using (var errWriter = new StreamWriter(@".\movedError.txt", append: false, encoding: Encoding.UTF8))
            {
                Console.Error.Write("detecting possible moves...\r");
                ulong possibleFileMoves = 0;
                ulong possibleFileMovesFileSizes = 0;
                MoveDetector.Run(newFiles, delFiles, errWriter,
                    (filename, FromDir, ToDir, filesize) =>
                    {
                        possibleFileMoves += 1;
                        possibleFileMovesFileSizes += filesize;
                        writer.WriteLine($"{filename}\t{FromDir}\t{ToDir}");
                    });
                Console.Out.WriteLine(
                $"moved files\t{possibleFileMoves,12:N0}\t{Misc.GetPrettyFilesize(possibleFileMovesFileSizes)}");
            }
        }

        private static void RunCompare(Opts opts, CancellationToken CtrlCEvent, TextWriter errWriter)
        {
            DateTime start = DateTime.Now;

            using (DiffWriter diffWriters = new DiffWriter())
            {
                Stats stats = new Stats();
                var enumOpts = new EnumOptions()
                {
                    followJunctions = opts.FollowJunctions,
                    maxDepth = opts.Depth,
                    forceSortSource = opts.forceSortSource,
                    forceSortTarget = opts.forceSortTarget
                };

                DiffProcessing diffProcessor = new DiffProcessing(stats, diffWriters);

                string src = Spi.Long.GetLongFilenameNotation(opts.sourceDir.TrimEnd('\\'));
                string trg = Spi.Long.GetLongFilenameNotation(opts.targetDir.TrimEnd('\\'));

                Console.Error.WriteLine($"source: {src}\ntarget: {trg}");

                var paraCmp = new CmpDirsParallel(
                    sourceDir:       src,
                    targetDir:       trg,
                    opts:            enumOpts,
                    diffHandler:     diffProcessor.DiffCallback,
                    errorHandler:    (int RetCode, string Message) =>
                    {
                        Interlocked.Increment(ref stats.Errors);
                        errWriter.WriteLine($"E: rc={RetCode}\t{Message}");
                    },
                    CtrlCEvent:      CtrlCEvent, 
                    maxThreadsToRun: opts.MaxThreads);
                paraCmp.Start();

                StatusLineWriter statWriter = new StatusLineWriter();
                Misc.ExecUtilWaitHandleSet(paraCmp.Finished, 2000, 
                    () => WriteProgress(stats, paraCmp.Queued, paraCmp.Running, paraCmp.Done, statWriter));
                WriteStatistics(new TimeSpan(DateTime.Now.Ticks - start.Ticks), paraCmp.Done, stats);
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

            string virtMem      = currProc == null ? "n/a" : Misc.GetPrettyFilesize(currProc.VirtualMemorySize64);
            string threadcount  = currProc == null ? "n/a" : currProc.Threads.Count.ToString();

            statWriter.Write($"dirs queued/running/done/errors: {queued:N0}/{running}/{cmpsDone:N0}/{stats.Errors:N0}"
                 + $" | new/mod/del: {stats.FilesNew:N0}/{stats.FilesMod:N0}/{stats.FilesDel:N0}"
                 + $" | virtMem {virtMem}");
        }
        private static void WriteStatistics(TimeSpan ProgramDuration, long comparesDone, Stats stats)
        {
            Console.Out.Write($"\n{comparesDone:N0} dirs compared in {Spi.Misc.NiceDuration(ProgramDuration)}");
            if (ProgramDuration.Ticks > 0)
            {
                double cmpsPerMilli = (double)comparesDone / (double)ProgramDuration.TotalMilliseconds;
                Console.Out.Write(" ({0:0.##} cmp/s | {1:0.##} cmp/m | {2:0.##} cmp/h)",
                    cmpsPerMilli * 1000,
                    cmpsPerMilli * 1000 * 60,
                    cmpsPerMilli * 1000 * 60 * 60);
            }
            Console.Out.WriteLine(
              $"\n\nnew files\t{stats.FilesNew,12:N0}\t{Misc.GetPrettyFilesize(stats.FilesNewBytes)}"
              + $"\nmod files\t{stats.FilesMod,12:N0}\t{Misc.GetPrettyFilesize(stats.FilesModBytes)}"
              + $"\ndel files\t{stats.FilesDel,12:N0}\t{Misc.GetPrettyFilesize(stats.FilesDelBytes)}"
              + $"\nnew dirs \t{stats.DirsNew,12:N0}"
              + $"\ndel dirs \t{stats.DirsDel,12:N0}");
        }
        static void StartBackgroudQuitPressedThread(CancellationTokenSource CtrlC)
        {
            ThreadPool.QueueUserWorkItem(
                new WaitCallback(
                    (object state) =>
                    {
                        try
                        {
                            while (true)
                            {
                                if (Console.ReadKey().KeyChar == 'q')
                                {
                                    Console.Error.WriteLine("\ngoing down...\n");
                                    CtrlC.Cancel();
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"something is wrong in the thread waiting for 'q' to be pressed.\n[{ex.Message}]");
                        }
                    }
                    ));

        }
        static void ShowHelp(Mono.Options.OptionSet p)
        {
            Console.WriteLine("Usage: CmpTrees [OPTIONS] {source dir} {target dir}");
            Console.WriteLine("compare source with target and writes out the differences");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
        static Opts GetOpts(string[] args)
        {
            bool show_help = false;
            Opts opts = new Opts();
            var p = new Mono.Options.OptionSet() {
                { "d|depth=",   "max depth to go down",                     v => opts.Depth = Convert.ToInt32(v)      },
                { "j|follow",   "follow junctions",                         v => opts.FollowJunctions = (v != null)   },
                { "t|threads=", "max enumeration threads parallel",         v => opts.MaxThreads = Convert.ToInt32(v) },
                { "sorts",      "force sorting of entries on source side",  v => opts.forceSortSource = (v != null)        },
                { "sortt",      "force sorting of entries on target side",  v => opts.forceSortTarget = (v != null)        },
                { "h|help",     "show this message and exit",               v => show_help       = (v != null)        } };

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
                    opts.sourceDir = dirs[0];
                    opts.targetDir = dirs[1];
                }

                if (opts != null && opts.forceSortSource)
                {
                    Console.Error.WriteLine("will sort items in source dir");
                }
                if (opts != null && opts.forceSortTarget)
                {
                    Console.Error.WriteLine("will sort items in target dir");
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
