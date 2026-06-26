using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class ConcurrentMapRunner
    {
        public static void Run<TMap, TWorker>(
            IReadOnlyList<TMap> maps,
            int concurrency,
            Func<int, TWorker> createWorker,
            Action<TWorker, TMap> runMap,
            Action<TMap, Exception> onError)
        {
            if (maps == null)
            {
                throw new ArgumentNullException(nameof(maps));
            }
            if (concurrency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(concurrency), concurrency, "Concurrency must be greater than 0.");
            }
            if (createWorker == null)
            {
                throw new ArgumentNullException(nameof(createWorker));
            }
            if (runMap == null)
            {
                throw new ArgumentNullException(nameof(runMap));
            }
            if (onError == null)
            {
                throw new ArgumentNullException(nameof(onError));
            }

            if (maps.Count == 0)
            {
                return;
            }

            int workerCount = Math.Min(concurrency, maps.Count);
            int nextMapIndex = -1;
            Task[] workerTasks = Enumerable.Range(0, workerCount)
                .Select(workerIndex => Task.Run(() =>
                {
                    TWorker worker = createWorker(workerIndex);
                    try
                    {
                        while (true)
                        {
                            int mapIndex = Interlocked.Increment(ref nextMapIndex);
                            if (mapIndex >= maps.Count)
                            {
                                break;
                            }

                            TMap map = maps[mapIndex];
                            try
                            {
                                runMap(worker, map);
                            }
                            catch (Exception ex)
                            {
                                onError(map, ex);
                            }
                        }
                    }
                    finally
                    {
                        if (worker is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }))
                .ToArray();

            Task.WaitAll(workerTasks);
        }
    }
}
