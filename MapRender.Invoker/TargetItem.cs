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

        public ObjectClass Type { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public TargetItem() { }

        public TargetItem(SceneItem item)
        {
            Name = item.Name;
            Index = item.Index;
            Tags = item.Tags;
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
            Type = RenderTypeToObjectClass(type);
        }

        public static ObjectClass RenderTypeToObjectClass(RenderObjectType type)
        {
            switch (type)
            {
                case RenderObjectType.Mob:
                    return ObjectClass.Mob;
                case RenderObjectType.Foothold:
                    return ObjectClass.Foothold;
                case RenderObjectType.Npc:
                    return ObjectClass.Npc;
                case RenderObjectType.LadderRope:
                    return ObjectClass.LadderRope;
                case RenderObjectType.Portal:
                    return ObjectClass.CrossMapPortal; //TODO: Classify portal
            }
            return ObjectClass.Unknown;
        }

    }
}
