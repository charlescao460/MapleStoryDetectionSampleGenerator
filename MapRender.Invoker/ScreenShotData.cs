using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapRender.Invoker
{
    public class ScreenShotData
    {
        public List<TargetItem> Items { get; private set; }

        public Rectangle CameraRectangle { get; private set; }

        public ScreenShotData(List<TargetItem> items, Microsoft.Xna.Framework.Rectangle camRectangle)
        {
            Items = items;
            CameraRectangle = new Rectangle(camRectangle.X, camRectangle.Y, camRectangle.Width, camRectangle.Height);
        }
    }
}
