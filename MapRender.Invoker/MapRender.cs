using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        private static readonly TimeSpan ScreenShotTimeout = TimeSpan.FromSeconds(30);
        private readonly object _screenShotSync = new object();
        private ScreenShotRequest _screenShotRequest;

        static MapRender()
        {
            UseNullInputDevice = true;
            UseHeadlessMode = true;
        }

        public MapRender(Wz_Image img) : base()
        {
            LoadMap(img);
        }

        public bool IsSceneRunning => SceneRunning;

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
            ScreenShotRequest request = new ScreenShotRequest(stream);
            lock (_screenShotSync)
            {
                if (_screenShotRequest != null)
                {
                    throw new InvalidOperationException("A screenshot request is already pending.");
                }
                _screenShotRequest = request;
            }

            if (!request.Completion.Task.Wait(ScreenShotTimeout))
            {
                lock (_screenShotSync)
                {
                    if (ReferenceEquals(_screenShotRequest, request))
                    {
                        _screenShotRequest = null;
                    }
                }
                request.Completion.TrySetCanceled();
                throw new TimeoutException($"MapRender screenshot did not complete within {ScreenShotTimeout.TotalSeconds} seconds.");
            }

            return request.Completion.Task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Switch to a new map
        /// </summary>
        /// <param name="imgId">Wz img id</param>
        public void SwitchToNewMap(int imgId)
        {
            MoveToPortal(imgId, null);
            WaitSceneLoading();
        }

        public void WaitSceneLoading(TimeSpan? timeout = null)
        {
            DateTime start = DateTime.UtcNow;
            SpinWait spinWait = new SpinWait();
            // Wait until new map loaded
            while (!SceneRunning)
            {
                if (timeout.HasValue && DateTime.UtcNow - start > timeout.Value)
                {
                    throw new TimeoutException($"Scene loading did not complete within {timeout.Value.TotalSeconds} seconds.");
                }
                spinWait.SpinOnce();
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            if (mapData != null)
            {
                DrawScene(gameTime);
            }

            ScreenShotRequest request;
            lock (_screenShotSync)
            {
                request = _screenShotRequest;
            }

            if (request != null)
            {
                try
                {
                    ScreenShotHelper(request.Stream, gameTime);
                    ScreenShotData screenShotData = new ScreenShotData(new List<TargetItem>(), this.renderEnv.Camera.ClipRect);
                    GetScreenShotMapData(mapData.Scene, ref screenShotData);
                    request.Completion.TrySetResult(screenShotData);
                }
                catch (Exception ex)
                {
                    request.Completion.TrySetException(ex);
                }
                finally
                {
                    lock (_screenShotSync)
                    {
                        if (ReferenceEquals(_screenShotRequest, request))
                        {
                            _screenShotRequest = null;
                        }
                    }
                }
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

        private sealed class ScreenShotRequest
        {
            public ScreenShotRequest(Stream stream)
            {
                Stream = stream;
            }

            public Stream Stream { get; }

            public TaskCompletionSource<ScreenShotData> Completion { get; } =
                new TaskCompletionSource<ScreenShotData>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

    }
}
