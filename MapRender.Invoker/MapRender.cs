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
        private Stream _screenShotStream;
        private List<TargetItem> _itemsOnMap;

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

        public List<TargetItem> TakeScreenShot(Stream file)
        {
            _screenShotStream = file;
            while (_screenShotStream != null) ; //Wait next Draw()
            var ret = _itemsOnMap;
            _itemsOnMap = null;
            return ret;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_screenShotStream != null)
            {
                ScreenShotHelper(_screenShotStream, gameTime);
                _itemsOnMap = new List<TargetItem>();
                GetScreenShotMapData(mapData.Scene, ref _itemsOnMap);
                _screenShotStream = null;
            }
            base.Draw(gameTime);
        }

        private void ScreenShotHelper(Stream destination, GameTime gameTime)
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
            target.SaveAsPng(destination, width, height);
        }

        private void GetScreenShotMapData(SceneNode node, ref List<TargetItem> itemsOnMap)
        {
            if (node is ContainerNode container)
            {
                foreach (var item in container.Slots)
                {
                    if (item is LifeItem life)
                    {
                        if (life.View.Animator is StateMachineAnimator animator)
                        {
                            var data = (StateMachineAnimator.FrameStateMachineData)animator.Data;
                            IDictionary<string, RepeatableFrameAnimationData> dict = data.Data;
                            Rectangle rectangle = data.FrameAnimator.CurrentFrame.Rectangle;
                            itemsOnMap.Add(new TargetItem(life, rectangle, RenderObjectType.Mob) { Id = life.ID });
                        }
                    }
                }
            }
            else
            {
                for (int i = 0, total = node.Nodes.Count; i < total; ++i)
                {
                    GetScreenShotMapData(node.Nodes[i], ref itemsOnMap);
                }
            }
        }

    }
}
