using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

using Spi.Native;
using Spi.Data;

namespace CmpTrees
{
    public delegate void ErrorHandler(int RetCode, string Message);
    public delegate void DiffHandler(DIFF_STATE state, string basedir, ref Win32.WIN32_FIND_DATA find_data_a, ref Win32.WIN32_FIND_DATA find_data_b);
    
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
        public int maxDepth;
        public bool followJunctions;
        public DiffHandler diffHandler;
        public ErrorHandler errorHandler;
    }

    public class CmpDirsParallel
    {
        readonly string _RootDirA;
        readonly string _RootDirB;
        readonly EnumOptions _opts;
        readonly ManualResetEvent _CtrlCEvent;
        readonly ManualResetEvent _isFinished;

        readonly Queue<ParallelCtx> _workItems;

        int _ThreadpoolUserItemsEnqueued;
        int _maxThreads;

        long _EnumerationsQueued;
        long _EnumerationsRunning;
        long _ComparesDone;

        public CmpDirsParallel(string dira, string dirb, EnumOptions opts, ManualResetEvent CtrlCEvent, ManualResetEvent isFinished)
        {
            _RootDirA = dira;
            _RootDirB = dirb;
            _opts = opts;
            _CtrlCEvent = CtrlCEvent;
            _isFinished = isFinished;
            _workItems = new Queue<ParallelCtx>();
        }
        public void Start()
        {
            Start(32);
        }
        public void Start(int maxThreads)
        {
            if (_EnumerationsRunning != 0)
            {
                throw new Exception("CmpDirsParallel is already running");
            }
            _maxThreads = maxThreads;
            _internal_Start();
        }
        private void _internal_Start()
        {
            _EnumerationsQueued = 1;
            try
            {
                QueueOneDirForCompare(dirSinceRootDir: null, currDepth: -1);
            }
            finally
            {
                DecrementEnumerationQueueCountAndSetFinishedIfZero();
            }
        }
        private void QueueOneDirForCompare(string dirSinceRootDir, int currDepth)
        {
            Interlocked.Increment(ref _EnumerationsQueued);

            bool startNewThread = false;
            lock (_workItems)
            {
                _workItems.Enqueue(new ParallelCtx(dirSinceRootDir, currDepth + 1));

                if ( _ThreadpoolUserItemsEnqueued < _maxThreads )
                {
                    startNewThread = true;
                    Interlocked.Increment(ref _ThreadpoolUserItemsEnqueued);
                }
            }

            if (startNewThread)
            {
                if (!ThreadPool.QueueUserWorkItem(
                    callBack: new WaitCallback(ThreadCmpOneDirectory)))
                {
                    Interlocked.Decrement(ref _ThreadpoolUserItemsEnqueued);
                    throw new Exception("ThreadPool.QueueUserWorkItem returned false. STOP!");
                }
            }
            /*
            if (!ThreadPool.QueueUserWorkItem(
                    callBack:   new WaitCallback(ThreadCmpOneDirectory),
                    state:      new ParallelCtx(dirSinceRootDir, currDepth + 1)))
            {
                Interlocked.Decrement(ref _EnumerationsQueued);
                throw new Exception("ThreadPool.QueueUserWorkItem returned false. STOP!");
            }
            */
        }
        private void DecrementEnumerationQueueCountAndSetFinishedIfZero()
        {
            if (Interlocked.Decrement(ref _EnumerationsQueued) == 0)
            {
                // I'm the last. Enumerations have finished
                _isFinished.Set();
            }
        }
        /***
         * make sure no exception escapes this method!
         * Otherwise we get an "Unhandled exception" and die
         ***/
        private void ThreadCmpOneDirectory(object state)
        {
            try
            {
                Interlocked.Increment(ref _EnumerationsRunning);
                while (true)
                {
                    ParallelCtx ctx = null;
                    lock (_workItems)
                    {
                        if ( _workItems.Count == 0 )
                        {
                            break;
                        }
                        ctx = _workItems.Dequeue();
                    }
                    CompareTwoDirectories(ctx.dirToSearchSinceRootDir, ctx.depth);
                    Interlocked.Increment(ref _ComparesDone);
                    DecrementEnumerationQueueCountAndSetFinishedIfZero();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _opts.errorHandler?.Invoke(99,$"Exception caught (ThreadEnumDir): {ex.Message}\n{ex.StackTrace}");
                }
                catch (Exception ex2)
                {
                    Console.Error.WriteLine("Exception writing exception to ErrorHandler. Bad.");
                    Console.Error.WriteLine($"First exception: {ex.Message}\n{ex.StackTrace}");
                    Console.Error.WriteLine($"Second exception: {ex2.Message}\n{ex2.StackTrace}");
                }
            }
            finally
            {
                Interlocked.Decrement(ref _EnumerationsRunning);
                Interlocked.Decrement(ref _ThreadpoolUserItemsEnqueued);
            }
        }

        private void CompareTwoDirectories(string dirToSearchSinceRootDir, int depth)
        {
            string FullA = BuildFullDirName(_RootDirA, dirToSearchSinceRootDir);
            string FullB = BuildFullDirName(_RootDirB, dirToSearchSinceRootDir);

            CmpDirs.Run(FullA, FullB,
                (DIFF_STATE diffstate, Win32.WIN32_FIND_DATA find_data_a, Win32.WIN32_FIND_DATA find_data_b) =>
                {
                    GetDirToEnum(diffstate, ref find_data_a, ref find_data_b, out string newDirToEnum, out uint attrs);

                    if (newDirToEnum != null && WalkIntoDir(attrs, _opts.followJunctions, depth, _opts.maxDepth))
                    {
                        QueueOneDirForCompare(dirToSearchSinceRootDir == null ? newDirToEnum : Path.Combine(dirToSearchSinceRootDir, newDirToEnum), depth);
                    }
                    else
                    {
                        
                    }
                    _opts.diffHandler(diffstate, dirToSearchSinceRootDir, ref find_data_a, ref find_data_b);
                },
                _opts.errorHandler,
                _CtrlCEvent);
        }

        private static void GetDirToEnum(DIFF_STATE state, ref Win32.WIN32_FIND_DATA find_data_a, ref Win32.WIN32_FIND_DATA find_data_b, out string newDirToEnum, out uint attrs)
        {
            newDirToEnum = null;
            attrs = 0;
            if (state == DIFF_STATE.NEW && Spi.Misc.IsDirectoryFlagSet(find_data_b))
            {
                newDirToEnum    = find_data_b.cFileName;
                attrs           = find_data_b.dwFileAttributes;
            }
            else if (Spi.Misc.IsDirectoryFlagSet(find_data_a))
            {
                newDirToEnum    = find_data_a.cFileName;
                attrs           = find_data_a.dwFileAttributes;
            }
        }

        private static string BuildFullDirName(string RootDir, string dir)
        {
            string dirToEnumerate;
            if (String.IsNullOrEmpty(dir))
            {
                dirToEnumerate = RootDir;
            }
            else
            {
                dirToEnumerate = Path.Combine(RootDir, dir);
            }
            return dirToEnumerate;
        }
        public void GetCounter(out ulong queued, out ulong running, out ulong done)
        {
            queued = (ulong)_EnumerationsQueued;
            running = (ulong)_EnumerationsRunning;
            done = (ulong)_ComparesDone;
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
