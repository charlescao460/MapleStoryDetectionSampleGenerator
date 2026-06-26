using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MapleStory.Avatar
{
    public sealed class AvatarFrameResult : IDisposable
    {
        private byte[] _pngBytes;
        private bool _disposed;

        public AvatarFrameResult(
            Bitmap bitmap,
            Point origin,
            Point bodyOrigin,
            int bodyWidth,
            int bodyHeight,
            int bodyDelay,
            int emotionDelay,
            int tamingDelay)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            Bitmap = new Bitmap(bitmap);
            Width = Bitmap.Width;
            Height = Bitmap.Height;
            Origin = origin;
            BodyOrigin = bodyOrigin;
            BodyWidth = bodyWidth;
            BodyHeight = bodyHeight;
            BodyDelay = bodyDelay;
            EmotionDelay = emotionDelay;
            TamingDelay = tamingDelay;
        }

        public Bitmap Bitmap { get; }

        public byte[] PngBytes
        {
            get
            {
                ThrowIfDisposed();

                if (_pngBytes == null)
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        Bitmap.Save(stream, ImageFormat.Png);
                        _pngBytes = stream.ToArray();
                    }
                }

                return _pngBytes;
            }
        }

        public int Width { get; }

        public int Height { get; }

        public Point Origin { get; }

        public int BodyWidth { get; }

        public int BodyHeight { get; }

        public Point BodyOrigin { get; }

        public Rectangle BodyBounds
        {
            get
            {
                if (BodyWidth <= 0 || BodyHeight <= 0)
                {
                    return Rectangle.Empty;
                }

                return new Rectangle(
                    Origin.X - BodyOrigin.X,
                    Origin.Y - BodyOrigin.Y,
                    BodyWidth,
                    BodyHeight);
            }
        }

        public int BodyDelay { get; }

        public int EmotionDelay { get; }

        public int TamingDelay { get; }

        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            File.WriteAllBytes(path, PngBytes);
        }

        public void Save(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            stream.Write(PngBytes, 0, PngBytes.Length);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Bitmap.Dispose();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AvatarFrameResult));
            }
        }
    }
}
