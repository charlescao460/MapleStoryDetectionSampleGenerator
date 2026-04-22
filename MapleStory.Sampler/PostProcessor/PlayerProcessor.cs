using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using MapRender.Invoker;

namespace MapleStory.Sampler.PostProcessor
{
    /// <summary>
    /// Post-processor by adding generated player avatars and coordinates.
    /// </summary>
    public sealed class PlayerProcessor : IPostProcessor, IDisposable
    {
        private const double VerticalRange = 0.85;
        private const double HorizontalRange = 0.85;

        private readonly IReadOnlyList<PlayerAvatar> _avatars;
        private readonly IReadOnlyList<string> _actions;
        private readonly IReadOnlyList<string> _emotions;
        private readonly int _count;
        private readonly IPlayerFrameSource _frameSource;
        private readonly Random _random;
        private readonly object _sync = new object();
        private readonly Dictionary<string, int> _bodyFrameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _emotionFrameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, PlayerFrame> _frames = new Dictionary<string, PlayerFrame>(StringComparer.Ordinal);
        private bool _disposed;

        public PlayerProcessor(
            IEnumerable<PlayerAvatar> avatars,
            IEnumerable<string> actions,
            IEnumerable<string> emotions,
            int count,
            IPlayerFrameSource frameSource,
            Random random = null)
        {
            _avatars = ToRequiredList(avatars, nameof(avatars));
            _actions = ToRequiredList(actions, nameof(actions));
            _emotions = ToRequiredList(emotions, nameof(emotions));
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Player count must be greater than 0.");
            }

            _count = count;
            _frameSource = frameSource ?? throw new ArgumentNullException(nameof(frameSource));
            _random = random ?? new Random();
        }

        public Sample Process(Sample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            lock (_sync)
            {
                sample.ImageStream = DrawPlayers(sample.ImageStream, sample);
            }

            return sample;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (PlayerFrame frame in _frames.Values)
            {
                frame.Dispose();
            }

            _frames.Clear();
            _disposed = true;
        }

        private MemoryStream DrawPlayers(Stream source, Sample sample)
        {
            source.Position = 0;
            using Bitmap result = new Bitmap(source);
            using Graphics graphics = Graphics.FromImage(result);

            for (int i = 0; i < _count; i++)
            {
                PlayerFrame frame = GetRandomFrame();
                if (frame.BodyBounds.IsEmpty)
                {
                    throw new InvalidOperationException("Generated player frame has empty body bounds.");
                }

                Rectangle bodyBounds = frame.BodyBounds;
                bool flip = _random.NextDouble() > 0.5;
                if (flip)
                {
                    bodyBounds = FlipBodyBounds(frame);
                }

                Point bodyLocation = GetBodyLocation(sample, bodyBounds);
                Point drawLocation = new Point(
                    bodyLocation.X - bodyBounds.X,
                    bodyLocation.Y - bodyBounds.Y);

                DrawFrame(graphics, frame, drawLocation, flip);
                sample.Items.Add(new TargetItem
                {
                    X = bodyLocation.X,
                    Y = bodyLocation.Y,
                    Width = bodyBounds.Width,
                    Height = bodyBounds.Height,
                    Type = ObjectClass.Player,
                });
            }

            MemoryStream output = new MemoryStream();
            result.Save(output, ImageFormat.Png);
            output.Position = 0;
            return output;
        }

        private PlayerFrame GetRandomFrame()
        {
            PlayerAvatar avatar = _avatars[_random.Next(0, _avatars.Count)];
            string action = _actions[_random.Next(0, _actions.Count)];
            string emotion = _emotions[_random.Next(0, _emotions.Count)];
            int bodyFrame = GetRandomFrameIndex(GetBodyFrameCount(avatar, action));
            int emotionFrame = GetRandomFrameIndex(GetEmotionFrameCount(avatar, emotion));

            PlayerFramePose pose = new PlayerFramePose
            {
                BodyAction = action,
                BodyFrame = bodyFrame,
                Emotion = emotion,
                EmotionFrame = emotionFrame,
            };

            string cacheKey = CreateFrameCacheKey(avatar, pose);
            if (!_frames.TryGetValue(cacheKey, out PlayerFrame frame))
            {
                frame = _frameSource.Render(avatar, pose);
                _frames.Add(cacheKey, frame);
            }

            return frame;
        }

