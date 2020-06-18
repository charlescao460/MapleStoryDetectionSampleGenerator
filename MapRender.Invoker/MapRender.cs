using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WzComparerR2.MapRender;
using WzComparerR2.WzLib;

namespace MapRender.Invoker
{
    internal class MapRender : FrmMapRender2
    {
        private Stream _screenShotStream;

        public MapRender(Wz_Image img) : base(img)
        {
        }

        public void ChangeResolution(int width, int height)
        {
            GraphicsDeviceManager deviceManager = this.GraphicsManager;
            deviceManager.PreferredBackBufferWidth = width;
            deviceManager.PreferredBackBufferHeight = height;
            WzComparerR2.Rendering.D2DFactory.Instance.ReleaseContext(deviceManager.GraphicsDevice);
            deviceManager.ApplyChanges();
            this.ui.Width = width;
            this.ui.Height = height;
            engine.Renderer.ResetNativeSize();
        }

        public void TakeScreenShot(Stream file)
        {
            _screenShotStream = file;
            while (_screenShotStream != null) ;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_screenShotStream != null)
            {
                int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
                int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
                RenderTarget2D target = new RenderTarget2D(GraphicsDevice, width, height, false, 
                    SurfaceFormat.Rgba64, DepthFormat.None);
                var oldTarget = GraphicsDevice.GetRenderTargets();
                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(Color.Black);
                DrawScene(gameTime);
                DrawTooltipItems(gameTime);
                this.ui.Draw(gameTime.ElapsedGameTime.TotalMilliseconds);
                this.tooltip.Draw(gameTime, renderEnv);
                GraphicsDevice.SetRenderTargets(oldTarget);
                target.SaveAsPng(_screenShotStream, width, height);// intentionally block
                _screenShotStream = null;
            }
            base.Draw(gameTime);
        }

    }
}
