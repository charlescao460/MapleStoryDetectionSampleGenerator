using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapRender.Invoker;

namespace MapleStory.Sampler
{
    public class Sample
    {
        public MemoryStream ImageStream { get; private set; }

        public IEnumerable<TargetItem> Items { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public Guid Guid { get; private set; }

        public Sample(MemoryStream imageStream, IEnumerable<TargetItem> items, int width, int height)
        {
            ImageStream = imageStream;
            Items = items;
            Width = width;
            Height = height;
            Guid = Guid.NewGuid();
        }
    }
}
