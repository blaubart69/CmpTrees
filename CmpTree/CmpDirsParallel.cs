using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

using Spi.Native;
using Spi.Data;

namespace CmpTrees
{
    public delegate void ErrorHandler(int RetCode, string Message);
    public delegate void DiffHandler(DIFF_STATE state, string basedir, Win32.FIND_DATA find_data_a, Win32.FIND_DATA find_data_b);
    
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
    }

    public class CmpDirsParallel
    {
        readonly string _RootDirA;
        readonly string _RootDirB;
        readonly EnumOptions _opts;
        readonly DiffHandler _diffHandler;
        readonly ErrorHandler _errorHandler;
        readonly ManualResetEvent _CtrlCEvent;
        readonly ManualResetEvent _isFinished;

        readonly Queue<ParallelCtx> _workItems;

        int _ThreadpoolUserItemsEnqueued;
        int _maxThreads;

        long _EnumerationsQueued;
        long _EnumerationsRunning;
        long _ComparesDone;

        public long Queued
        {
            get { return _EnumerationsQueued; }
        }
        public long Running
        {
            get { return _EnumerationsRunning;  }
        }
        public long Done
        {
            get { return _ComparesDone;  }
        }
        public WaitHandle IsFinished
        {
            get { return _isFinished; }
        }

        public CmpDirsParallel(string dira, string dirb, EnumOptions opts, DiffHandler diffHandler, ErrorHandler errorHandler, ManualResetEvent CtrlCEvent)
        {
            _RootDirA = dira;
            _RootDirB = dirb;
            _opts = opts;
            _diffHandler = diffHandler;
            _errorHandler = errorHandler;
            _CtrlCEvent = CtrlCEvent;
            _isFinished = new ManualResetEvent(false);
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
//#if DEBUG
//            Console.WriteLine("QueueOneDirForCompare: [{0}]", dirSinceRootDir, _RootDirA, _RootDirB);
//#endif


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
                    try
                    {
                        CompareTwoDirectories(ctx.dirToSearchSinceRootDir, ctx.depth);
                    }
                    catch (Exception ex)
                    {
                        _errorHandler?.Invoke(99, $"Exception caught (ThreadEnumDir-CompareTwoDirectories): {ex.Message}\n{ex.StackTrace}");
                    }
                    Interlocked.Increment(ref _ComparesDone);
                    DecrementEnumerationQueueCountAndSetFinishedIfZero();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _errorHandler?.Invoke(99,$"Exception caught (ThreadEnumDir): {ex.Message}\n{ex.StackTrace}");
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

#if DEBUG
            Console.WriteLine("cmpdir: base [{0}]\ta [{1}]\tb [{2}]", dirToSearchSinceRootDir, _RootDirA, _RootDirB);
#endif

            CmpDirs.Run(FullA, FullB,
                (DIFF_STATE diffstate, Win32.FIND_DATA find_data_a, Win32.FIND_DATA find_data_b) =>
                {
#if DEBUG
                    Console.WriteLine("state: {0}\ta [{1}]\tb [{2}]", diffstate.ToString(), find_data_a.ToString(), find_data_b.ToString());
#endif

                    GetDirToEnum(diffstate, find_data_a, find_data_b, out string newDirToEnum, out uint attrs);

                    if (newDirToEnum != null && WalkIntoDir(attrs, _opts.followJunctions, depth, _opts.maxDepth))
                    {
                        QueueOneDirForCompare(dirToSearchSinceRootDir == null ? newDirToEnum : Path.Combine(dirToSearchSinceRootDir, newDirToEnum), depth);
                    }
                    else
                    {
                        
                    }
                    _diffHandler(diffstate, dirToSearchSinceRootDir, find_data_a, find_data_b);
                },
                _errorHandler,
                _CtrlCEvent);
        }

        private static void GetDirToEnum(DIFF_STATE state, Win32.FIND_DATA find_data_a, Win32.FIND_DATA find_data_b, out string newDirToEnum, out uint attrs)
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
