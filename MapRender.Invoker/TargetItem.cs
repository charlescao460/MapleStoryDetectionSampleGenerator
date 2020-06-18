using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using WzComparerR2.MapRender.Patches;
using WzComparerR2.MapRender.Patches2;

namespace MapRender.Invoker
{
    //Object detection interests item
    public class TargetItem : SceneItem
    {
        public int Id { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public RenderObjectType Type { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public TargetItem() { }

        public TargetItem(SceneItem item)
        {
            Name = item.Name;
            Index = item.Index;
            Tag = item.Tag;
        }

        public TargetItem(SceneItem item, Rectangle rectangle) : this(item)
        {
            X = rectangle.X;
            Y = rectangle.Y;
            Width = rectangle.Width;
            Height = rectangle.Height;
        }

        public TargetItem(SceneItem item, Rectangle rectangle, RenderObjectType type) : this(item, rectangle)
        {
            Type = type;
        }

    }
}
