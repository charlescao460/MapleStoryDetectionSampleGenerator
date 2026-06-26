using System;
using System.Drawing;

namespace MapleStory.Sampler.PostProcessor
{
    public sealed class PlayerFrame : IDisposable
    {
        private bool _disposed;

        public PlayerFrame(Bitmap bitmap, Rectangle bodyBounds)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            Bitmap = new Bitmap(bitmap);
            BodyBounds = bodyBounds;
        }

        public Bitmap Bitmap { get; }

        public Rectangle BodyBounds { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Bitmap.Dispose();
            _disposed = true;
        }
    }
}
