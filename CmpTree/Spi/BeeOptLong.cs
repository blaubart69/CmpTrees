using System;
using System.Linq;
using System.Collections.Generic;

namespace Spi
{
    public delegate void OnOption(string value);
    public delegate void OnUnknownOption(string value);

    public enum OPTTYPE
    {
        VALUE,
        BOOL
    }

    public class BeeOpts
    {
        public readonly char? opt;
        public readonly string optLong;
        public readonly OPTTYPE type;
        public readonly string desc;

        public readonly OnOption OnOptionCallback;

        public BeeOpts(char? opt, string optLong, OPTTYPE type, string desc, OnOption OnOptionCallback)
        {
            this.opt = opt;
            this.optLong = optLong;
            this.desc = desc;
            this.type = type;
            this.OnOptionCallback = OnOptionCallback;
        }
        public static void PrintOptions(IEnumerable<BeeOpts> opts)
        {
            /*
            * Options:
             -b, --basedir=VALUE        appends this basedir to all filenames in the file
             -n, --dryrun               show what would be deleted
             -h, --help                 show this message and exit
            */
            foreach ( BeeOpts o in opts )
            {
                string OneCharOpt = o.opt.HasValue ? $"-{o.opt.Value}," : "   ";
                string valueOpt = o.type == OPTTYPE.VALUE ? "=VALUE" : String.Empty;
                string left = $"  {OneCharOpt} --{o.optLong}{valueOpt}";
                Console.Error.WriteLine($"{left,-30}{o.desc}");
            }
        }
        public static List<string> Parse(string[] args, IList<BeeOpts> opts, OnUnknownOption OnUnknown)
        {
            List<string> parsedArgs = new List<string>();

            int argIdx = 0;
            while (argIdx < args.Length)
            {
                string curr = args[argIdx];

                if ( curr.StartsWith("--") )
                {
                    if (curr.Length > 2)
                    {
                        ParseLong(args, opts, ref argIdx, OnUnknown);
                    }
                    else
                    {
                        parsedArgs.AddRange(args.Skip(argIdx + 1));
                        break;
                    }
                }
                else if ( curr.StartsWith("-"))
                {
                    if (curr.Length > 1)
                    {
                        ParseShort(args, opts, ref argIdx, OnUnknown);
                    }
                    else
                    {
                        throw new Exception($"bad option [{curr}]");
                    }
                }
                else
                {
                    parsedArgs.Add(args[argIdx]);
                }
                ++argIdx;
            }

            return parsedArgs;
        }
        private static void ParseShort(string[] args, IList<BeeOpts> opts, ref int currArgIdx, OnUnknownOption OnUnknown)
        {
            string currArg = args[currArgIdx];
            int shotOptIdx = 1; // skip beginning "-"

            while (shotOptIdx < currArg.Length)
            {
                char curr = currArg[shotOptIdx];

                BeeOpts foundOpt = opts.FirstOrDefault(o => o.opt.HasValue && curr.Equals(o.opt.Value)); 
                if ( foundOpt == null )
                {
                    OnUnknown(curr.ToString());
                    ++shotOptIdx;
                }
                else
                {
                    if ( foundOpt.type == OPTTYPE.BOOL )
                    {
                        foundOpt.OnOptionCallback(null);
                        ++shotOptIdx;
                    }
                    else if (foundOpt.type == OPTTYPE.VALUE)
                    {
                        if (shotOptIdx < currArg.Length - 1)     
                        {
                            foundOpt.OnOptionCallback(currArg.Substring(shotOptIdx + 1));    // rest is the value
                        }
                        else
                        {
                            string value = ReadNextAsArg(args, ref currArgIdx);
                            foundOpt.OnOptionCallback(value);
                        }
                        break;
                    }
                }
            }
        }
        private static void ParseLong(string[] args, IList<BeeOpts> opts, ref int currArgIdx, OnUnknownOption OnUnknown)
        {
            string longOpt = args[currArgIdx].Substring(2);

            string[] optWithValue = longOpt.Split('=');

            string optname;
            if ( optWithValue.Length == 1 || optWithValue.Length == 2)
            {
                optname = optWithValue[0];
            }
            else
            {
                throw new Exception($"bad option [{longOpt}]");
            }

            BeeOpts foundOpt = opts.FirstOrDefault(o => o.optLong.Equals(optname));

            if ( foundOpt == null )
            {
                OnUnknown?.Invoke(optname);
            }
            else
            {
                if (foundOpt.type == OPTTYPE.BOOL)
                {
                    foundOpt.OnOptionCallback(null);
                }
                else if (foundOpt.type == OPTTYPE.VALUE)
                {
                    if (optWithValue.Length == 2)
                    {
                        foundOpt.OnOptionCallback(optWithValue[1]);
                    }
                    else
                    {
                        string value = ReadNextAsArg(args, ref currArgIdx);
                        foundOpt.OnOptionCallback(value);
                    }
                }
            }
        }
        private static string ReadNextAsArg(string[] args, ref int i)
        {
            string value = (i+1) < args.Length ? args[i+1] : null;

            if (value != null)
            {
                if (value.StartsWith("-") || value.StartsWith("--"))
                {
                    return null;
                }
            }

            if ( value != null )
            {
                ++i;
            }

            return value;
        }
    }
    public class BeeOptsBuilder
    {
        private IList<BeeOpts> _data = new List<BeeOpts>();

        public BeeOptsBuilder Add(char? opt, string optLong, OPTTYPE type, string desc, OnOption OptionCallback)
        {
            _data.Add(new BeeOpts(opt, optLong, type, desc, OptionCallback));
            return this;
        }
        public IList<BeeOpts> GetOpts()
        {
            return _data;
        }
    }
}