        private int GetBodyFrameCount(PlayerAvatar avatar, string action)
        {
            string key = avatar.CacheKey + "|" + action;
            if (!_bodyFrameCounts.TryGetValue(key, out int count))
            {
                count = _frameSource.GetBodyFrameCount(avatar, action);
                if (count <= 0)
                {
                    throw new InvalidOperationException($"Player action '{action}' has no frames.");
                }

                _bodyFrameCounts.Add(key, count);
            }

            return count;
        }

        private int GetEmotionFrameCount(PlayerAvatar avatar, string emotion)
        {
            string key = avatar.CacheKey + "|" + emotion;
            if (!_emotionFrameCounts.TryGetValue(key, out int count))
            {
                count = _frameSource.GetEmotionFrameCount(avatar, emotion);
                if (count < 0)
                {
                    throw new InvalidOperationException($"Player emotion '{emotion}' returned a negative frame count.");
                }

                _emotionFrameCounts.Add(key, count);
            }

            return count;
        }

        private int GetRandomFrameIndex(int frameCount)
        {
            return frameCount <= 0 ? 0 : _random.Next(0, frameCount);
        }

        private Point GetBodyLocation(Sample sample, Rectangle bodyBounds)
        {
            if (bodyBounds.Width > sample.Width || bodyBounds.Height > sample.Height)
            {
                throw new InvalidOperationException(
                    $"Generated player body bounds {bodyBounds.Width}x{bodyBounds.Height} do not fit inside sample {sample.Width}x{sample.Height}.");
            }

            int minX = Math.Max(0, (int)(sample.Width * (1 - HorizontalRange)));
            int minY = Math.Max(0, (int)(sample.Height * (1 - VerticalRange)));
            int maxX = Math.Min(sample.Width - bodyBounds.Width, (int)(sample.Width * HorizontalRange) - bodyBounds.Width);
            int maxY = Math.Min(sample.Height - bodyBounds.Height, (int)(sample.Height * VerticalRange) - bodyBounds.Height);

            if (maxX < minX)
            {
                minX = 0;
                maxX = sample.Width - bodyBounds.Width;
            }

            if (maxY < minY)
            {
                minY = 0;
                maxY = sample.Height - bodyBounds.Height;
            }

            return new Point(_random.Next(minX, maxX + 1), _random.Next(minY, maxY + 1));
        }

        private static Rectangle FlipBodyBounds(PlayerFrame frame)
        {
            Rectangle bounds = frame.BodyBounds;
            return new Rectangle(
                frame.Bitmap.Width - bounds.Right,
                bounds.Y,
                bounds.Width,
                bounds.Height);
        }

        private static void DrawFrame(Graphics graphics, PlayerFrame frame, Point drawLocation, bool flip)
        {
            if (!flip)
            {
                graphics.DrawImageUnscaled(frame.Bitmap, drawLocation);
                return;
            }

            System.Drawing.Drawing2D.GraphicsState state = graphics.Save();
            graphics.TranslateTransform(drawLocation.X + frame.Bitmap.Width, drawLocation.Y);
            graphics.ScaleTransform(-1, 1);
            graphics.DrawImageUnscaled(frame.Bitmap, Point.Empty);
            graphics.Restore(state);
        }

        private static string CreateFrameCacheKey(PlayerAvatar avatar, PlayerFramePose pose)
        {
            return string.Join(
                "|",
                avatar.CacheKey,
                pose.BodyAction,
                pose.BodyFrame.ToString(),
                pose.Emotion,
                pose.EmotionFrame.ToString());
        }

        private static IReadOnlyList<T> ToRequiredList<T>(IEnumerable<T> values, string name)
        {
            if (values == null)
            {
                throw new ArgumentNullException(name);
            }

            T[] array = values.ToArray();
            if (array.Length == 0)
            {
                throw new ArgumentException($"{name} cannot be empty.", name);
            }

            return array;
        }
    }
}
