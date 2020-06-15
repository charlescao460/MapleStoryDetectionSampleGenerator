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
using WzComparerR2.Common;
using WzComparerR2.MapRender;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace MapRender.Invoker
{
    public class MapRenderInvoker
    {
        private readonly Wz_Structure _wzStructure;
        private Wz_Image _currentMapImage;
        private StringLinker _stringLinker;
        private Thread _renderThread;
        private FrmMapRender2 _mapRender;

        public MapRenderInvoker(string mapleStoryPath, Encoding encoding, bool disableImgCheck = false)
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

        ~MapRenderInvoker()
        {
            _renderThread?.Abort();
        }

        /// <summary>
        /// Load specified Wz map img to this invoker.
        /// </summary>
        /// <param name="imgText">The map node text to search. E.g. "450007010.img" </param>
        /// <exception cref="MapleStory.Common.Exceptions.WzImgNotFoundException">If supplied img cannot be found.</exception>
        public void LoadMap(string imgText)
        {
            Wz_File stringWzFile;
            _currentMapImage = WzTreeSearcher.SearchForMap(_wzStructure.WzNode, imgText, out stringWzFile);
            Exception ex;
            _currentMapImage.TryExtract(out ex);
            if (ex != null)
            {
                throw ex;
            }
            _stringLinker = new StringLinker();
            _stringLinker.Load(stringWzFile);
        }

        /// <summary>
        /// Lunch map render. Make sure we have loaded img.
        /// </summary>
        public void Launch()
        {
            if (_currentMapImage == null)
            {
                throw new InvalidOperationException("MapRenderInvoker.LoadMap() must be called before Launch().");
            }
            _renderThread = new Thread(() =>
            {
                _mapRender = new FrmMapRender2(_currentMapImage) { StringLinker = _stringLinker };
                _mapRender.Window.Title = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileName;
                try
                {
                    using (_mapRender)
                    {
                        _mapRender.Run();
                    }
                }
                finally
                {
                    _mapRender = null;
                }
            });
            _renderThread.SetApartmentState(ApartmentState.STA);
            _renderThread.IsBackground = true;
            _renderThread.Start();
            while (true)
            {
                // Block
            }
        }

    }
}
