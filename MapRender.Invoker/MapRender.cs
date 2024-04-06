using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WzComparerR2.Animation;
using WzComparerR2.MapRender;
using WzComparerR2.MapRender.Patches;
using WzComparerR2.MapRender.Patches2;
using WzComparerR2.WzLib;

namespace MapRender.Invoker
{
    internal class MapRender : FrmMapRender2
    {
        private volatile Stream _screenShotStream;
        private volatile ScreenShotData _screenShotData;

        public MapRender(Wz_Image img) : base()
        {
            LoadMap(img);
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

        /// <summary>
        /// Take Screen, save image to stream, and return items info on the entire map.
        /// </summary>
        /// <param name="stream">Stream to save image</param>
        public ScreenShotData TakeScreenShot(Stream stream)
        {
            _screenShotStream = stream;
            while (_screenShotStream != null) ; //Wait next Draw(), yield to GetScreenShotMapData()
            var ret = _screenShotData;
            _screenShotData = null;
            return ret;
        }

        /// <summary>
        /// Switch to a new map
        /// </summary>
        /// <param name="imgId">Wz img id</param>
        public void SwitchToNewMap(int imgId)
        {
            MoveToPortal(imgId, null);
            while (!SceneRunning) ; // Wait until new map loaded
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            if (_screenShotStream != null)
            {
                ScreenShotHelper(_screenShotStream, gameTime);
                _screenShotData = new ScreenShotData(new List<TargetItem>(), this.renderEnv.Camera.ClipRect);
                GetScreenShotMapData(mapData.Scene, ref _screenShotData);
                _screenShotStream = null;
            }
        }

        private void ScreenShotHelper(Stream destination, GameTime gameTime)
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            using RenderTarget2D target = new RenderTarget2D(GraphicsDevice, width, height, false,
                SurfaceFormat.Rgba64, DepthFormat.None);
            var oldTarget = GraphicsDevice.GetRenderTargets();
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.Black);
            DrawScene(gameTime);
            DrawTooltipItems(gameTime);
            this.ui.Draw(gameTime.ElapsedGameTime.TotalMilliseconds);
            this.tooltip.Draw(gameTime, renderEnv);
            GraphicsDevice.SetRenderTargets(oldTarget);
            target.SaveAsPng(destination, width, height);

        }

        private void GetScreenShotMapData(SceneNode node, ref ScreenShotData screenShotData)
        {
            var itemsOnMap = screenShotData.Items;
            if (node is ContainerNode container)
            {
                foreach (var item in container.Slots)
                {
                    if (item is LifeItem life)
                    {
                        var rectangle = this.GetLifeBoundingBox(life);
                        if (rectangle.HasValue)
                        {
                            itemsOnMap.Add(new TargetItem(life, rectangle.Value, RenderObjectType.Mob) { Id = life.ID });
                        }
                    }
                }
            }
            else
            {
                for (int i = 0, total = node.Nodes.Count; i < total; ++i)
                {
                    GetScreenShotMapData(node.Nodes[i], ref screenShotData);
                }
            }
        }

    }
}
