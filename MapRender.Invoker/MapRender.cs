using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using WzComparerR2.MapRender;
using WzComparerR2.WzLib;

namespace MapRender.Invoker
{
    internal class MapRender: FrmMapRender2
    {
        public MapRender(Wz_Image img) : base(img)
        {
        }

        public void ChangeResolution(int width, int height)
        {
            GraphicsDeviceManager deviceManager = this.GraphicsManager;
            deviceManager.PreferredBackBufferWidth = width;
            deviceManager.PreferredBackBufferHeight = height;
            WzComparerR2.Rendering.D2DFactory.Instance.ReleaseContext(deviceManager.GraphicsDevice);
            this.ui.Width = width;
            this.ui.Height = height;
            deviceManager.ApplyChanges();
        }


    }
}
