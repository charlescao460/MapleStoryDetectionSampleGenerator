using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MapleStory.Sampler.PostProcessor
{
    public enum RuneArrowDirection
    {
        Down,
        Up,
        Left,
        Right
    }

    public sealed class RuneAssetSet : IDisposable
    {
        private bool _disposed;

        public RuneAssetSet(IEnumerable<RuneArrowAsset> arrows, IEnumerable<Bitmap> noises)
        {
            Arrows = ToRequiredList(arrows, nameof(arrows));
            Noises = (noises ?? Enumerable.Empty<Bitmap>()).ToArray();
        }

        public IReadOnlyList<RuneArrowAsset> Arrows { get; }

        public IReadOnlyList<Bitmap> Noises { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (RuneArrowAsset arrow in Arrows)
            {
                arrow.Dispose();
            }

            foreach (Bitmap noise in Noises)
            {
                noise.Dispose();
            }

            _disposed = true;
        }

        private static IReadOnlyList<RuneArrowAsset> ToRequiredList(IEnumerable<RuneArrowAsset> values, string name)
        {
            if (values == null)
            {
                throw new ArgumentNullException(name);
            }

            RuneArrowAsset[] array = values.ToArray();
            if (array.Length == 0)
            {
                throw new ArgumentException($"{name} cannot be empty.", name);
            }

            return array;
        }
    }

    public sealed class RuneArrowAsset : IDisposable
    {
        private bool _disposed;

        public RuneArrowAsset(
            string name,
            RuneArrowDirection direction,
            Bitmap arrow,
            IEnumerable<Bitmap> bases = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? direction.ToString() : name;
            Direction = direction;
            Arrow = arrow ?? throw new ArgumentNullException(nameof(arrow));
            Bases = (bases ?? Enumerable.Empty<Bitmap>())
                .Where(bitmap => bitmap != null)
                .ToArray();
        }

        public string Name { get; }

        public RuneArrowDirection Direction { get; }

        public Bitmap Arrow { get; }

        public IReadOnlyList<Bitmap> Bases { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Arrow.Dispose();
            foreach (Bitmap bitmap in Bases)
            {
                bitmap.Dispose();
            }

            _disposed = true;
        }
    }
}
