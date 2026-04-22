using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using MapleStory.Sampler;
using MapleStory.Sampler.PostProcessor;
using MapRender.Invoker;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public sealed class PlayerProcessorTests
    {
        [Fact]
        public void Process_DrawsFullFrameButLabelsBodyBounds()
        {
            using FakePlayerFrameSource frameSource = new FakePlayerFrameSource(CreateFrame());
            using PlayerProcessor processor = CreateProcessor(frameSource, 1, new FixedRandom(0));
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            TargetItem item = Assert.Single(sample.Items);
            Assert.Equal(ObjectClass.Player, item.Type);
            Assert.Equal(15, item.X);
            Assert.Equal(15, item.Y);
            Assert.Equal(8, item.Width);
            Assert.Equal(6, item.Height);
            using Bitmap output = ReadBitmap(sample.ImageStream);
            Assert.Equal(Color.Red.ToArgb(), output.GetPixel(10, 13).ToArgb());
        }

        [Fact]
        public void Process_FlipAdjustsBodyBoundsBeforeDrawing()
        {
            using FakePlayerFrameSource frameSource = new FakePlayerFrameSource(CreateFrame());
            using PlayerProcessor processor = CreateProcessor(frameSource, 1, new FixedRandom(1));
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            TargetItem item = Assert.Single(sample.Items);
            Assert.Equal(new[] { 15, 15, 8, 6 }, new[] { item.X, item.Y, item.Width, item.Height });
            using Bitmap output = ReadBitmap(sample.ImageStream);
            Assert.Equal(Color.Red.ToArgb(), output.GetPixel(27, 13).ToArgb());
            Assert.NotEqual(Color.Red.ToArgb(), output.GetPixel(29, 13).ToArgb());
        }

        [Fact]
        public void Process_CountControlsGeneratedPlayerAnnotations()
        {
            using FakePlayerFrameSource frameSource = new FakePlayerFrameSource(CreateFrame());
            using PlayerProcessor processor = CreateProcessor(frameSource, 2, new FixedRandom(0));
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            Assert.Equal(2, sample.Items.Count(item => item.Type == ObjectClass.Player));
            Assert.All(sample.Items, item =>
            {
                Assert.InRange(item.X, 0, sample.Width - item.Width);
                Assert.InRange(item.Y, 0, sample.Height - item.Height);
            });
        }

        private static PlayerProcessor CreateProcessor(
            IPlayerFrameSource frameSource,
            int count,
            Random random)
        {
            return new PlayerProcessor(
                new[] { new PlayerAvatar(new[] { 2000, 12003, 20000, 30000 }) },
                new[] { "stand1" },
                new[] { "default" },
                count,
                frameSource,
                random);
        }

        private static PlayerFrame CreateFrame()
        {
            using Bitmap bitmap = new Bitmap(20, 10, PixelFormat.Format32bppArgb);
            bitmap.SetPixel(0, 0, Color.Red);
            return new PlayerFrame(bitmap, new Rectangle(5, 2, 8, 6));
        }

        private static Sample CreateSample(int width, int height)
        {
            MemoryStream stream = new MemoryStream();
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                bitmap.Save(stream, ImageFormat.Png);
            }

            stream.Position = 0;
            return new Sample(stream, Array.Empty<TargetItem>(), width, height);
        }

        private static Bitmap ReadBitmap(Stream stream)
        {
            stream.Position = 0;
            return new Bitmap(stream);
        }

        private sealed class FakePlayerFrameSource : IPlayerFrameSource, IDisposable
        {
            private readonly PlayerFrame _frame;

            public FakePlayerFrameSource(PlayerFrame frame)
            {
                _frame = frame;
            }

            public int GetBodyFrameCount(PlayerAvatar avatar, string action)
            {
                return 1;
            }

            public int GetEmotionFrameCount(PlayerAvatar avatar, string emotion)
            {
                return 1;
            }

            public PlayerFrame Render(PlayerAvatar avatar, PlayerFramePose pose)
            {
                return new PlayerFrame(_frame.Bitmap, _frame.BodyBounds);
            }

            public void Dispose()
            {
                _frame.Dispose();
            }
        }

        private sealed class FixedRandom : Random
        {
            private readonly double _nextDouble;

            public FixedRandom(double nextDouble)
            {
                _nextDouble = nextDouble;
            }

            public override int Next()
            {
                return 0;
            }

            public override int Next(int maxValue)
            {
                return 0;
            }

            public override int Next(int minValue, int maxValue)
            {
                return minValue;
            }

            public override double NextDouble()
            {
                return _nextDouble;
            }
        }
    }
}
