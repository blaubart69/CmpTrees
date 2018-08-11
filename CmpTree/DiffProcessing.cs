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

        public DiffProcessing(Stats stats, DiffWriter writers, bool collectNewAndDelFiles)
        {
            _stats = stats;
            _writers = writers;

            var find_data_Comparer = new FindDataComparer_Name_Size_Modified();

            if (collectNewAndDelFiles)
            {
                newFilesDic = new ConcurrentDictionary<Win32.FIND_DATA, List<string>>(find_data_Comparer);
                delFilesDic = new ConcurrentDictionary<Win32.FIND_DATA, List<string>>(find_data_Comparer);
            }
        }
        /// <summary>
        /// ATTENZIONE!!!! MULTI-THREADING AHEAD!!!
        /// </summary>
        public void DiffCallback(DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b)
        {
            Win32.FIND_DATA? File_Data_ToUse;
            Win32.FIND_DATA? File_Data_NewDel;
            ConcurrentDictionary<Win32.FIND_DATA, List<string>> DicToUse;
            TextWriter toWriteTo;

            ProcessDiffState_UpdateCounters(state, ref find_data_a, ref find_data_b, 
                out toWriteTo, out File_Data_ToUse, out File_Data_NewDel, out DicToUse);

            if (newFilesDic != null && delFilesDic != null)
            {
                if (DicToUse != null && File_Data_NewDel.HasValue)
                {
                    ProcessNewDelDictionary(basedir, ref DicToUse, File_Data_NewDel.Value);
                }
            }

            string filenameToPrint = (state == DIFF_STATE.NEW) ? find_data_b.cFileName : find_data_a.cFileName;
            string baseDirToPrint = basedir == null ? String.Empty : basedir + "\\";
            string FullFilename = baseDirToPrint + filenameToPrint;

            if (File_Data_ToUse.HasValue)
            {
                var findData = File_Data_ToUse.Value;
                WriteDiffLineToFile(toWriteTo, findData, FullFilename);
            }
            else
            {
                toWriteTo.WriteLine($"{FullFilename}");
            }
        }

        private static void WriteDiffLineToFile(TextWriter toWriteTo, Win32.FIND_DATA findData, string FullFilename)
        {
            toWriteTo.WriteLine(
                $"{findData.FileSize}"
                + $"\t{ConvertFiletimeToString(findData.ftCreationTime, FullFilename, "creationTime")}"
                + $"\t{ConvertFiletimeToString(findData.ftLastWriteTime, FullFilename, "lastWriteTime")}"
                + $"\t{ConvertFiletimeToString(findData.ftLastAccessTime, FullFilename, "lastAccessTime")}"
                + $"\t{FullFilename}");
        }

        private static void ProcessNewDelDictionary(string basedir, ref ConcurrentDictionary<Win32.FIND_DATA, List<string>> DicToUse, Win32.FIND_DATA Find_Data)
        {
            DicToUse.AddOrUpdate(
                key: Find_Data,
                addValueFactory:    (find_data)               => { return new List<string>() {basedir}; },
                updateValueFactory: (find_data, listBasedirs) =>
                {
                    listBasedirs.Add(basedir);
                    return listBasedirs;
                });
        }

        private void ProcessDiffState_UpdateCounters(DIFF_STATE state, 
            ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b, 
            out TextWriter toWriteTo, 
            out Win32.FIND_DATA? File_Data_ToUse, out Win32.FIND_DATA? File_Data_NewDel, 
            out ConcurrentDictionary<Win32.FIND_DATA, List<string>> DicToUse)
        {
            File_Data_ToUse = null;
            File_Data_NewDel = null;
            DicToUse = null;

            switch (state)
            {
                default:
                    throw new Exception($"internal error. no such writer for this kind of state. [{state.ToString()}]");
                case DIFF_STATE.NEW:
                    if (Spi.Misc.IsDirectoryFlagSet(find_data_b))
                    {
                        Interlocked.Increment(ref _stats.DirsNew);
                        toWriteTo = _writers.newDirWriter;
                    }
                    else
                    {
                        Interlocked.Increment(ref _stats.FilesNew);
                        Interlocked.Add(ref _stats.FilesNewBytes, (long) find_data_b.FileSize);
                        toWriteTo = _writers.newWriter;
                        File_Data_ToUse = find_data_b;
                        File_Data_NewDel = find_data_b;
                        DicToUse = newFilesDic;
                    }

                    break;

                case DIFF_STATE.MODIFY:
                    toWriteTo = _writers.modWriter;
                    Interlocked.Increment(ref _stats.FilesMod);
                    Interlocked.Add(ref _stats.FilesModBytes, (long) find_data_b.FileSize - (long) find_data_a.FileSize);
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
                        Interlocked.Add(ref _stats.FilesDelBytes, (long) find_data_a.FileSize);
                        File_Data_ToUse = find_data_a;
                        File_Data_NewDel = find_data_a;
                        DicToUse = delFilesDic;
                    }
                    break;
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
