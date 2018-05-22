using Spi.Data;
using Spi.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Spi;
using System.Runtime.InteropServices.ComTypes;
using System.Collections.Concurrent;

namespace CmpTrees
{
    public class DiffProcessing
    {
        public readonly Stats _stats;
        public readonly DiffWriter _writers;
        public readonly ConcurrentDictionary<Win32.FIND_DATA, List<string>> newFilesDic;
        public readonly ConcurrentDictionary<Win32.FIND_DATA, List<string>> delFilesDic;

        public DiffProcessing(Stats stats, DiffWriter writers)
        {
            _stats = stats;
            _writers = writers;

            var find_data_Comparer = new FindDataComparer_Name_Size_Modified();
            newFilesDic = new ConcurrentDictionary<Win32.FIND_DATA, List<string>>(find_data_Comparer);
            delFilesDic = new ConcurrentDictionary<Win32.FIND_DATA, List<string>>(find_data_Comparer);
        }
        /// <summary>
        /// ATTENZIONE!!!! MULTI-THREADING AHEAD!!!
        /// </summary>
        public void DiffCallback(DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b)
        {
            Win32.FIND_DATA? File_Data_ToUse = null;
            Win32.FIND_DATA? File_Data_NewDel = null;
            char? NewOrDel = null;
            ConcurrentDictionary<Win32.FIND_DATA, List<string>> DicToUse = null;
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
                        Interlocked.Add(ref _stats.FilesNewBytes, (long)find_data_b.FileSize);
                        File_Data_ToUse = find_data_b;
                        File_Data_NewDel = find_data_b;
                        NewOrDel = 'N';
                        DicToUse = newFilesDic;
                    }
                    break;
                case DIFF_STATE.MODIFY:
                    toWriteTo = _writers.modWriter;
                    Interlocked.Increment(ref _stats.FilesMod);
                    Interlocked.Add(ref _stats.FilesModBytes, (long)find_data_b.FileSize - (long)find_data_a.FileSize);
                    File_Data_ToUse = find_data_b;
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
                        Interlocked.Add(ref _stats.FilesDelBytes, (long)find_data_a.FileSize);
                        File_Data_ToUse = find_data_a;
                        File_Data_NewDel = find_data_a;
                        NewOrDel = 'D';
                        DicToUse = delFilesDic;
                    }
                    break;
            }

            if (DicToUse != null && File_Data_NewDel.HasValue)
            {
                DicToUse.AddOrUpdate(
                    key: File_Data_NewDel.Value,
                    addValueFactory:    (find_data)               =>
                    {
#if DEBUG
                        Console.WriteLine("{0} addValue:\tfile [{1}] basedir [{2}]", NewOrDel, find_data.cFileName, basedir);
#endif
                        return new List<string>() { basedir };
                    },
                    updateValueFactory: (find_data, listBasedirs) => 
                    {
#if DEBUG
                        Console.WriteLine("{0} updateValue:\tfile [{1}] basedir [{2}]", NewOrDel, find_data.cFileName, basedir);
#endif
                        listBasedirs.Add(basedir); return listBasedirs;
                    });
            }

            string filenameToPrint = (state == DIFF_STATE.NEW) ? find_data_b.cFileName : find_data_a.cFileName;
            string baseDirToPrint = basedir == null ? String.Empty : basedir + "\\";
            string FullFilename = baseDirToPrint + filenameToPrint;

            if (File_Data_ToUse.HasValue)
            {
                var data = File_Data_ToUse.Value;
                toWriteTo.WriteLine(
                      $"{data.FileSize}"
                    + $"\t{ConvertFiletimeToString(data.ftCreationTime,   FullFilename, "creationTime")}"
                    + $"\t{ConvertFiletimeToString(data.ftLastWriteTime,  FullFilename, "lastWriteTime")}"
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
