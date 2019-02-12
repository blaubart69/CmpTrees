using System;
using System.Collections.Generic;

using Spi;

namespace CmpTrees
{
    class GetOpts
    {
        public static Opts Parse(string[] args)
        {
            Opts opts = new Opts();

            bool showHelp = false;
            IList<BeeOpts> CommandLineOpts = new BeeOptsBuilder()
                .Add('d',  "depth",   OPTTYPE.VALUE, "max depth to go down",                    v => opts.Depth = Convert.ToInt32(v))
                .Add('j',  "follow",  OPTTYPE.BOOL,  "follow junctions",                        v => opts.FollowJunctions = (v != null))
                .Add('t',  "threads", OPTTYPE.VALUE, "max enumeration threads parallel",        v => opts.MaxThreads = Convert.ToInt32(v))
                .Add('s',  "same",    OPTTYPE.BOOL,  "report equal files (same.txt)",           v => opts.reportSameFile = true)
                .Add(null, "sorts",   OPTTYPE.BOOL,  "force sorting of entries on source side", v => opts.forceSortSource = true )
                .Add(null, "sortt",   OPTTYPE.BOOL,  "force sorting of entries on target side", v => opts.forceSortTarget = true)
                .Add('h',  "help",    OPTTYPE.BOOL,  "show this help",                          v => showHelp = true)
                .GetOpts();

            IList<string> dirs = BeeOpts.Parse(
                args, 
                CommandLineOpts, 
                OnUnknown: (optname) => Console.Error.WriteLine($"unknow option: [{optname}]"));

            if (showHelp)
            {
                opts = null;
                PrintUsage(CommandLineOpts);
            }
            else
            {
                if (dirs.Count != 2)
                {
                    Console.Error.WriteLine("no two dir's given");
                    opts = null;
                    showHelp = true;
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

                if (showHelp)
                {
                    Spi.BeeOpts.PrintOptions(CommandLineOpts);
                    opts = null;
                }
            }
            return opts;
        }
        private static void PrintUsage(IEnumerable<BeeOpts> CommandOpts)
        {
            Console.Error.WriteLine("Usage: CmpTrees [OPTIONS] {sourceDir} {targetDir}"
            + "\ncompare two directore trees"
            + "\n  new.txt ... exists only in source --> can be copied  to   target"
            + "\n  del.txt ... exists only in target --> can be deleted from target"
            + "\n  mod.txt ... exists in source AND target with the same name but different size or timestamp --> can be copied to target"
            );
            Console.Error.WriteLine("\nOptions:");
            BeeOpts.PrintOptions(CommandOpts);
        }
    }
}
