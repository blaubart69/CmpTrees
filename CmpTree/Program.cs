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
        public long FilesSame;
        public long FilesSameBytes;
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
        public bool reportSameFile = false;
        public string errorFilename = @".\err.txt";
    }
    class Program
    {
        //static readonly string ErrFilename = Path.Combine(Environment.GetEnvironmentVariable("temp"), "cmptrees.err.txt");
        static int Main(string[] args)
        {
            Opts opts;
            if ((opts = GetOpts.Parse(args)) == null)
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
                using (var errWriter = TextWriter.Synchronized(new StreamWriter(opts.errorFilename, append: false, encoding: System.Text.Encoding.UTF8)))
                {
                    StartBackgroudQuitPressedThread(CtrlCEvent);
                    RunCompare(opts, CtrlCEvent.Token, errWriter);
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

                DiffProcessing diffProcessor = new DiffProcessing(stats, diffWriters, opts.reportSameFile);

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
                using (Process currProc = System.Diagnostics.Process.GetCurrentProcess())
                {
                    Misc.ExecUtilWaitHandleSet(paraCmp.Finished, 2000,
                        () => WriteProgress(stats, currProc, paraCmp.Queued, paraCmp.Running, paraCmp.Done, statWriter));
                }

                WriteStatistics(new TimeSpan(DateTime.Now.Ticks - start.Ticks), paraCmp.Done, stats);
                if ( stats.Errors > 0 )
                {
                    Console.Error.WriteLine($"{stats.Errors} erros occoured. please see file {opts.errorFilename}.");
                }
            }
        }
        private static void WriteProgress(Stats stats, Process currProc, long queued, long running, long cmpsDone, StatusLineWriter statWriter)
        {
            currProc.Refresh();

            string privMem      = currProc == null ? "n/a" : Misc.GetPrettyFilesize(currProc.PrivateMemorySize64);
            //string threadcount  = currProc == null ? "n/a" : currProc.Threads.Count.ToString();

            statWriter.Write($"dirs queued/running/done/errors: {queued:N0}/{running}/{cmpsDone:N0}/{stats.Errors:N0}"
                 + $" | new/mod/del/same: {stats.FilesNew:N0}/{stats.FilesMod:N0}/{stats.FilesDel:N0}/{stats.FilesSame:N0}"
                 + $" | privateMem: {privMem}");
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
              $"\n\nnew  files\t{stats.FilesNew, 12:N0}\t{Misc.GetPrettyFilesize(stats.FilesNewBytes)}"
              + $"\nmod  files\t{stats.FilesMod, 12:N0}\t{Misc.GetPrettyFilesize(stats.FilesModBytes)}"
              + $"\ndel  files\t{stats.FilesDel, 12:N0}\t{Misc.GetPrettyFilesize(stats.FilesDelBytes)}"
              + $"\nsame files\t{stats.FilesSame,12:N0}\t{Misc.GetPrettyFilesize(stats.FilesSameBytes)}"
              + $"\nnew  dirs \t{stats.DirsNew,  12:N0}"
              + $"\ndel  dirs \t{stats.DirsDel,  12:N0}");
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
    }
}
