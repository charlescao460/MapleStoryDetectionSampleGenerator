using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using MapleStory.Common;
using Microsoft.Xna.Framework;
using WzComparerR2.Common;
using WzComparerR2.MapRender;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace MapRender.Invoker
{
    public class MapRenderInvoker : MapRenderInvokerBase
    {
        private static readonly object RenderInitializationSyncRoot = new object();
        private static readonly TimeSpan SceneLoadingTimeout = TimeSpan.FromSeconds(60);
        private readonly object _lifetimeSync = new object();
        private Wz_Image _currentMapImage;
        private StringLinker _stringLinker;
        private Thread _renderThread;
        private MapRender _mapRender;
        private Camera _camera;
        private Exception _renderThreadException;
        private volatile bool _isRunning;
        private bool _disposed;

        public bool IsRunning => _isRunning;

        public int ScreenWidth { private set; get; }

        public int ScreenHeight { private set; get; }

        public int WorldWidth => _camera.WorldRect.Width;

        public int WorldHeight => _camera.WorldRect.Height;

        public int WorldOriginX => _camera.WorldRect.X;

        public int WorldOriginY => _camera.WorldRect.Y;

        public int CurrentCameraX => (int)_camera.Center.X;

        public int CurrentCameraY => (int)_camera.Center.Y;

        public int CurrentMap { get; private set; }

        public MapRenderInvoker(string mapleStoryPath, Encoding encoding, bool disableImgCheck = false)
            : base(mapleStoryPath, encoding, disableImgCheck)
        {
            _isRunning = false;
        }

        ~MapRenderInvoker()
        {
            Dispose(false);
        }

        ///<inheritdoc/>
        public override void LoadMap(string imgText)
        {
            ActivateWzContext();
            ThrowIfDisposed();
            CurrentMap = int.Parse(imgText);
            imgText = imgText.EndsWith(".img") ? imgText : (imgText + ".img");
            _currentMapImage = WzTreeSearcher.SearchForMap(_wzStructure.WzNode, imgText);
            Exception ex;
            _currentMapImage.TryExtract(out ex);
            if (ex != null)
            {
                throw ex;
            }
            _stringLinker = new StringLinker();
            _stringLinker.Load(PluginManager.FindWz(Wz_Type.String).GetValueEx<Wz_File>(null));
        }

        /// <inheritdoc/>
        public override void Launch(int width, int height)
        {
            ActivateWzContext();
            ThrowIfDisposed();
            if (_currentMapImage == null)
            {
                throw new InvalidOperationException("MapRenderInvoker.LoadMap() must be called before Launch().");
            }

            _isRunning = false;
            _renderThreadException = null;
            ManualResetEventSlim initialized = new ManualResetEventSlim(false);
            _renderThread = new Thread(() =>
            {
                try
                {
                    ActivateWzContext();
                    MapRender mapRender = new MapRender(_currentMapImage) { StringLinker = _stringLinker };
                    mapRender.Window.Title = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileName;
                    lock (_lifetimeSync)
                    {
                        _mapRender = mapRender;
                    }
                    using (mapRender)
                    {
                        mapRender.RunOneFrame(); // Initialize
                        mapRender.ChangeResolution(width, height);
                        initialized.Set();
                        mapRender.Run();
                    }
                }
                catch (Exception ex)
                {
                    _renderThreadException = ex;
                    initialized.Set();
                }
                finally
                {
                    lock (_lifetimeSync)
                    {
                        _mapRender = null;
                        _camera = null;
                    }
                    _isRunning = false;
                }
            });
            ScreenHeight = height;
            ScreenWidth = width;
            _renderThread.SetApartmentState(ApartmentState.STA);
            _renderThread.IsBackground = true;
            lock (RenderInitializationSyncRoot)
            {
                _renderThread.Start();
                initialized.Wait();
                initialized.Dispose();
                if (_renderThreadException != null)
                {
                    throw new InvalidOperationException("MapRender launch failed.", _renderThreadException);
                }
                MapRender mapRender;
                lock (_lifetimeSync)
                {
                    mapRender = _mapRender;
                }
                if (mapRender == null)
                {
                    throw new InvalidOperationException("MapRender launch failed before initialization completed.");
                }
                mapRender.WaitSceneLoading(SceneLoadingTimeout);
                _camera = mapRender.renderEnv.Camera;
                _isRunning = true;
            }
        }

        public override void SwitchMap(string imgText)
        {
            ActivateWzContext();
            ThrowIfDisposed();
            ThrowIfRenderThreadFailed();
            int mapId = int.Parse(imgText);
            if (CurrentMap == mapId)
            {
                return;
            }

            string mapImgText = imgText.EndsWith(".img") ? imgText : (imgText + ".img");
            Wz_Image nextMapImage = WzTreeSearcher.SearchForMap(_wzStructure.WzNode, mapImgText);
            Exception ex;
            nextMapImage.TryExtract(out ex);
            if (ex != null)
            {
                throw ex;
            }

            MapRender mapRender;
            lock (_lifetimeSync)
            {
                mapRender = _mapRender;
            }
            if (mapRender == null)
            {
                throw new InvalidOperationException("MapRender is not available.");
            }

            mapRender.SwitchToNewMap(nextMapImage, mapId, SceneLoadingTimeout);
            _camera = mapRender.renderEnv.Camera;
            _currentMapImage = nextMapImage;
            CurrentMap = mapId;
            ThrowIfRenderThreadFailed();
        }

        public void MoveCamera(int centerX, int centerY)
        {
            ThrowIfDisposed();
            ThrowIfRenderThreadFailed();
            _camera.Center = new Vector2(centerX, centerY);
            _camera.AdjustToWorldRect();
        }

        public ScreenShotData TakeScreenShot(Stream stream)
        {
            ThrowIfDisposed();
            ThrowIfRenderThreadFailed();
            MapRender mapRender;
            lock (_lifetimeSync)
            {
                mapRender = _mapRender;
            }
            if (mapRender == null)
            {
                throw new InvalidOperationException("MapRender is not available.");
            }
            ScreenShotData screenShotData = mapRender.TakeScreenShot(stream);
            ThrowIfRenderThreadFailed();
            return screenShotData;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            MapRender mapRender;
            Thread renderThread;
            lock (_lifetimeSync)
            {
                mapRender = _mapRender;
                renderThread = _renderThread;
            }

            try
            {
                lock (RenderInitializationSyncRoot)
                {
                    mapRender?.Exit();
                    if (disposing && renderThread != null && renderThread.IsAlive)
                    {
                        renderThread.Join(TimeSpan.FromSeconds(10));
                    }
                }
            }
            finally
            {
                if (disposing)
                {
                    base.Dispose(disposing);
                }
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MapRenderInvoker));
            }
        }

        private void ThrowIfRenderThreadFailed()
        {
            if (_renderThreadException != null)
            {
                throw new InvalidOperationException("MapRender render thread failed.", _renderThreadException);
            }
            if (!_isRunning)
            {
                throw new InvalidOperationException("MapRender is not running.");
            }
        }

    }

    public abstract class MapRenderInvokerBase : IDisposable
    {
        private readonly WzContext _wzContext;
        private bool _disposed;
        protected readonly Wz_Structure _wzStructure;

        /// <summary>
        /// Provide base methods to open MapleStory data directory.
        /// For maintenance with upstream WzR2, check <see cref="WzComparerR2.MainForm.openWz"/>
        /// </summary>
        /// <param name="mapleStoryPath"></param>
        /// <param name="encoding"></param>
        /// <param name="disableImgCheck"></param>
        /// <exception cref="ArgumentException"></exception>
        protected MapRenderInvokerBase(string mapleStoryPath, Encoding encoding, bool disableImgCheck = false)
        {
            _wzContext = new WzContext(mapleStoryPath, encoding, disableImgCheck);
            _wzStructure = _wzContext.WzStructure;
        }

        protected void ActivateWzContext()
        {
            _wzContext.Activate();
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _wzContext.Dispose();
            }
            _disposed = true;
        }

        /// <summary>
        /// Load specified Wz map img to this invoker.
        /// </summary>
        /// <param name="imgText">The map node search. E.g. "450007010" </param>
        /// <exception cref="MapleStory.Common.Exceptions.WzImgNotFoundException">If supplied img cannot be found.</exception>
        public abstract void LoadMap(string imgText);

        /// <summary>
        /// Lunch map render. Make sure we have loaded img.
        /// </summary>
        /// <param name="width">Width of resolution</param>
        /// <param name="height">Height of resolution</param>
        public abstract void Launch(int width, int height);

        /// <summary>
        /// Switch to a new map after <see cref="Launch"/>
        /// </summary>
        /// <param name="imgText">The map node search. E.g. "450007010" </param>
        public abstract void SwitchMap(string imgText);

    }
}
