﻿using System;
using System.IO;
using System.Text;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.Collections.Generic;

namespace Spi.Native
{
    public static class Win32
    {
        public const int ERROR_PATH_NOT_FOUND = 0x00000003;
        public const int ERROR_INVALID_PARAMETER = 0x00000057;
        public const int ERROR_DIRECTORY = 0x10B; // The directory name is invalid.
        public const int ERROR_NO_MORE_FILES = 0x12; // There are no more files.

        [Flags]
        public enum EFileAccess : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_READ_WRITE = GENERIC_READ | GENERIC_WRITE
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            [MarshalAs(UnmanagedType.U2)]
            public short Year;
            [MarshalAs(UnmanagedType.U2)]
            public short Month;
            [MarshalAs(UnmanagedType.U2)]
            public short DayOfWeek;
            [MarshalAs(UnmanagedType.U2)]
            public short Day;
            [MarshalAs(UnmanagedType.U2)]
            public short Hour;
            [MarshalAs(UnmanagedType.U2)]
            public short Minute;
            [MarshalAs(UnmanagedType.U2)]
            public short Second;
            [MarshalAs(UnmanagedType.U2)]
            public short Milliseconds;

            public SYSTEMTIME(DateTime dt)
            {
                dt = dt.ToUniversalTime();  // SetSystemTime expects the SYSTEMTIME in UTC
                Year = (short)dt.Year;
                Month = (short)dt.Month;
                DayOfWeek = (short)dt.DayOfWeek;
                Day = (short)dt.Day;
                Hour = (short)dt.Hour;
                Minute = (short)dt.Minute;
                Second = (short)dt.Second;
                Milliseconds = (short)dt.Millisecond;
            }
        }
        /*
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public FILETIME(uint high, uint low)
            {
                dwLowDateTime = low;
                dwHighDateTime = high;
            }
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }
        */
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct FIND_DATA : IEquatable<FIND_DATA>
        {
            public readonly uint dwFileAttributes;
            public readonly System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public readonly System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public readonly System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            private readonly uint nFileSizeHigh;
            private readonly uint nFileSizeLow;
            private readonly uint dwReserved0;
            private readonly uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public readonly string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public readonly string cAlternateFileName;

            public ulong FileSize
            {
                get
                {
                    return Misc.TwoUIntsToULong(nFileSizeHigh, nFileSizeLow);
                }
            }
            #region OVERRIDE
            public override int GetHashCode()
            {
                var hashCode = -1111092689;
                hashCode = hashCode * -1521134295 + base.GetHashCode();
                //hashCode = hashCode * -1521134295 + EqualityComparer<System.Runtime.InteropServices.ComTypes.FILETIME>.Default.GetHashCode(ftCreationTime);
                //hashCode = hashCode * -1521134295 + EqualityComparer<System.Runtime.InteropServices.ComTypes.FILETIME>.Default.GetHashCode(ftLastAccessTime);
                hashCode = hashCode * -1521134295 + EqualityComparer<System.Runtime.InteropServices.ComTypes.FILETIME>.Default.GetHashCode(ftLastWriteTime);
                hashCode = hashCode * -1521134295 + nFileSizeHigh.GetHashCode();
                hashCode = hashCode * -1521134295 + nFileSizeLow.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(cFileName);
                hashCode = hashCode * -1521134295 + dwFileAttributes.GetHashCode();
                return hashCode;
            }
            public override string ToString()
            {
                return cFileName;
            }
            public override bool Equals(object obj)
            {
                return obj is FIND_DATA && Equals((FIND_DATA)obj);
            }
            public bool Equals(FIND_DATA other)
            {
                return dwFileAttributes == other.dwFileAttributes &&
                       EqualityComparer<System.Runtime.InteropServices.ComTypes.FILETIME>.Default.Equals(ftLastWriteTime, other.ftLastWriteTime) &&
                       nFileSizeHigh == other.nFileSizeHigh &&
                       nFileSizeLow == other.nFileSizeLow &&
                       cFileName == other.cFileName &&
                       FileSize == other.FileSize;
            }
            #endregion
        }

        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern SafeFindHandle FindFirstFile(string lpFileName, out FIND_DATA lpFindFileData);

        public enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }
        public enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2
        }
        [Flags]
        public enum FINDEX_ADDITIONAL_FLAGS : int
        {
            FIND_FIRST_EX_CASE_SENSITIVE = 1,
            FIND_FIRST_EX_LARGE_FETCH = 2,
            FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 4
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern IntPtr FindFirstFileEx(
        string lpFileName,
        FINDEX_INFO_LEVELS fInfoLevelId,
        ref FIND_DATA lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        FINDEX_ADDITIONAL_FLAGS dwAdditionalFlags);


        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "FindFirstFileW")]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern IntPtr FindFirstFile_IntPtr(string lpFileName, ref FIND_DATA lpFindFileData);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool FindNextFile(SafeHandle hFindFile, out FIND_DATA lpFindFileData);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "FindNextFileW")]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool FindNextFile_IntPtr(IntPtr hFindFile, ref FIND_DATA lpFindFileData);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool FindClose(SafeHandle hFindFile);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "FindClose")]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool FindClose_IntPtr(IntPtr hFindFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern uint GetFileAttributes(string lpFileName);

        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern long StrFormatByteSize(
                long fileSize
                , [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer
                , int bufferSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern SafeFileHandle CreateFileW(
             [MarshalAs(UnmanagedType.LPWStr)]  string          filename,
                                                EFileAccess     access,
             [MarshalAs(UnmanagedType.U4)]      FileShare       share,
                                                IntPtr          securityAttributes,
             [MarshalAs(UnmanagedType.U4)]      FileMode        creationDisposition,
             [MarshalAs(UnmanagedType.U4)]      FileAttributes  flagsAndAttributes,
                                                IntPtr          templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool CreateDirectoryW(string lpPathName,IntPtr lpSecurityAttributes);

        public static bool SetFileTime(IntPtr hFile, System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime, System.Runtime.InteropServices.ComTypes.FILETIME lpLastAccessTime, System.Runtime.InteropServices.ComTypes.FILETIME lpLastWriteTime)
        {
            long c = Spi.Misc.FiletimeToLong(lpCreationTime);
            long a = Spi.Misc.FiletimeToLong(lpLastAccessTime);
            long w = Spi.Misc.FiletimeToLong(lpLastWriteTime);

            return
                SetFileTime(
                    hFile: hFile,
                    lpCreationTime:     ref c,
                    lpLastAccessTime:   ref a,
                    lpLastWriteTime:    ref w);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool SetFileTime(IntPtr hFile, ref long lpCreationTime, ref long lpLastAccessTime, ref long lpLastWriteTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool FileTimeToSystemTime(ref System.Runtime.InteropServices.ComTypes.FILETIME ft, out SYSTEMTIME st);

        [DllImport("kernel32.dll", SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        public static extern bool FileTimeToLocalFileTime(ref System.Runtime.InteropServices.ComTypes.FILETIME ftin, ref System.Runtime.InteropServices.ComTypes.FILETIME ftout);

        [DllImport("kernel32.dll", SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemTimeToTzSpecificLocalTime(IntPtr lpTimeZoneInformation, [In] ref SYSTEMTIME lpUniversalTime, out SYSTEMTIME lpLocalTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [System.Security.SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemTimeToFileTime(ref SYSTEMTIME st, out System.Runtime.InteropServices.ComTypes.FILETIME ft);

    }
}
