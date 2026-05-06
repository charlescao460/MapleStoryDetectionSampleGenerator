using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class ConcurrentMapRunner
    {
        public static void Run<TMap>(
            IReadOnlyCollection<TMap> maps,
            int concurrency,
            Action<TMap> runMap,
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
            if (runMap == null)
            {
                throw new ArgumentNullException(nameof(runMap));
            }
            if (onError == null)
            {
                throw new ArgumentNullException(nameof(onError));
            }

            using SemaphoreSlim semaphore = new SemaphoreSlim(concurrency);
            Task[] mapTasks = maps
                .Select(map => Task.Run(() =>
                {
                    semaphore.Wait();
                    try
                    {
                        runMap(map);
                    }
                    catch (Exception ex)
                    {
                        onError(map, ex);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }))
                .ToArray();

            Task.WaitAll(mapTasks);
        }
    }
}
