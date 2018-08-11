using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

using Spi.Native;
using Spi.Data;
using System.Text;

namespace CmpTrees
{
    public delegate void ErrorHandler(int RetCode, string Message);
    public delegate void DiffHandler(DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b);
    
    class ParallelCtx
    {
        public readonly int       depth;
        public readonly string    dirToSearchSinceRootDir;

        public ParallelCtx(string DirToSearchSinceRootDir, int depth)
        {
            this.depth = depth;
            this.dirToSearchSinceRootDir = DirToSearchSinceRootDir;
        }
    }
    
    public class EnumOptions
    {
        public int maxDepth = -1;
        public bool followJunctions = false;
        public bool forceSortA = false;
        public bool forceSortB = false;
    }

    public class RootDirs
    {
        public StringBuilder A;
        public StringBuilder B;
        public readonly int initialLenA;
        public readonly int initialLenB;

        public RootDirs(string a, string b)
        {
            A = new StringBuilder(a);
            B = new StringBuilder(b);

            initialLenA = A.Length;
            initialLenB = B.Length;
        }
    }

    public class CmpDirsParallel
    {
        readonly EnumOptions _opts;

        readonly DiffHandler        _diffHandler;
        readonly ErrorHandler       _errorHandler;
        readonly CancellationToken  _CtrlCEvent;

        readonly Spi.ParallelExecutor<ParallelCtx, object, RootDirs> _executor;

        #region PROPERTIES
        public long Queued
        {
            get { return _executor.Queued; }
        }
        public long Running
        {
            get { return _executor.Running;  }
        }
        public long Done
        {
            get { return _executor.Done;  }
        }
        public WaitHandle Finished { get { return _executor.Finished; } }
        #endregion

        public CmpDirsParallel(string dira, string dirb, EnumOptions opts, DiffHandler diffHandler, ErrorHandler errorHandler, CancellationToken CtrlCEvent, int maxThreadsToRun)
        {
            _opts = opts;
            _diffHandler = diffHandler;
            _errorHandler = errorHandler;
            _CtrlCEvent = CtrlCEvent;

            _executor = new Spi.ParallelExecutor<ParallelCtx, object, RootDirs>(
                initTL: () => new RootDirs(dira, dirb)
                , func: CompareTwoDirectories
                , freeTL: null
                , context: null
                , maxThreads: maxThreadsToRun
                , cancel: CtrlCEvent
                );

        }
        public void Start()
        {
            _executor.Enqueue(new ParallelCtx(String.Empty, 0));
        }
        //public delegate void WorkFunc(T item, ParallelExecutor<T, C, TL> executor, C context, ref TL threadLocalObject);

        private void CompareTwoDirectories(ParallelCtx relativeDir, Spi.ParallelExecutor<ParallelCtx, object, RootDirs> executor, object ctx, ref RootDirs rootDir)
        {
            AppendDir(ref rootDir.A, relativeDir.dirToSearchSinceRootDir);
            AppendDir(ref rootDir.B, relativeDir.dirToSearchSinceRootDir);

            CmpDirs.Run(rootDir.A, rootDir.B,
                (DIFF_STATE diffstate, Win32.FIND_DATA find_data_a, Win32.FIND_DATA find_data_b) =>
                {
                    GetDirToEnum(diffstate, ref find_data_a, ref find_data_b, out string newDirToEnum, out uint attrs);

                    if (newDirToEnum != null && WalkIntoDir(attrs, _opts.followJunctions, relativeDir.depth, _opts.maxDepth))
                    {
                        executor.Enqueue(new ParallelCtx(Path.Combine(relativeDir.dirToSearchSinceRootDir, newDirToEnum), relativeDir.depth + 1));
                    }

                    if (diffstate != DIFF_STATE.SAMESAME)
                    {
                        _diffHandler(diffstate, relativeDir.dirToSearchSinceRootDir, ref find_data_a, ref find_data_b);
                    }
                },
                this._opts.forceSortA,
                this._opts.forceSortB,
                _errorHandler);

            rootDir.A.Length = rootDir.initialLenA;
            rootDir.B.Length = rootDir.initialLenB;
        }

        private static void GetDirToEnum(DIFF_STATE state, ref Win32.FIND_DATA find_data_a, ref Win32.FIND_DATA find_data_b, out string newDirToEnum, out uint attrs)
        {
            newDirToEnum = null;
            attrs = 0;
            if (state == DIFF_STATE.NEW && Spi.Misc.IsDirectoryFlagSet(find_data_b))
            {
                newDirToEnum    = find_data_b.cFileName;
                attrs           = find_data_b.dwFileAttributes;
            }
            else if (state == DIFF_STATE.DELETE && Spi.Misc.IsDirectoryFlagSet(find_data_a))
            {
                newDirToEnum = find_data_a.cFileName;
                attrs        = find_data_a.dwFileAttributes;
            }
            else if (state == DIFF_STATE.SAMESAME && Spi.Misc.IsDirectoryFlagSet(find_data_a) && Spi.Misc.IsDirectoryFlagSet(find_data_b))
            {
                newDirToEnum = find_data_a.cFileName;
                attrs = find_data_a.dwFileAttributes;
            }
        }
        private static void AppendDir(ref StringBuilder dir, string dirToAppend)
        {
            if ( !String.IsNullOrEmpty(dirToAppend) )
            {
                dir.Append('\\');
                dir.Append(dirToAppend);
            }
        }
        private static bool WalkIntoDir(uint dwFileAttributes, bool FollowJunctions, int currDepth, int maxDepth)
        {
            bool enterDir = true;

            if (maxDepth > -1)
            {
                if (currDepth + 1 > maxDepth)
                {
                    return false;
                }
            }

            const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
            if ((dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
            {
                if (FollowJunctions == false)
                {
                    enterDir = false;
                }
            }

            return enterDir;

        }
    }
}
