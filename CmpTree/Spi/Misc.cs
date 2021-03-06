﻿using System;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.IO;

namespace Spi
{
    public class Misc
    {
        public static string NiceDuration(TimeSpan ts)
        {
            string res;
            if (ts.TotalHours >= 24)
            {
                res = String.Format("{0}d {1}h {2}m {3}s {4}ms", ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            }
            else if (ts.TotalMinutes >= 60)
            {
                res = String.Format("{0}h {1}m {2}s {3}ms", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            }
            else if (ts.TotalSeconds >= 60)
            {
                res = String.Format("{0}m {1}s {2}ms", ts.Minutes, ts.Seconds, ts.Milliseconds);
            }
            else if (ts.TotalMilliseconds >= 1000)
            {
                res = String.Format("{0}s {1}ms", ts.Seconds, ts.Milliseconds);
            }
            else
            {
                res = String.Format("{0}ms", ts.Milliseconds);
            }
            return res;
        }
        public static string GetPrettyFilesize(ulong Filesize)
        {
            const int MaxLen = 32;
            StringBuilder sb = new StringBuilder(MaxLen);
            Spi.Native.Win32.StrFormatByteSize((long)Filesize, sb, MaxLen);
            return sb.ToString();
        }
        public static string GetPrettyFilesize(long Filesize)
        {
            if (Filesize >= 0)
            {
                return GetPrettyFilesize((ulong)Filesize);
            }
            else
            {
                long positiveSize = -Filesize;
                return "-" + GetPrettyFilesize((ulong)positiveSize);
            }
        }
        public static string FiletimeToString(FILETIME filetime)
        {
            Native.Win32.SYSTEMTIME universalSystemtime;
            if ( ! Native.Win32.FileTimeToSystemTime(ref filetime, out universalSystemtime) )
            {
                throw new System.ComponentModel.Win32Exception();
            }
            /**
             *  universalTime:
             *  year:   30828
             *  month:  9
             *  day:    14
             *  hour:   2
             *  minute: 48
             *  second: 5
             *  milli:  477
            */

            Native.Win32.SYSTEMTIME localSystemtime;
            if ( ! Native.Win32.SystemTimeToTzSpecificLocalTime(IntPtr.Zero, ref universalSystemtime, out localSystemtime) )
            {
                throw new System.ComponentModel.Win32Exception();
            }

            return FormatSystemtime(localSystemtime);
        }
        public static string FormatSystemtime(Native.Win32.SYSTEMTIME sysTime)
        {
            return $"{sysTime.Year}.{sysTime.Month:00}.{sysTime.Day:00} {sysTime.Hour:00}:{sysTime.Minute:00}:{sysTime.Second:00}";
        }
        public static long TwoIntToLong(int high, int low)
        {
            ulong h = (ulong)high << 32;
            ulong l = (uint)low;

            ulong    u_result = h | l;
            long result = (long)u_result;

            return result; 
        }
        public static ulong TwoUIntsToULong(uint high, uint low)
        {
            ulong h = (ulong)high << 32;

            return h | (ulong)low;
        }
        public static long FiletimeToLong(FILETIME ft)
        {
            return TwoIntToLong(ft.dwHighDateTime, ft.dwLowDateTime);
        }
        public static bool IsDotOrDotDotDirectory(string Filename)
        {
            if (Filename[0] == '.')
            {
                if (Filename.Length == 1)
                {
                    return true;
                }
                if (Filename[1] == '.')
                {
                    if (Filename.Length == 2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool IsDirectory(uint dwFileAttributes)
        {
            return (dwFileAttributes & 0x10) != 0;
        }
        public static bool IsDirectory(Spi.Native.Win32.FIND_DATA find_data)
        {
            return IsDirectory(find_data.dwFileAttributes);
        }
        
        public static void ExecUtilWaitHandleSet(WaitHandle waitHandle, int milliSeconds, Action doEvery)
        {
            while ( !waitHandle.WaitOne(milliSeconds) )
            {
                doEvery?.Invoke();
            }
            doEvery?.Invoke();
        }
        public static int CompareULongsToInt(ulong a, ulong b)
        {
            if (a < b)
            {
                return -1;
            }
            else if (a > b)
            {
                return 1;
            }

            return 0;
        }
        public static int CmpFileTimes(System.Runtime.InteropServices.ComTypes.FILETIME a, System.Runtime.InteropServices.ComTypes.FILETIME b)
        {
            long aLong = Misc.FiletimeToLong(a);
            long bLong = Misc.FiletimeToLong(b);

            return CompareULongsToInt((ulong)aLong, (ulong)bLong);
        }
    }
}
