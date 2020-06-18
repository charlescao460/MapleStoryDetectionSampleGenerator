using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MapleStory.Common;
using Microsoft.Xna.Framework;
using Un4seen.Bass;
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

        public bool IsRunning { get; private set; }

        public int ScreenWidth { private set; get; }

        public int ScreenHeight { private set; get; }

        public int WorldWidth => _camera.WorldRect.Width;

        public int WorldHeight => _camera.WorldRect.Height;

        public int CurrentCameraX => (int)_camera.Center.X;

        public int CurrentCameraY => (int) _camera.Center.Y;

        public MapRenderInvoker(string mapleStoryPath, Encoding encoding, bool disableImgCheck = false)
            : base(mapleStoryPath, encoding, disableImgCheck)
        {
            AddFindWzEventHandler();
        }

        /// <summary>
        /// Attach event handler to PlugManager, let it throw if reflection fail so we know there are changes in WzComparerR2
        /// </summary>
        /// <seealso cref="WzComparerR2.PluginBase.PluginManager.WzFileFinding"/>
        private void AddFindWzEventHandler()
        {
            EventInfo findWzEvent = typeof(PluginManager)
                .GetEvent("WzFileFinding", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo findWzHandler =
                typeof(MapRenderInvoker).GetMethod("CharaSimLoader_WzFileFinding", BindingFlags.NonPublic | BindingFlags.Instance);
            Delegate findWzDelegate = Delegate.CreateDelegate(findWzEvent.EventHandlerType, this, findWzHandler);
            findWzEvent.AddMethod.Invoke(this, new[] { findWzDelegate });
        }

        ~MapRenderInvoker()
        {
            _renderThread?.Abort();
        }

        ///<inheritdoc/>
        public override void LoadMap(string imgText)
        {
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
            if (_currentMapImage == null)
            {
                throw new InvalidOperationException("MapRenderInvoker.LoadMap() must be called before Launch().");
            }

            IsRunning = false;
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
                        IsRunning = true;
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

            while (!IsRunning) ; // Wait until ready
        }

        public void MoveCamera(int centerX, int centerY)
        {
            _camera.Center = new Vector2(centerX,centerY);
            _camera.AdjustToWorldRect();
        }

        public List<TargetItem> TakeScreenShot(Stream stream)
        {
            return _mapRender.TakeScreenShot(stream);
        }

        #region COPIED_CODE

        /// <summary>
        /// !!!!!!!!!!!!COPIED CODE!!!!!!!!!!!!!!
        /// Version: git@github.com:Kagamia/WzComparerR2.git:f6ecfb18cae661f125a189e527feea1964f5bda8
        /// </summary>
        /// <see cref="WzComparerR2.MainForm.CharaSimLoader_WzFileFinding"/>
        private void CharaSimLoader_WzFileFinding(object sender, WzComparerR2.FindWzEventArgs e)
        {
            string[] fullPath = null;
            if (!string.IsNullOrEmpty(e.FullPath)) //用fullpath作为输入参数
            {
                fullPath = e.FullPath.Split('/', '\\');
                try
                {
                    e.WzType = (Wz_Type)Enum.Parse(typeof(Wz_Type), fullPath[0], true);
                }
                catch
                {
                    e.WzType = Wz_Type.Unknown;
                }
            }

            List<Wz_Node> preSearch = new List<Wz_Node>();
            if (e.WzType != Wz_Type.Unknown) //用wztype作为输入参数
            {
                IEnumerable<Wz_Structure> preSearchWz = e.WzFile?.WzStructure != null ?
                    Enumerable.Repeat(e.WzFile.WzStructure, 1) : new List<Wz_Structure>() { _wzStructure };
                foreach (var wzs in preSearchWz)
                {
                    Wz_File baseWz = null;
                    bool find = false;
                    foreach (Wz_File wz_f in wzs.wz_files)
                    {
                        if (wz_f.Type == e.WzType)
                        {
                            preSearch.Add(wz_f.Node);
                            find = true;
                            //e.WzFile = wz_f;
                        }
                        if (wz_f.Type == Wz_Type.Base)
                        {
                            baseWz = wz_f;
                        }
                    }

                    // detect data.wz
                    if (baseWz != null && !find)
                    {
                        string key = e.WzType.ToString();
                        foreach (Wz_Node node in baseWz.Node.Nodes)
                        {
                            if (node.Text == key && node.Nodes.Count > 0)
                            {
                                preSearch.Add(node);
                            }
                        }
                    }
                }
            }

            if (fullPath == null || fullPath.Length <= 1)
            {
                if (e.WzType != Wz_Type.Unknown && preSearch.Count > 0) //返回wzFile
                {
                    e.WzNode = preSearch[0];
                    e.WzFile = preSearch[0].Value as Wz_File;
                }
                return;
            }

            if (preSearch.Count <= 0)
            {
                return;
            }

            foreach (var wzFileNode in preSearch)
            {
                var searchNode = wzFileNode;
                for (int i = 1; i < fullPath.Length && searchNode != null; i++)
                {
                    searchNode = searchNode.Nodes[fullPath[i]];
                    var img = searchNode.GetValueEx<Wz_Image>(null);
                    if (img != null)
                    {
                        searchNode = img.TryExtract() ? img.Node : null;
                    }
                }

                if (searchNode != null)
                {
                    e.WzNode = searchNode;
                    e.WzFile = wzFileNode.Value as Wz_File;
                    return;
                }
            }
            //寻找失败
            e.WzNode = null;
        }
        #endregion
    }

    public abstract class MapRenderInvokerBase
    {
        static MapRenderInvokerBase()
        {
            // We must initialize bass.dll here
            Console.WriteLine(@"###########Copyright info from bass.dll###########");
            Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, System.IntPtr.Zero);
        }

        protected readonly Wz_Structure _wzStructure;

        protected MapRenderInvokerBase(string mapleStoryPath, Encoding encoding, bool disableImgCheck = false)
        {
            // Static settings for Wz_Structure :(
            Wz_Structure.DefaultAutoDetectExtFiles = true;
            Wz_Structure.DefaultEncoding = encoding;
            Wz_Structure.DefaultImgCheckDisabled = disableImgCheck;
            // Then our constructor
            string baseWzPath = Path.Combine(mapleStoryPath, MapleStoryPathHelper.MapleStoryBaseWzName);
            if (!File.Exists(baseWzPath))
            {
                throw new ArgumentException($"Cannot find {MapleStoryPathHelper.MapleStoryBaseWzName} in given directory {mapleStoryPath}.");
            }
            _wzStructure = new Wz_Structure();
            _wzStructure.Load(baseWzPath);
        }

        /// <summary>
        /// Load specified Wz map img to this invoker.
        /// </summary>
        /// <param name="imgText">The map node text to search. E.g. "450007010.img" </param>
        /// <exception cref="MapleStory.Common.Exceptions.WzImgNotFoundException">If supplied img cannot be found.</exception>
        public abstract void LoadMap(string imgText);

        /// <summary>
        /// Lunch map render. Make sure we have loaded img.
        /// </summary>
        /// <param name="width">Width of resolution</param>
        /// <param name="height">Height of resolution</param>
        public abstract void Launch(int width, int height);

    }
}
