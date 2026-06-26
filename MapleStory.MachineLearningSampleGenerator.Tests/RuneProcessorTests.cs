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
    public sealed class RuneProcessorTests
    {
        [Fact]
        public void Process_CropsCenterSquareAndClearsExistingAnnotations()
        {
            using RuneAssetSet assets = CreateAssetSet(RuneArrowDirection.Right);
            RuneProcessor processor = CreateIdentityProcessor(assets);
            Sample sample = CreateSample(120, 80, new[]
            {
                new TargetItem { X = 1, Y = 1, Width = 2, Height = 2, Type = ObjectClass.Mob },
            });

            processor.Process(sample);

            Assert.Equal(80, sample.Width);
            Assert.Equal(80, sample.Height);
            Assert.Equal(4, sample.Items.Count);
            Assert.All(sample.Items, item => Assert.Equal(ObjectClass.RuneArrow, item.Type));
            using Bitmap output = ReadBitmap(sample.ImageStream);
            Assert.Equal(80, output.Width);
            Assert.Equal(80, output.Height);
        }

        [Theory]
        [InlineData(RuneArrowDirection.Right, 40, 50, 60, 50)]
        [InlineData(RuneArrowDirection.Left, 60, 50, 40, 50)]
        [InlineData(RuneArrowDirection.Up, 50, 55, 50, 45)]
        [InlineData(RuneArrowDirection.Down, 50, 45, 50, 55)]
        public void Process_IdentityTransformKeypointsFollowDirection(
            RuneArrowDirection direction,
            float startX,
            float startY,
            float endX,
            float endY)
        {
            using RuneAssetSet assets = CreateAssetSet(direction);
            RuneProcessor processor = CreateIdentityProcessor(assets, arrowCount: 1);
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            TargetItem item = Assert.Single(sample.Items);
            Assert.Equal(new[] { 40, 45, 20, 10 }, new[] { item.X, item.Y, item.Width, item.Height });
            Assert.Equal(2, item.Keypoints.Count);
            AssertClose(startX, item.Keypoints[0].X);
            AssertClose(startY, item.Keypoints[0].Y);
            AssertClose(endX, item.Keypoints[1].X);
            AssertClose(endY, item.Keypoints[1].Y);
        }

        [Fact]
        public void Process_RandomTransformKeepsKeypointsInsideAnnotation()
        {
            using RuneAssetSet assets = CreateAssetSet(RuneArrowDirection.Right);
            RuneProcessor processor = new RuneProcessor(
                assets,
                new Random(1234),
                new RuneProcessorOptions
                {
                    ArrowCount = 1,
                    EnableBases = false,
                    EnableColorRemap = false,
                    EnableNoise = false,
                });
            Sample sample = CreateSample(300, 300);

            processor.Process(sample);

            TargetItem item = Assert.Single(sample.Items);
            Assert.True(item.Width > 0);
            Assert.True(item.Height > 0);
            Assert.Equal(2, item.Keypoints.Count);
            Assert.All(item.Keypoints, keypoint =>
            {
                Assert.InRange(keypoint.X, item.X - 0.01f, item.X + item.Width + 0.01f);
                Assert.InRange(keypoint.Y, item.Y - 0.01f, item.Y + item.Height + 0.01f);
            });
        }

        [Fact]
        public void Process_NoiseOverlayRemainsGrayscale()
        {
            using RuneAssetSet assets = new RuneAssetSet(
                new[] { new RuneArrowAsset("transparent", RuneArrowDirection.Right, CreateArrowBitmap(Color.Transparent)) },
                new[] { CreateNoiseBitmap() });
            RuneProcessor processor = new RuneProcessor(
                assets,
                new FixedRandom(0),
                new RuneProcessorOptions
                {
                    ArrowCount = 1,
                    EnableBases = false,
                    EnableColorRemap = false,
                    EnableNoise = true,
                    EnableRandomTransforms = false,
                });
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            using Bitmap output = ReadBitmap(sample.ImageStream);
            bool sawNoisePixel = false;
            for (int y = 0; y < output.Height; y++)
            {
                for (int x = 0; x < output.Width; x++)
                {
                    Color pixel = output.GetPixel(x, y);
                    if (pixel.ToArgb() == Color.White.ToArgb())
                    {
                        continue;
                    }

                    sawNoisePixel = true;
                    Assert.Equal(pixel.R, pixel.G);
                    Assert.Equal(pixel.G, pixel.B);
                }
            }

            Assert.True(sawNoisePixel);
        }

        [Fact]
        public void Process_SharedBaseConstrainsArrowsToBaseSpan()
        {
            using RuneAssetSet assets = CreateAssetSet(RuneArrowDirection.Right, new[] { CreateBaseBitmap() });
            RuneProcessor processor = new RuneProcessor(
                assets,
                new FixedRandom(0),
                new RuneProcessorOptions
                {
                    ArrowCount = 4,
                    EnableBases = true,
                    EnableColorRemap = false,
                    EnableNoise = false,
                    EnableRandomTransforms = false,
                });
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            Assert.Equal(4, sample.Items.Count);
            Assert.All(sample.Items, item =>
            {
                Assert.InRange(item.X, 13, 87);
                Assert.InRange(item.X + item.Width, 13, 87);
            });
        }

        [Fact]
        public void Process_BaseProbabilityCanSkipSharedBase()
        {
            using RuneAssetSet assets = CreateAssetSet(RuneArrowDirection.Right, new[] { CreateBaseBitmap() });
            RuneProcessor processor = new RuneProcessor(
                assets,
                new FixedRandom(1),
                new RuneProcessorOptions
                {
                    ArrowCount = 1,
                    EnableBases = true,
                    EnableColorRemap = false,
                    EnableNoise = false,
                });
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            using Bitmap output = ReadBitmap(sample.ImageStream);
            Assert.False(HasBlueBasePixel(output));
        }

        [Fact]
        public void Process_ColorRemapCanChangeMainTone()
        {
            using RuneAssetSet assets = CreateAssetSet(RuneArrowDirection.Right);
            RuneProcessor processor = new RuneProcessor(
                assets,
                new SequenceRandom(
                    new[] { 0, 0, 5 },
                    new[] { 0.80, 0.10, 0.50, 0.50, 0.50, 0.80, 0.80, 0.80, 0.80, 0.80 }),
                new RuneProcessorOptions
                {
                    ArrowCount = 1,
                    EnableBases = false,
                    EnableColorRemap = true,
                    EnableNoise = false,
                });
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            TargetItem item = Assert.Single(sample.Items);
            using Bitmap output = ReadBitmap(sample.ImageStream);
            int blueDominantPixels = 0;
            for (int y = item.Y; y < item.Y + item.Height; y++)
            {
                for (int x = item.X; x < item.X + item.Width; x++)
                {
                    Color pixel = output.GetPixel(x, y);
                    if (pixel.B > pixel.R + 40 && pixel.B > pixel.G)
                    {
                        blueDominantPixels++;
                    }
                }
            }

            Assert.True(blueDominantPixels > 0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void Process_ColorRemapModesGenerateValidOutput(int mode)
        {
            using RuneAssetSet assets = CreateAssetSet(RuneArrowDirection.Right);
            RuneProcessor processor = new RuneProcessor(
                assets,
                new SequenceRandom(new[] { 0, mode }, new[] { 0.0 }),
                new RuneProcessorOptions
                {
                    ArrowCount = 1,
                    EnableBases = false,
                    EnableColorRemap = true,
                    EnableNoise = false,
                });
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            Assert.Single(sample.Items);
            using Bitmap output = ReadBitmap(sample.ImageStream);
            Assert.Equal(100, output.Width);
            Assert.Equal(100, output.Height);
        }

        [Theory]
        [InlineData(0.69, RuneArrowDirection.Left)]
        [InlineData(0.75, RuneArrowDirection.Right)]
        [InlineData(0.85, RuneArrowDirection.Up)]
        [InlineData(0.95, RuneArrowDirection.Down)]
        public void Process_WeightedAssetSelectionUsesArrow7Arrow8Arrow9ThenOthers(
            double bucket,
            RuneArrowDirection expectedDirection)
        {
            using RuneAssetSet assets = CreateWeightedAssetSet();
            RuneProcessor processor = new RuneProcessor(
                assets,
                new SequenceRandom(new[] { 0 }, new[] { bucket }),
                new RuneProcessorOptions
                {
                    ArrowCount = 1,
                    EnableBases = false,
                    EnableColorRemap = false,
                    EnableNoise = false,
                    EnableRandomTransforms = false,
                });
            Sample sample = CreateSample(100, 100);

            processor.Process(sample);

            TargetItem item = Assert.Single(sample.Items);
            AssertDirection(expectedDirection, item);
        }


        private static RuneProcessor CreateIdentityProcessor(RuneAssetSet assets, int arrowCount = 4)
        {
            return new RuneProcessor(
                assets,
                new FixedRandom(0),
                new RuneProcessorOptions
                {
                    ArrowCount = arrowCount,
                    EnableBases = false,
                    EnableColorRemap = false,
                    EnableNoise = false,
                    EnableRandomTransforms = false,
                });
        }

        private static RuneAssetSet CreateAssetSet(RuneArrowDirection direction, Bitmap[] bases = null)
        {
            return new RuneAssetSet(
                new[] { new RuneArrowAsset(direction.ToString(), direction, CreateArrowBitmap(Color.Red), bases) },
                Array.Empty<Bitmap>());
        }

        private static RuneAssetSet CreateWeightedAssetSet()
        {
            return new RuneAssetSet(
                new[]
                {
                    new RuneArrowAsset("arrow7/2", RuneArrowDirection.Left, CreateArrowBitmap(Color.Yellow)),
                    new RuneArrowAsset("arrow8/3", RuneArrowDirection.Right, CreateArrowBitmap(Color.Red)),
                    new RuneArrowAsset("arrow9/1", RuneArrowDirection.Up, CreateArrowBitmap(Color.Green)),
                    new RuneArrowAsset("arrow2/0", RuneArrowDirection.Down, CreateArrowBitmap(Color.Blue)),
                },
                Array.Empty<Bitmap>());
        }

        private static Bitmap CreateArrowBitmap(Color color)
        {
            Bitmap bitmap = new Bitmap(20, 10, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(color);
            return bitmap;
        }

        private static Bitmap CreateNoiseBitmap()
        {
            Bitmap bitmap = new Bitmap(20, 10, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.FromArgb(255, 220, 20, 90));
            return bitmap;
        }

        private static Bitmap CreateBaseBitmap()
        {
            Bitmap bitmap = new Bitmap(80, 12, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Blue);
            return bitmap;
        }

        private static bool HasBlueBasePixel(Bitmap bitmap)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.B > 180 && pixel.R < 80 && pixel.G < 80)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Sample CreateSample(int width, int height, TargetItem[] items = null)
        {
            MemoryStream stream = new MemoryStream();
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                bitmap.Save(stream, ImageFormat.Png);
            }

            stream.Position = 0;
            return new Sample(stream, items ?? Array.Empty<TargetItem>(), width, height);
        }

        private static Bitmap ReadBitmap(Stream stream)
        {
            stream.Position = 0;
            return new Bitmap(stream);
        }

        private static void AssertClose(float expected, float actual)
        {
            Assert.InRange(actual, expected - 0.01f, expected + 0.01f);
        }

        private static void AssertDirection(RuneArrowDirection expectedDirection, TargetItem item)
        {
            TargetKeypoint start = item.Keypoints[0];
            TargetKeypoint end = item.Keypoints[1];
            switch (expectedDirection)
            {
                case RuneArrowDirection.Right:
                    Assert.True(start.X < end.X);
                    AssertClose(start.Y, end.Y);
                    break;
                case RuneArrowDirection.Left:
                    Assert.True(start.X > end.X);
                    AssertClose(start.Y, end.Y);
                    break;
                case RuneArrowDirection.Up:
                    AssertClose(start.X, end.X);
                    Assert.True(start.Y > end.Y);
                    break;
                case RuneArrowDirection.Down:
                    AssertClose(start.X, end.X);
                    Assert.True(start.Y < end.Y);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(expectedDirection), expectedDirection, null);
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

        private sealed class SequenceRandom : Random
        {
            private readonly int[] _ints;
            private readonly double[] _doubles;
            private int _intIndex;
            private int _doubleIndex;

            public SequenceRandom(int[] ints, double[] doubles)
            {
                _ints = ints;
                _doubles = doubles;
            }

            public override int Next(int minValue, int maxValue)
            {
                if (_ints.Length == 0)
                {
                    return minValue;
                }

                int value = _ints[Math.Min(_intIndex, _ints.Length - 1)];
                _intIndex++;
                return minValue + Math.Abs(value) % (maxValue - minValue);
            }

            public override double NextDouble()
            {
                if (_doubles.Length == 0)
                {
                    return 0;
                }

                double value = _doubles[Math.Min(_doubleIndex, _doubles.Length - 1)];
                _doubleIndex++;
                return value;
            }
        }
    }
}
