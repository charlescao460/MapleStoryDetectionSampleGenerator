using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapleStory.Common;
using WzComparerR2.Common;
using WzComparerR2.PluginBase;
using WzComparerR2.WzLib;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal sealed class MapGeometryExporter
    {
        private const int SchemaVersion = 2;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly string _mapleStoryPath;
        private readonly Encoding _encoding;

        public MapGeometryExporter(string mapleStoryPath, Encoding encoding)
        {
            _mapleStoryPath = mapleStoryPath ?? throw new ArgumentNullException(nameof(mapleStoryPath));
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public void ExportMaps(IEnumerable<MapExportRequest> maps, string outputDirectory)
        {
            if (maps == null)
            {
                throw new ArgumentNullException(nameof(maps));
            }
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
            }

            List<NormalizedMapExportRequest> normalizedMaps = maps
                .Select(request => new NormalizedMapExportRequest(
                    ParseMapId(request.MapId),
                    request.SkipUnsupported))
                .GroupBy(request => request.MapId)
                .Select(group => new NormalizedMapExportRequest(
                    group.Key,
                    group.All(request => request.SkipUnsupported)))
                .OrderBy(request => request.MapId)
                .ToList();
            if (normalizedMaps.Count == 0)
            {
                throw new InvalidOperationException("At least one map id is required.");
            }

            string fullOutputPath = Path.GetFullPath(outputDirectory);
            string outputParent = Path.GetDirectoryName(fullOutputPath);
            string stagingDirectory = Path.Combine(
                outputParent,
                ".hecate-map-pack-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);

            using WzContext context = new WzContext(_mapleStoryPath, _encoding, false);
            context.Activate();
            try
            {
                IReadOnlyDictionary<int, string> mapNames = LoadMapNames(context);
                IReadOnlyDictionary<int, Wz_Image> mapIndex = BuildMapIndex(context.WzStructure.WzNode);
                foreach (NormalizedMapExportRequest map in normalizedMaps)
                {
                    try
                    {
                        ExportMap(mapIndex, map.MapId, mapNames, stagingDirectory);
                    }
                    catch (UnsupportedMapGeometryException ex) when (map.SkipUnsupported)
                    {
                        Console.Error.WriteLine(
                            $"Skipped map {map.MapId.ToString(CultureInfo.InvariantCulture)}: {ex.Reason}.");
                    }
                }

                IReadOnlyList<int> sourceVersions = context.WzStructure.wz_files
                    .Select(file => file.Header.WzVersion)
                    .Where(version => version > 0)
                    .Distinct()
                    .OrderBy(version => version)
                    .ToList();
                string producerVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
                MapPackPublisher.Publish(stagingDirectory, fullOutputPath, sourceVersions, producerVersion);
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, true);
                }
            }
        }

        private static void ExportMap(
            IReadOnlyDictionary<int, Wz_Image> mapIndex,
            int mapId,
            IReadOnlyDictionary<int, string> mapNames,
            string outputDirectory)
        {
            string rawMapId = mapId.ToString(CultureInfo.InvariantCulture);
            if (!mapIndex.TryGetValue(mapId, out Wz_Image image))
            {
                throw new InvalidOperationException($"Map {rawMapId} was not found in the loaded WZ files.");
            }

            HashSet<Wz_Image> extractedImages = new HashSet<Wz_Image>(ReferenceEqualityComparer.Instance)
            {
                image,
            };
            try
            {
                Exception extractError;
                image.TryExtract(out extractError);
                if (extractError != null)
                {
                    throw extractError;
                }

                ResolvedMap resolvedMap = ResolveLinkedMap(mapIndex, image.Node, mapId, extractedImages);
                Wz_Node miniMapNode = RequireSupportedMinimap(resolvedMap.Node, mapId);

                string imageName = rawMapId + ".png";
                SaveMinimapImage(
                    miniMapNode,
                    Path.Combine(outputDirectory, imageName),
                    mapId,
                    extractedImages);
                MapGeometryPayload payload = new MapGeometryPayload
                {
                    SchemaVersion = SchemaVersion,
                    MapId = mapId,
                    MapName = mapNames.TryGetValue(mapId, out string mapName) && !string.IsNullOrWhiteSpace(mapName)
                        ? mapName
                        : rawMapId,
                    Minimap = ReadMinimap(miniMapNode, imageName),
                    Platforms = ReadPlatforms(resolvedMap.Node.Nodes["foothold"]),
                    Ropes = ReadRopes(resolvedMap.Node.Nodes["ladderRope"]),
                    Portals = ReadPortals(resolvedMap.Node.Nodes["portal"], resolvedMap.MapId),
                };

                string json = JsonSerializer.Serialize(payload, JsonOptions) + "\n";
                File.WriteAllText(Path.Combine(outputDirectory, rawMapId + ".json"), json, new UTF8Encoding(false));
                Console.WriteLine(
                    $"Exported {rawMapId}: {payload.Platforms.Count} platforms, {payload.Ropes.Count} ropes/ladders, {payload.Portals.Count} portals");
            }
            finally
            {
                foreach (Wz_Image extractedImage in extractedImages)
                {
                    extractedImage.Unextract();
                }
            }
        }

        private static ResolvedMap ResolveLinkedMap(
            IReadOnlyDictionary<int, Wz_Image> mapIndex,
            Wz_Node mapNode,
            int mapId,
            ISet<Wz_Image> extractedImages)
        {
            int? link = mapNode.Nodes["info"]?.Nodes["link"].GetValueEx<int>();
            if (!link.HasValue)
            {
                return new ResolvedMap(mapNode, mapId);
            }

            if (!mapIndex.TryGetValue(link.Value, out Wz_Image linkedImage))
            {
                throw new InvalidOperationException($"Linked map {link.Value} was not found in the loaded WZ files.");
            }

            extractedImages.Add(linkedImage);
            Exception extractError;
            linkedImage.TryExtract(out extractError);
            if (extractError != null)
            {
                throw extractError;
            }
            return new ResolvedMap(linkedImage.Node, link.Value);
        }

        internal static Wz_Node RequireSupportedMinimap(Wz_Node mapNode, int mapId)
        {
            Wz_Node miniMapNode = mapNode?.Nodes["miniMap"];
            if (miniMapNode == null)
            {
                throw new UnsupportedMapGeometryException(mapId, "has no miniMap node");
            }

            if (miniMapNode.Nodes["canvas"] == null)
            {
                throw new UnsupportedMapGeometryException(mapId, "has no minimap canvas image");
            }

            List<string> additionalCanvases = miniMapNode.Nodes
                .Select(node => new
                {
                    Node = node,
                    Index = GetAdditionalCanvasIndex(node.Text),
                })
                .Where(item => item.Index.HasValue)
                .OrderBy(item => item.Index.Value)
                .ThenBy(item => item.Node.Text, StringComparer.Ordinal)
                .Select(item => item.Node.Text)
                .ToList();
            if (additionalCanvases.Count > 0)
            {
                throw new UnsupportedMapGeometryException(
                    mapId,
                    $"has multiple minimap canvases ({string.Join(", ", additionalCanvases)})");
            }

            return miniMapNode;
        }

        private static MinimapPayload ReadMinimap(Wz_Node miniMapNode, string imageName)
        {
            return new MinimapPayload
            {
                CenterX = miniMapNode.Nodes["centerX"].GetValueEx(0),
                CenterY = miniMapNode.Nodes["centerY"].GetValueEx(0),
                Mag = miniMapNode.Nodes["mag"].GetValueEx(0),
                CanvasWidth = miniMapNode.Nodes["width"].GetValueEx(0),
                CanvasHeight = miniMapNode.Nodes["height"].GetValueEx(0),
                Image = imageName,
            };
        }

        private static void SaveMinimapImage(
            Wz_Node miniMapNode,
            string path,
            int mapId,
            ISet<Wz_Image> extractedImages)
        {
            Wz_Node canvasNode = miniMapNode.FindNodeByPath("canvas").GetLinkedSourceNode(PluginManager.FindWz);
            Wz_Image canvasImage = canvasNode?.GetNodeWzImage();
            if (canvasImage != null)
            {
                extractedImages.Add(canvasImage);
            }

            Wz_Png png = canvasNode.GetValueEx<Wz_Png>(null);
            if (png == null)
            {
                throw new UnsupportedMapGeometryException(mapId, "has no minimap canvas image");
            }

            using var bitmap = png.ExtractPng();
            bitmap.Save(path, ImageFormat.Png);
        }

        private static int ParseMapId(string rawMapId)
        {
            if (!int.TryParse(rawMapId, NumberStyles.None, CultureInfo.InvariantCulture, out int mapId) ||
                mapId > 999999999)
            {
                throw new InvalidOperationException($"Map id '{rawMapId}' must be numeric with at most nine decimal digits.");
            }
            return mapId;
        }

        internal static string FormatWzMapId(int mapId)
        {
            if (mapId < 0 || mapId > 999999999)
            {
                throw new ArgumentOutOfRangeException(nameof(mapId));
            }

            return mapId.ToString("D9", CultureInfo.InvariantCulture);
        }

        internal static IReadOnlyDictionary<int, Wz_Image> BuildMapIndex(IEnumerable<Wz_Node> mapRoots)
        {
            if (mapRoots == null)
            {
                throw new ArgumentNullException(nameof(mapRoots));
            }

            Dictionary<int, Wz_Image> mapIndex = new Dictionary<int, Wz_Image>();
            foreach (Wz_Node mapRoot in mapRoots)
            {
                foreach (Wz_Node node in EnumerateBreadthFirst(mapRoot))
                {
                    if (TryParseMapImageId(node.Text, out int mapId) &&
                        node.Value is Wz_Image image &&
                        !mapIndex.ContainsKey(mapId))
                    {
                        mapIndex.Add(mapId, image);
                    }
                }
            }

            return mapIndex;
        }

        private static IReadOnlyDictionary<int, Wz_Image> BuildMapIndex(Wz_Node root)
        {
            IEnumerable<Wz_Node> mapRoots = root.Nodes
                .Where(node => node.GetNodeWzFile()?.Type == Wz_Type.Map);
            return BuildMapIndex(mapRoots);
        }

        private static IReadOnlyDictionary<int, string> LoadMapNames(WzContext context)
        {
            Wz_File stringWz = context.WzStructure.wz_files.FirstOrDefault(file => file.Type == Wz_Type.String);
            if (stringWz == null)
            {
                return new Dictionary<int, string>();
            }

            Wz_Image mapImage = stringWz.Node?.Nodes["Map.img"]?.Value as Wz_Image;
            if (mapImage == null)
            {
                return new Dictionary<int, string>();
            }

            try
            {
                if (!mapImage.TryExtract())
                {
                    return new Dictionary<int, string>();
                }

                return ReadMapNames(mapImage.Node);
            }
            finally
            {
                mapImage.Unextract();
            }
        }

        internal static IReadOnlyDictionary<int, string> ReadMapNames(Wz_Node mapStringRoot)
        {
            Dictionary<int, string> mapNames = new Dictionary<int, string>();
            if (mapStringRoot == null)
            {
                return mapNames;
            }

            foreach (Wz_Node categoryNode in mapStringRoot.Nodes)
            {
                foreach (Wz_Node mapNode in categoryNode.Nodes)
                {
                    if (!int.TryParse(
                            mapNode.Text,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int mapId) ||
                        mapNode.ResolveUol() is not Wz_Node resolvedMapNode)
                    {
                        continue;
                    }

                    string streetName = ReadString(resolvedMapNode, "streetName");
                    string mapName = ReadString(resolvedMapNode, "mapName");
                    mapNames[mapId] = $"{streetName}：{mapName}";
                }
            }

            return mapNames;
        }

        internal static List<PlatformPayload> ReadPlatforms(Wz_Node footholdRoot)
        {
            List<PlatformPayload> platforms = new List<PlatformPayload>();
            if (footholdRoot == null)
            {
                return platforms;
            }

            foreach (Wz_Node node in Descendants(footholdRoot))
            {
                if (!HasChildren(node, "x1", "x2", "y1", "y2"))
                {
                    continue;
                }

                int x1 = node.Nodes["x1"].GetValueEx(0);
                int x2 = node.Nodes["x2"].GetValueEx(0);
                int y1 = node.Nodes["y1"].GetValueEx(0);
                int y2 = node.Nodes["y2"].GetValueEx(0);
                if (x1 == x2 || y1 != y2)
                {
                    continue;
                }

                platforms.Add(new PlatformPayload
                {
                    X1 = Math.Min(x1, x2),
                    X2 = Math.Max(x1, x2),
                    Y = y1,
                    Kind = "platform",
                });
            }

            return platforms
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X1)
                .ToList();
        }

        private static List<RopePayload> ReadRopes(Wz_Node ladderRopeRoot)
        {
            List<RopePayload> ropes = new List<RopePayload>();
            if (ladderRopeRoot == null)
            {
                return ropes;
            }

            foreach (Wz_Node node in ladderRopeRoot.Nodes)
            {
                if (!HasChildren(node, "x", "y1", "y2"))
                {
                    continue;
                }

                int y1 = node.Nodes["y1"].GetValueEx(0);
                int y2 = node.Nodes["y2"].GetValueEx(0);
                ropes.Add(new RopePayload
                {
                    X = node.Nodes["x"].GetValueEx(0),
                    Y1 = Math.Min(y1, y2),
                    Y2 = Math.Max(y1, y2),
                    Kind = node.Nodes["l"].GetValueEx(0) != 0 ? "ladder" : "rope",
                });
            }

            return ropes
                .OrderBy(r => r.X)
                .ThenBy(r => r.Y1)
                .ToList();
        }

        private static List<PortalPayload> ReadPortals(Wz_Node portalRoot, int mapId)
        {
            List<PortalPayload> portals = new List<PortalPayload>();
            if (portalRoot == null)
            {
                return portals;
            }

            List<Wz_Node> nodes = portalRoot.Nodes.ToList();
            foreach (Wz_Node node in nodes)
            {
                if (!HasChildren(node, "x", "y"))
                {
                    continue;
                }

                int? toMap = node.Nodes["tm"].GetValueEx<int>();
                if (toMap.HasValue && toMap.Value != mapId)
                {
                    continue;
                }

                string targetName = node.Nodes["tn"].GetValueEx<string>(null);
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    continue;
                }

                Wz_Node target = nodes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Nodes["pn"].GetValueEx<string>(null), targetName, StringComparison.Ordinal) ||
                    string.Equals(candidate.Text, targetName, StringComparison.Ordinal));
                if (target == null || !HasChildren(target, "x", "y"))
                {
                    continue;
                }

                int portalType = node.Nodes["pt"].GetValueEx(0);
                portals.Add(new PortalPayload
                {
                    X = node.Nodes["x"].GetValueEx(0),
                    Y = node.Nodes["y"].GetValueEx(0),
                    DestX = target.Nodes["x"].GetValueEx(0),
                    DestY = target.Nodes["y"].GetValueEx(0),
                    Type = PortalTypeName(portalType),
                });
            }

            return portals
                .OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                .ToList();
        }

        private static string PortalTypeName(int type)
        {
            string[] names =
            {
                "sp", "pi", "pv", "pc", "pg", "tp", "ps", "pgi", "psi", "pcs",
                "ph", "psh", "pcj", "pci", "pci2", "pcig", "pshg", "pcc", "pcir"
            };
            return type >= 0 && type < names.Length
                ? names[type]
                : type.ToString(CultureInfo.InvariantCulture);
        }

        private static IEnumerable<Wz_Node> Descendants(Wz_Node root)
        {
            foreach (Wz_Node child in root.Nodes)
            {
                yield return child;
                foreach (Wz_Node descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }

        private static IEnumerable<Wz_Node> EnumerateBreadthFirst(Wz_Node root)
        {
            Queue<Wz_Node> queue = new Queue<Wz_Node>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Wz_Node node = queue.Dequeue();
                yield return node;

                foreach (Wz_Node child in node.Nodes)
                {
                    queue.Enqueue(child);
                }
            }
        }

        private static bool TryParseMapImageId(string nodeText, out int mapId)
        {
            mapId = 0;
            if (string.IsNullOrWhiteSpace(nodeText) ||
                !nodeText.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rawMapId = nodeText.Substring(0, nodeText.Length - ".img".Length);
            return int.TryParse(
                    rawMapId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out mapId) &&
                mapId <= 999999999;
        }

        private static int? GetAdditionalCanvasIndex(string nodeText)
        {
            const string prefix = "canvas";
            if (string.IsNullOrEmpty(nodeText) ||
                !nodeText.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            string suffix = nodeText.Substring(prefix.Length);
            if (!int.TryParse(
                    suffix,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int index) ||
                index <= 0 ||
                !string.Equals(suffix, index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return null;
            }

            return index;
        }

        private static string ReadString(Wz_Node node, string childName)
        {
            Wz_Node child = node.FindNodeByPath(childName);
            return child == null
                ? null
                : Convert.ToString(child.Value, CultureInfo.InvariantCulture);
        }

        private static bool HasChildren(Wz_Node node, params string[] keys)
        {
            return node != null && keys.All(key => node.Nodes[key] != null);
        }

        internal readonly struct MapExportRequest
        {
            public MapExportRequest(string mapId, bool skipUnsupported)
            {
                MapId = mapId;
                SkipUnsupported = skipUnsupported;
            }

            public string MapId { get; }

            public bool SkipUnsupported { get; }
        }

        private readonly struct NormalizedMapExportRequest
        {
            public NormalizedMapExportRequest(int mapId, bool skipUnsupported)
            {
                MapId = mapId;
                SkipUnsupported = skipUnsupported;
            }

            public int MapId { get; }

            public bool SkipUnsupported { get; }
        }

        internal sealed class UnsupportedMapGeometryException : InvalidOperationException
        {
            public UnsupportedMapGeometryException(int mapId, string reason)
                : base(string.Format(CultureInfo.InvariantCulture, "Map {0} {1}.", mapId, reason))
            {
                Reason = reason;
            }

            public string Reason { get; }
        }

        private sealed class ResolvedMap
        {
            public ResolvedMap(Wz_Node node, int mapId)
            {
                Node = node;
                MapId = mapId;
            }

            public Wz_Node Node { get; }

            public int MapId { get; }
        }

        private sealed class MapGeometryPayload
        {
            [JsonPropertyName("schema_version")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("map_id")]
            public int MapId { get; set; }

            [JsonPropertyName("map_name")]
            public string MapName { get; set; }

            [JsonPropertyName("minimap")]
            public MinimapPayload Minimap { get; set; }

            [JsonPropertyName("platforms")]
            public List<PlatformPayload> Platforms { get; set; }

            [JsonPropertyName("ropes")]
            public List<RopePayload> Ropes { get; set; }

            [JsonPropertyName("portals")]
            public List<PortalPayload> Portals { get; set; }
        }

        private sealed class MinimapPayload
        {
            [JsonPropertyName("center_x")]
            public int CenterX { get; set; }

            [JsonPropertyName("center_y")]
            public int CenterY { get; set; }

            [JsonPropertyName("mag")]
            public int Mag { get; set; }

            [JsonPropertyName("canvas_width")]
            public int CanvasWidth { get; set; }

            [JsonPropertyName("canvas_height")]
            public int CanvasHeight { get; set; }

            [JsonPropertyName("image")]
            public string Image { get; set; }
        }

        internal sealed class PlatformPayload
        {
            [JsonPropertyName("x1")]
            public int X1 { get; set; }

            [JsonPropertyName("x2")]
            public int X2 { get; set; }

            [JsonPropertyName("y")]
            public int Y { get; set; }

            [JsonPropertyName("kind")]
            public string Kind { get; set; }
        }

        private sealed class RopePayload
        {
            [JsonPropertyName("x")]
            public int X { get; set; }

            [JsonPropertyName("y1")]
            public int Y1 { get; set; }

            [JsonPropertyName("y2")]
            public int Y2 { get; set; }

            [JsonPropertyName("kind")]
            public string Kind { get; set; }
        }

        private sealed class PortalPayload
        {
            [JsonPropertyName("x")]
            public int X { get; set; }

            [JsonPropertyName("y")]
            public int Y { get; set; }

            [JsonPropertyName("dest_x")]
            public int DestX { get; set; }

            [JsonPropertyName("dest_y")]
            public int DestY { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }
        }
    }
}
