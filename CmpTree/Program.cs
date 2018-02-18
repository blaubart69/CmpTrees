using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;

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
    }
    class Program
    {
        static int Main(string[] args)
        {
            Opts opts;
            if ((opts = GetOpts(args)) == null)
            {
                return 8;
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
                            Console.Error.WriteLine("going down...");
                            CrtlCEvent.Set();
                            break;
                        }
                    }
                }))
                { IsBackground = true }.Start();

                ThreadPool.SetMaxThreads(64, 1);


                DiffHandler diff = (DIFF_STATE state, string basedir, Win32.WIN32_FIND_DATA find_data_a, Win32.WIN32_FIND_DATA find_data_b) =>
                {
                    if ( state == DIFF_STATE.SAMESAME )
                    {
                        return;
                    }
                    string baseDirToPrint = basedir == null ? String.Empty : basedir + "\\";
                    if ( state == DIFF_STATE.NEW )
                    {
                        Console.Out.WriteLine($"+ {baseDirToPrint}{find_data_b.cFileName}");
                    }
                    else if ( state == DIFF_STATE.DELETE )
                    {
                        Console.Out.WriteLine($"- {baseDirToPrint}{find_data_a.cFileName}");
                    }
                    else
                    {
                        Console.Out.WriteLine($"# {baseDirToPrint}{find_data_a.cFileName}");
                    }
                };

                var enumOpts = new EnumOptions()
                {
                    diffHandler = diff,
                    errorHandler = (int RetCode, string Message) => Console.Error.WriteLine($"E: rc={RetCode} {Message}"),
                    followJunctions = opts.FollowJunctions,
                    maxDepth = opts.Depth
                };

                ManualResetEvent cmpFinished = new ManualResetEvent(false);
                var paraCmp = new CmpDirsParallel(opts.DirA, opts.DirB, enumOpts, CrtlCEvent, cmpFinished);
                paraCmp.Start();
                while ( ! cmpFinished.WaitOne(1000) )
                {
                    
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
                { "h|help",     "show this message and exit",               v => show_help = v != null }            };

            try
            {
                List<string> dirs = p.Parse(args);
                if ( dirs.Count != 2)
                {
                    Console.Error.WriteLine("no two dir's given");
                    return null;
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
                return null;
            }
            if (show_help)
            {
                ShowHelp(p);
                return null;
            }
            return opts;
        }

    }
}
