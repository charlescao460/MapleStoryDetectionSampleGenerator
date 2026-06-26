using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using WzComparerR2;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace MapleStory.Common
{
    public sealed class WzContext : IDisposable
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<WzContext> Contexts = new List<WzContext>();
        private static readonly AsyncLocal<WzContext> ActiveContext = new AsyncLocal<WzContext>();
        private static EventInfo _findWzEvent;
        private static Delegate _findWzDelegate;

        private bool _disposed;

        public WzContext(string mapleStoryPath, Encoding encoding, bool disableImgCheck = false)
        {
            if (mapleStoryPath == null)
            {
                throw new ArgumentNullException(nameof(mapleStoryPath));
            }

            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            WzStructure = LoadWzStructure(mapleStoryPath, encoding, disableImgCheck);
            Register(this);
        }

        public Wz_Structure WzStructure { get; }

        public void Activate()
        {
            ActiveContext.Value = this;
            lock (SyncRoot)
            {
                if (Contexts.Remove(this))
                {
                    Contexts.Add(this);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Unregister(this);
            if (ActiveContext.Value == this)
            {
                ActiveContext.Value = null;
            }
            WzStructure.Clear();
            _disposed = true;
        }

        private static Wz_Structure LoadWzStructure(string mapleStoryPath, Encoding encoding, bool disableImgCheck)
        {
            string baseWzPath = Path.Combine(mapleStoryPath, MapleStoryPathHelper.MapleStoryBaseWzName);
            if (!File.Exists(baseWzPath))
            {
                throw new ArgumentException(
                    $"Cannot find {MapleStoryPathHelper.MapleStoryBaseWzName} in given directory {mapleStoryPath}.",
                    nameof(mapleStoryPath));
            }

            Wz_Structure wzStructure = new Wz_Structure();
            wzStructure.AutoDetectExtFiles = true;
            wzStructure.TextEncoding = encoding;
            wzStructure.ImgCheckDisabled = disableImgCheck;
            if (string.Equals(Path.GetExtension(baseWzPath), ".ms", StringComparison.OrdinalIgnoreCase))
            {
                wzStructure.LoadMsFile(baseWzPath);
            }
            else if (wzStructure.IsKMST1125WzFormat(baseWzPath))
            {
                wzStructure.LoadKMST1125DataWz(baseWzPath);
                if (string.Equals(Path.GetFileName(baseWzPath), "Base.wz", StringComparison.OrdinalIgnoreCase))
                {
                    string packsDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(baseWzPath)), "Packs");
                    if (Directory.Exists(packsDir))
                    {
                        foreach (string msFile in Directory.GetFiles(packsDir, "*.ms"))
                        {
                            wzStructure.LoadMsFile(msFile);
                        }
                    }
                }
            }
            else
            {
                wzStructure.Load(baseWzPath, true);
            }

            return wzStructure;
        }

        private static void Register(WzContext context)
        {
            lock (SyncRoot)
            {
                EnsureFindWzHandlerRegistered();
                Contexts.Add(context);
            }
        }

        private static void Unregister(WzContext context)
        {
            lock (SyncRoot)
            {
                Contexts.Remove(context);
                if (Contexts.Count == 0)
                {
                    RemoveFindWzHandler();
                }
            }
        }

        private static void EnsureFindWzHandlerRegistered()
        {
            if (_findWzDelegate != null)
            {
                return;
            }

            _findWzEvent = typeof(PluginManager).GetEvent("WzFileFinding", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo findWzHandler = typeof(WzContext).GetMethod(
                nameof(OnWzFileFinding),
                BindingFlags.Static | BindingFlags.NonPublic);
            _findWzDelegate = Delegate.CreateDelegate(_findWzEvent.EventHandlerType, findWzHandler);
            _findWzEvent.AddMethod.Invoke(null, new object[] { _findWzDelegate });
        }

        private static void RemoveFindWzHandler()
        {
            if (_findWzDelegate == null)
            {
                return;
            }

            _findWzEvent.RemoveMethod.Invoke(null, new object[] { _findWzDelegate });
            _findWzEvent = null;
            _findWzDelegate = null;
        }

        private static void OnWzFileFinding(object sender, FindWzEventArgs e)
        {
            WzContext activeContext = ActiveContext.Value;
            if (activeContext != null && activeContext.TryResolve(e, out Wz_Node activeWzNode, out Wz_File activeWzFile))
            {
                e.WzNode = activeWzNode;
                e.WzFile = activeWzFile;
                return;
            }

            WzContext[] contexts;
            lock (SyncRoot)
            {
                contexts = Contexts.ToArray();
            }

            for (int i = contexts.Length - 1; i >= 0; i--)
            {
                if (contexts[i] == activeContext)
                {
                    continue;
                }

                if (contexts[i].TryResolve(e, out Wz_Node wzNode, out Wz_File wzFile))
                {
                    e.WzNode = wzNode;
                    e.WzFile = wzFile;
                    return;
                }
            }
        }

        private bool TryResolve(FindWzEventArgs e, out Wz_Node wzNode, out Wz_File wzFile)
        {
            wzNode = null;
            wzFile = null;

            string[] fullPath = null;
            Wz_Type wzType = e.WzType;
            if (!string.IsNullOrEmpty(e.FullPath))
            {
                fullPath = e.FullPath.Split('/', '\\');
                try
                {
                    wzType = (Wz_Type)Enum.Parse(typeof(Wz_Type), fullPath[0], true);
                }
                catch
                {
                    wzType = Wz_Type.Unknown;
                }
            }

            List<Wz_Node> preSearch = new List<Wz_Node>();
            if (wzType != Wz_Type.Unknown)
            {
                IEnumerable<Wz_Structure> preSearchWz = e.WzFile?.WzStructure != null
                    ? Enumerable.Repeat(e.WzFile.WzStructure, 1)
                    : new List<Wz_Structure>() { WzStructure };
                foreach (Wz_Structure wzStructure in preSearchWz)
                {
                    Wz_File baseWz = null;
                    bool foundTypedFile = false;
                    foreach (Wz_File currentWzFile in wzStructure.wz_files)
                    {
                        if (currentWzFile.Type == wzType)
                        {
                            preSearch.Add(currentWzFile.Node);
                            foundTypedFile = true;
                        }

                        if (currentWzFile.Type == Wz_Type.Base)
                        {
                            baseWz = currentWzFile;
                        }
                    }

                    if (baseWz != null && !foundTypedFile)
                    {
                        string key = wzType.ToString();
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
                if (wzType != Wz_Type.Unknown && preSearch.Count > 0)
                {
                    wzNode = preSearch[0];
                    wzFile = preSearch[0].Value as Wz_File;
                    return true;
                }

                return false;
            }

            if (preSearch.Count <= 0)
            {
                return false;
            }

            foreach (Wz_Node wzFileNode in preSearch)
            {
                Wz_Node searchNode = wzFileNode;
                for (int i = 1; i < fullPath.Length && searchNode != null; i++)
                {
                    string pathSegment = fullPath[i];
                    if (string.IsNullOrEmpty(pathSegment))
                    {
                        searchNode = null;
                        break;
                    }

                    searchNode = searchNode.Nodes[pathSegment];
                    if (searchNode == null)
                    {
                        break;
                    }

                    Wz_Image img = searchNode.GetValueEx<Wz_Image>(null);
                    if (img != null)
                    {
                        searchNode = img.TryExtract() ? img.Node : null;
                    }
                }

                if (searchNode != null)
                {
                    wzNode = searchNode;
                    wzFile = wzFileNode.Value as Wz_File;
                    return true;
                }
            }

            return false;
        }
    }
}
