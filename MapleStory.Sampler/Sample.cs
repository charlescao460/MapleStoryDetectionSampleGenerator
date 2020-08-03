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
        public MemoryStream ImageStream { get; internal set; }

        public IList<TargetItem> Items { get; internal set; }

        public int Width { get; internal set; }

        public int Height { get; internal set; }

        public Guid Guid { get; private set; }

        public Sample(MemoryStream imageStream, IEnumerable<TargetItem> items, int width, int height)
        {
            ImageStream = imageStream;
            Items = new List<TargetItem>(items);
            Width = width;
            Height = height;
            Guid = Guid.NewGuid();
        }
    }
}
