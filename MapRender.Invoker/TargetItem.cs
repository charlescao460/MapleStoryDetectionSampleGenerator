using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WzComparerR2.MapRender.Patches;
using WzComparerR2.MapRender.Patches2;

namespace MapRender.Invoker
{
    //Object detection interests item
    public class TargetItem : SceneItem
    {
        public int X { get; set; }

        public int Y { get; set; }
        
        public RenderObjectType Type { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }
}
