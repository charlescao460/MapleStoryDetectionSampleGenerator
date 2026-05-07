using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MapleStory.Sampler;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public sealed class ConcurrentMapRunnerTests
    {
        [Fact]
        public void Run_LimitsActiveMaps()
        {
            int active = 0;
            int maxActive = 0;
            ConcurrentBag<int> processed = new ConcurrentBag<int>();

            ConcurrentMapRunner.Run(
                new[] { 1, 2, 3, 4, 5, 6 },
                2,
                _ => new object(),
                (_, map) =>
                {
                    int current = Interlocked.Increment(ref active);
                    UpdateMax(ref maxActive, current);
                    Thread.Sleep(50);
                    processed.Add(map);
                    Interlocked.Decrement(ref active);
                },
                (_, ex) => throw new InvalidOperationException("Unexpected map error.", ex));

            Assert.Equal(6, processed.Count);
            Assert.True(maxActive <= 2);
        }

        [Fact]
        public void Run_ContinuesAfterNonfatalMapError()
        {
            ConcurrentBag<int> processed = new ConcurrentBag<int>();
            ConcurrentBag<int> failed = new ConcurrentBag<int>();

            ConcurrentMapRunner.Run(
                new[] { 1, 2, 3 },
                2,
                _ => new object(),
                (_, map) =>
                {
                    if (map == 2)
                    {
                        throw new InvalidOperationException("sample failure");
                    }

                    processed.Add(map);
                },
                (map, _) => failed.Add(map));

            Assert.Contains(1, processed);
            Assert.Contains(3, processed);
            Assert.DoesNotContain(2, processed);
            Assert.Equal(new[] { 2 }, failed.ToArray());
        }

        [Fact]
        public void Run_ReusesWorkersAcrossMaps()
        {
            int createdWorkers = 0;
            ConcurrentDictionary<int, int> processedByWorker = new ConcurrentDictionary<int, int>();

            ConcurrentMapRunner.Run(
                new[] { 1, 2, 3, 4, 5 },
                2,
                _ => new TrackingWorker(Interlocked.Increment(ref createdWorkers)),
                (worker, _) => processedByWorker.AddOrUpdate(worker.Id, 1, (_, count) => count + 1),
                (_, ex) => throw new InvalidOperationException("Unexpected map error.", ex));

            Assert.Equal(2, createdWorkers);
            Assert.Equal(5, processedByWorker.Values.Sum());
            Assert.Contains(processedByWorker.Values, count => count > 1);
        }

        [Fact]
        public void CocoWriter_SupportsConcurrentWrites()
        {
            string rootPath = Path.Combine(Path.GetTempPath(), "MapleStorySamplerTests", Guid.NewGuid().ToString("N"));
            try
            {
                CocoWriter writer = new CocoWriter(rootPath, "concurrent-test");

                Parallel.For(0, 20, _ =>
                    writer.Write(new Sample(
                        new MemoryStream(new byte[] { 1, 2, 3 }),
                        Array.Empty<MapRender.Invoker.TargetItem>(),
                        1,
                        1)));
                writer.Finish();

                int jpgCount = Directory.GetFiles(writer.RootPath, "*.jpg", SearchOption.AllDirectories).Length;
                int trainImageCount = CountCocoImages(Path.Combine(writer.AnnotationsPath, "instances_train2017.json"));
                int valImageCount = CountCocoImages(Path.Combine(writer.AnnotationsPath, "instances_val2017.json"));

                Assert.Equal(20, jpgCount);
                Assert.Equal(20, trainImageCount + valImageCount);
            }
            finally
            {
                if (Directory.Exists(rootPath))
                {
                    Directory.Delete(rootPath, true);
                }
            }
        }

        private static void UpdateMax(ref int target, int value)
        {
            int snapshot;
            do
            {
                snapshot = target;
                if (value <= snapshot)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, snapshot) != snapshot);
        }

        private static int CountCocoImages(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            return document.RootElement.GetProperty("images").EnumerateArray().Count();
        }

        private sealed class TrackingWorker
        {
            public TrackingWorker(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }
    }
}
