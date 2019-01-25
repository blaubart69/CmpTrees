using System;
using System.IO;
using System.Threading;

using Spi.Native;
using Spi.Data;
using System.Text;

namespace CmpTrees
{
    public delegate void ErrorHandler(int RetCode, string Message);
    public delegate void DiffHandler(DIFF_STATE state, string basedir, ref Win32.FIND_DATA find_data_src, ref Win32.FIND_DATA find_data_trg);
    
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
        public int  maxDepth = -1;
        public bool followJunctions = false;
        public bool forceSortSource = false;
        public bool forceSortTarget = false;
    }

    public class RootDirs
    {
        public StringBuilder source;
        public StringBuilder target;
        public readonly int initialLenSource;
        public readonly int initialLenTarget;

        public RootDirs(string source, string target)
        {
            this.source = new StringBuilder(source);
            this.target = new StringBuilder(target);

            initialLenSource = this.source.Length;
            initialLenTarget = this.target.Length;
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

        public CmpDirsParallel(string sourceDir, string targetDir, EnumOptions opts, DiffHandler diffHandler, ErrorHandler errorHandler, CancellationToken CtrlCEvent, int maxThreadsToRun)
        {
            _opts = opts;
            _diffHandler = diffHandler;
            _errorHandler = errorHandler;
            _CtrlCEvent = CtrlCEvent;

            _executor = new Spi.ParallelExecutor<ParallelCtx, object, RootDirs>(
                initTL: () => new RootDirs(sourceDir, targetDir)
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
        private void CompareTwoDirectories(ParallelCtx relativeDir, Spi.ParallelExecutor<ParallelCtx, object, RootDirs> executor, object ctx, ref RootDirs rootDir)
        {
            AppendDir(ref rootDir.source, relativeDir.dirToSearchSinceRootDir);
            AppendDir(ref rootDir.target, relativeDir.dirToSearchSinceRootDir);

            CmpDirs.Run(rootDir.source, rootDir.target,
                (DIFF_STATE diffstate, Win32.FIND_DATA find_data_src, Win32.FIND_DATA find_data_trg) =>
                {
                    GetDirToEnum(diffstate, ref find_data_src, ref find_data_trg, out string newDirToEnum, out uint attrs);

                    if (newDirToEnum != null && WalkIntoDir(attrs, _opts.followJunctions, relativeDir.depth, _opts.maxDepth))
                    {
                        executor.Enqueue(new ParallelCtx(Path.Combine(relativeDir.dirToSearchSinceRootDir, newDirToEnum), relativeDir.depth + 1));
                    }

                    //if (diffstate != DIFF_STATE.SAMESAME)
                    //{
                        _diffHandler(diffstate, relativeDir.dirToSearchSinceRootDir, ref find_data_src, ref find_data_trg);
                    //}
                },
                this._opts.forceSortSource,
                this._opts.forceSortTarget,
                _errorHandler);

            rootDir.source.Length = rootDir.initialLenSource;
            rootDir.target.Length = rootDir.initialLenTarget;
        }

        private static void GetDirToEnum(DIFF_STATE state, ref Win32.FIND_DATA find_data_src, ref Win32.FIND_DATA find_data_trg, out string newDirToEnum, out uint attrs)
        {
            newDirToEnum = null;
            attrs = 0;
            if (state == DIFF_STATE.NEW && Spi.Misc.IsDirectory(find_data_src))
            {
                newDirToEnum    = find_data_src.cFileName;
                attrs           = find_data_src.dwFileAttributes;
            }
            else if (state == DIFF_STATE.DELETE && Spi.Misc.IsDirectory(find_data_trg))
            {
                newDirToEnum = find_data_trg.cFileName;
                attrs        = find_data_trg.dwFileAttributes;
            }
            else if (state == DIFF_STATE.SAMESAME && Spi.Misc.IsDirectory(find_data_src) && Spi.Misc.IsDirectory(find_data_trg))
            {
                newDirToEnum = find_data_src.cFileName;
                attrs        = find_data_src.dwFileAttributes;
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
