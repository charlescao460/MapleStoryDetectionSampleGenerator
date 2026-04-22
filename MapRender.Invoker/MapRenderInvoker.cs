using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private Wz_Image _currentMapImage;
        private StringLinker _stringLinker;
        private Thread _renderThread;
        private MapRender _mapRender;
        private Camera _camera;
        private volatile bool _isRunning;

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
            _renderThread?.Abort();
        }

        ///<inheritdoc/>
        public override void LoadMap(string imgText)
        {
            ActivateWzContext();
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
            if (_currentMapImage == null)
            {
                throw new InvalidOperationException("MapRenderInvoker.LoadMap() must be called before Launch().");
            }

            _isRunning = false;
            _renderThread = new Thread(() =>
            {
                _mapRender = new MapRender(_currentMapImage) { StringLinker = _stringLinker };
                _mapRender.Window.Title = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileName;
                try
                {
                    using (_mapRender)
                    {
                        _mapRender.RunOneFrame(); // Initialize
                        _mapRender.ChangeResolution(width, height);
                        _camera = _mapRender.renderEnv.Camera;
                        _mapRender.Run();
                    }
                }
                finally
                {
                    _mapRender = null;
                }
            });
            ScreenHeight = height;
            ScreenWidth = width;
            _renderThread.SetApartmentState(ApartmentState.STA);
            _renderThread.IsBackground = true;
            _renderThread.Start();
            SpinWait spinWait = new SpinWait();
            while (_mapRender == null)
            {
                spinWait.SpinOnce();
            }
            _mapRender.WaitSceneLoading();
            _isRunning = true;
        }

        public override void SwitchMap(string imgText)
        {
            ActivateWzContext();
            CurrentMap = int.Parse(imgText);
            _mapRender.SwitchToNewMap(CurrentMap);
        }

        public void MoveCamera(int centerX, int centerY)
        {
            _camera.Center = new Vector2(centerX, centerY);
            _camera.AdjustToWorldRect();
        }

        public ScreenShotData TakeScreenShot(Stream stream)
        {
            return _mapRender.TakeScreenShot(stream);
        }

    }

    public abstract class MapRenderInvokerBase
    {
        private readonly WzContext _wzContext;
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
