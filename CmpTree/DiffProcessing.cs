using Spi.Data;
using Spi.Native;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Spi;
using System.Runtime.InteropServices.ComTypes;

namespace CmpTrees
{
    public class DiffProcessing
    {
        public readonly Stats _stats;
        public readonly DiffWriter _writers;
        public readonly ConcurrentDictionary<Win32.WIN32_FIND_DATA, List<string>> newFiles;
        public readonly ConcurrentDictionary<Win32.WIN32_FIND_DATA, List<string>> delFiles;

        public DiffProcessing(Stats stats, DiffWriter writers)
        {
            _stats = stats;
            _writers = writers;
            newFiles = new ConcurrentDictionary<Win32.WIN32_FIND_DATA, List<string>>();
            delFiles = new ConcurrentDictionary<Win32.WIN32_FIND_DATA, List<string>>();
        }
        /// <summary>
        /// ATTENZIONE!!!! MULTI-THREADING AHEAD!!!
        /// </summary>
        public void DiffCallback(DIFF_STATE state, string basedir, ref Win32.WIN32_FIND_DATA find_data_a, ref Win32.WIN32_FIND_DATA find_data_b)
        {
            if (state == DIFF_STATE.SAMESAME)
            {
                return;
            }

            Win32.WIN32_FIND_DATA? File_data_ToUse = null;
            ConcurrentDictionary<Win32.WIN32_FIND_DATA, List<string>> DicToUse = null;
            TextWriter toWriteTo;
            switch (state)
            {
                default:
                    throw new Exception($"internal error. no such writer for this kind of state. [{state.ToString()}]");
                case DIFF_STATE.NEW:
                    if (Spi.Misc.IsDirectoryFlagSet(find_data_b))
                    {
                        toWriteTo = _writers.newDirWriter;
                        Interlocked.Increment(ref _stats.DirsNew);
                    }
                    else
                    {
                        toWriteTo = _writers.newWriter;
                        Interlocked.Increment(ref _stats.FilesNew);
                        Interlocked.Add(ref _stats.FilesNewBytes, (long)Misc.GetFilesize(find_data_b));
                        File_data_ToUse = find_data_b;
                        DicToUse = newFiles;
                    }
                    break;
                case DIFF_STATE.MODIFY:
                    toWriteTo = _writers.modWriter;
                    Interlocked.Increment(ref _stats.FilesMod);
                    Interlocked.Add(ref _stats.FilesModBytes, (long)Misc.GetFilesize(find_data_b) - (long)Misc.GetFilesize(find_data_a));
                    File_data_ToUse = find_data_b;
                    break;
                case DIFF_STATE.DELETE:
                    if (Spi.Misc.IsDirectoryFlagSet(find_data_a))
                    {
                        toWriteTo = _writers.delDirWriter;
                        Interlocked.Increment(ref _stats.DirsDel);
                    }
                    else
                    {
                        toWriteTo = _writers.delWriter;
                        Interlocked.Increment(ref _stats.FilesDel);
                        Interlocked.Add(ref _stats.FilesDelBytes, (long)Misc.GetFilesize(find_data_a));
                        File_data_ToUse = find_data_a;
                        DicToUse = newFiles;
                    }
                    break;
            }

            if (DicToUse != null && File_data_ToUse.HasValue)
            {
                DicToUse.AddOrUpdate(
                                key: File_data_ToUse.Value,
                                addValueFactory: (find_data) => new List<string>() { basedir },
                                updateValueFactory: (find_data, listBasedirs) => { listBasedirs.Add(basedir); return listBasedirs; });
            }

            string filenameToPrint = (state == DIFF_STATE.NEW) ? find_data_b.cFileName : find_data_a.cFileName;
            string baseDirToPrint = basedir == null ? String.Empty : basedir + "\\";
            string FullFilename = baseDirToPrint + filenameToPrint;

            if (File_data_ToUse.HasValue)
            {
                var data = File_data_ToUse.Value;
                toWriteTo.WriteLine(
                      $"{Misc.GetFilesize(data)}"
                    + $"\t{ConvertFiletimeToString(data.ftCreationTime, FullFilename, "creationTime")}"
                    + $"\t{ConvertFiletimeToString(data.ftLastWriteTime, FullFilename, "lastWriteTime")}"
                    + $"\t{ConvertFiletimeToString(data.ftLastAccessTime, FullFilename, "lastAccessTime")}"
                    + $"\t{FullFilename}");
            }
            else
            {
                toWriteTo.WriteLine($"{FullFilename}");
            }
        }
        private static string ConvertFiletimeToString(FILETIME filetime, string FullFilename, string KindOfFiletime)
        {
            string result;
            try
            {
                result = Misc.FiletimeToString(filetime);
            }
            catch (System.ComponentModel.Win32Exception wex)
            {
                result = "error";
                System.Console.Error.WriteLine($"error converting filetime: value 0x{Misc.FiletimeToLong(filetime):x}, message: {wex.Message}, TypeOf: {KindOfFiletime}, Filename: [{FullFilename}]");
            }
            return result;
        }

    }
}
