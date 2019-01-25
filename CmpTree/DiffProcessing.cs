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

        public DiffProcessing(Stats stats, DiffWriter writers)
        {
            _stats = stats;
            _writers = writers;
        }
        /// <summary>
        /// ATTENZIONE!!!! MULTI-THREADING AHEAD!!!
        /// </summary>
        public void DiffCallback(DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_src, ref Win32.FIND_DATA find_data_trg)
        {
            Win32.FIND_DATA? File_Data_ToUse;
            Win32.FIND_DATA? File_Data_NewDel;
            //ConcurrentDictionary<Win32.FIND_DATA, List<string>> DicToUse;
            TextWriter toWriteTo;

            ProcessDiffState_UpdateCounters(state, ref find_data_src, ref find_data_trg, 
                out toWriteTo, out File_Data_ToUse, out File_Data_NewDel);

            if (toWriteTo == null)
            {
                return;
            }

            string filenameToPrint = (state == DIFF_STATE.NEW) ? find_data_src.cFileName : find_data_trg.cFileName;
            string FullFilename = Path.Combine(basedir, filenameToPrint);

            if (File_Data_ToUse.HasValue)
            {
                var findData = File_Data_ToUse.Value;
                if (state == DIFF_STATE.MODIFY)
                {
                    WriteModifyLine(toWriteTo, ref find_data_src, ref find_data_trg, FullFilename);
                }
                else
                {
                    WriteFindDataLine(toWriteTo, ref findData, FullFilename);
                }
            }
            else
            {
                toWriteTo.WriteLine($"{FullFilename}");
            }
        }
        private void ProcessDiffState_UpdateCounters(DIFF_STATE state, 
            ref Win32.FIND_DATA find_data_src, ref Win32.FIND_DATA find_data_trg, 
            out TextWriter toWriteTo, 
            out Win32.FIND_DATA? File_Data_ToUse, out Win32.FIND_DATA? File_Data_NewDel)
        {
            File_Data_ToUse = null;
            File_Data_NewDel = null;

            switch (state)
            {
                default:
                    throw new Exception($"internal error. no such writer for this kind of state. [{state.ToString()}]");
                case DIFF_STATE.SAMESAME:
                    if (!Spi.Misc.IsDirectory(find_data_src))
                    {
                        Interlocked.Increment(ref _stats.FilesSame);
                        Interlocked.Add(ref _stats.FilesSameBytes, (long)find_data_src.FileSize);
                        toWriteTo = _writers.sameWriter;
                    }
                    else
                    {
                        toWriteTo = null;
                    }
                    break;
                case DIFF_STATE.NEW:
                    if (Spi.Misc.IsDirectory(find_data_src))
                    {
                        Interlocked.Increment(ref _stats.DirsNew);
                        toWriteTo = _writers.newDirWriter;
                    }
                    else
                    {
                        Interlocked.Increment(ref _stats.FilesNew);
                        Interlocked.Add(ref _stats.FilesNewBytes, (long)find_data_src.FileSize);
                        toWriteTo = _writers.newWriter;
                        File_Data_ToUse  = find_data_src;
                        File_Data_NewDel = find_data_src;
                    }

                    break;

                case DIFF_STATE.MODIFY:
                    toWriteTo = _writers.modWriter;
                    Interlocked.Increment(ref _stats.FilesMod);
                    Interlocked.Add(ref _stats.FilesModBytes, (long) find_data_trg.FileSize - (long) find_data_src.FileSize);
                    File_Data_ToUse = find_data_trg;
                    break;

                case DIFF_STATE.DELETE:
                    if (Spi.Misc.IsDirectory(find_data_trg))
                    {
                        toWriteTo = _writers.delDirWriter;
                        Interlocked.Increment(ref _stats.DirsDel);
                    }
                    else
                    {
                        toWriteTo = _writers.delWriter;
                        Interlocked.Increment(ref _stats.FilesDel);
                        Interlocked.Add(ref _stats.FilesDelBytes, (long)find_data_trg.FileSize);
                        File_Data_ToUse  = find_data_trg;
                        File_Data_NewDel = find_data_trg;
                    }
                    break;
            }
        }
        private static void WriteModifyLine(TextWriter writer, ref Win32.FIND_DATA src, ref Win32.FIND_DATA trg, string FullFilename)
        {
            writer.WriteLine(
                "{0:X}|{1:X}"
            + "\t{2}|{3}"            
            + "\t{4}|{5}"
            + "\t{6}"

            , src.dwFileAttributes
                , (trg.dwFileAttributes == src.dwFileAttributes ? "-" : trg.dwFileAttributes.ToString("X")) 

            , ConvertFiletimeToString(src.ftLastWriteTime, FullFilename, "lastWriteTime")
                , CmpDirs.CmpFileTimes(src.ftLastWriteTime, trg.ftLastWriteTime) == 0 ? "-" : ConvertFiletimeToString(trg.ftLastWriteTime, FullFilename, "lastWriteTime")

            , src.FileSize
                , src.FileSize == trg.FileSize ? "-" : trg.FileSize.ToString()

            , FullFilename
            );

        }
        private static void WriteFindDataLine(TextWriter writer, ref Win32.FIND_DATA findData, string FullFilename)
        {
            uint attr = findData.dwFileAttributes;

            writer.WriteLine(
                "{0}{1}{2}{3}{4}\t{5:X}"   // attributes             (human \t machine)
            + "\t{6}\t{7}\t{8}"            // create, access, modify (human)
            + "\t{9}\t{10}"              // filesize, filename    
            , (((attr & (uint)System.IO.FileAttributes.Archive)   != 0) ? 'A' : '-')
            , (((attr & (uint)System.IO.FileAttributes.System)    != 0) ? 'S' : '-')
            , (((attr & (uint)System.IO.FileAttributes.Hidden)    != 0) ? 'H' : '-')
            , (((attr & (uint)System.IO.FileAttributes.ReadOnly)  != 0) ? 'R' : '-')
            , (((attr & (uint)System.IO.FileAttributes.Directory) != 0) ? 'D' : '-')
            , attr
            , ConvertFiletimeToString(findData.ftCreationTime,   FullFilename, "creationTime")
            , ConvertFiletimeToString(findData.ftLastAccessTime, FullFilename, "lastAccessTime")
            , ConvertFiletimeToString(findData.ftLastWriteTime,  FullFilename, "lastWriteTime")
            , findData.FileSize
            , FullFilename
            );

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
