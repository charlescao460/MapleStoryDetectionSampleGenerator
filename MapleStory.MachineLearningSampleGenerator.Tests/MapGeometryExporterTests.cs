using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MapleStory.Common;
using MapleStory.MachineLearningSampleGenerator;
using WzComparerR2.WzLib;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class MapGeometryExporterTests
    {
        [Fact]
        public void ReadPlatforms_NormalizesReversedHorizontalEndpoints()
        {
            Wz_Node footholdRoot = new Wz_Node("foothold");
            Wz_Node foothold = footholdRoot.Nodes.Add("1");
            foothold.Nodes.Add("x1").Value = 25;
            foothold.Nodes.Add("x2").Value = -10;
            foothold.Nodes.Add("y1").Value = 40;
            foothold.Nodes.Add("y2").Value = 40;

            MapGeometryExporter.PlatformPayload platform = Assert.Single(
                MapGeometryExporter.ReadPlatforms(footholdRoot));

            Assert.Equal(-10, platform.X1);
            Assert.Equal(25, platform.X2);
        }

        [Fact]
        public void BuildMapIndex_IndexesNumericImagesOnce()
        {
            Wz_Node mapRoot = new Wz_Node("Map");
            Wz_Image firstImage = new Wz_Image("000040000.img", 0, 0, 0, 0, null);
            Wz_Image duplicateImage = new Wz_Image("40000.img", 0, 0, 0, 0, null);
            mapRoot.Nodes.Add("000040000.img").Value = firstImage;
            mapRoot.Nodes.Add("not-a-map.img").Value = new Wz_Image("not-a-map.img", 0, 0, 0, 0, null);
            mapRoot.Nodes.Add("nested").Nodes.Add("40000.img").Value = duplicateImage;

            IReadOnlyDictionary<int, Wz_Image> mapIndex = MapGeometryExporter.BuildMapIndex(
                new[] { mapRoot });

            Assert.Single(mapIndex);
            Assert.Same(firstImage, mapIndex[40000]);
        }

        [Fact]
        public void NormalizeMapRequests_UsesIndexAndKeepsExplicitMapsStrict()
        {
            IReadOnlyList<MapGeometryExporter.NormalizedMapExportRequest> maps =
                MapGeometryExporter.NormalizeMapRequests(
                    new[] { "200000000", "300000000" },
                    new[] { 300000000, 100000000, 200000000 });

            Assert.Equal(new[] { 100000000, 200000000, 300000000 }, maps.Select(map => map.MapId));
            Assert.True(maps[0].SkipUnsupported);
            Assert.False(maps[1].SkipUnsupported);
            Assert.False(maps[2].SkipUnsupported);
        }

        [Fact]
        public void FormatProducerVersion_UsesSourceRevisionIdWithoutDuplication()
        {
            Guid moduleVersionId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

            Assert.Equal(
                "2.1.0+abcdef",
                MapGeometryExporter.FormatProducerVersion("2.1.0", "abcdef", moduleVersionId));
            Assert.Equal(
                "2.1.0+abcdef",
                MapGeometryExporter.FormatProducerVersion("2.1.0+abcdef", "abcdef", moduleVersionId));
        }

        [Fact]
        public void FormatProducerVersion_UsesModuleVersionIdWithoutSourceRevision()
        {
            Guid moduleVersionId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

            Assert.Equal(
                "2.1.0+mvid.0123456789abcdef0123456789abcdef",
                MapGeometryExporter.FormatProducerVersion("2.1.0", null, moduleVersionId));
        }

        [Fact]
        public void ExtractMapImage_RejectsUnidentifiedEncryption()
        {
            Wz_Image image = new UnextractableWzImage();
            try
            {
                InvalidDataException exception = Assert.Throws<InvalidDataException>(
                    () => MapGeometryExporter.ExtractMapImage(image, 410000520));

                Assert.Contains("Failed to extract map 410000520", exception.Message);
            }
            finally
            {
                image.Unextract();
            }
        }

        [Fact]
        public void RequireExtractedImageNode_PreservesChecksumFailure()
        {
            Wz_Image image = new InvalidChecksumWzImage();
            try
            {
                ArgumentException exception = Assert.Throws<ArgumentException>(
                    () => WzContext.RequireExtractedImageNode(image));

                Assert.Equal("checksum error", exception.Message);
            }
            finally
            {
                image.Unextract();
            }
        }

        [Fact]
        public void ExtractMapNames_PreservesChecksumFailure()
        {
            Wz_Image image = new InvalidChecksumWzImage();

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => MapGeometryExporter.ExtractMapNames(image));

            Assert.Equal("checksum error", exception.Message);
        }

        [Fact]
        public void ExtractMapNames_RejectsUnidentifiedEncryption()
        {
            Wz_Image image = new UnextractableWzImage("Map.img");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => MapGeometryExporter.ExtractMapNames(image));

            Assert.Equal("Failed to extract String.wz image 'Map.img'.", exception.Message);
        }

        [Fact]
        public void ExportMaps_CleansStagingDirectoryWhenContextConstructionFails()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string output = Path.Combine(workspace.RootPath, "output");
            MapGeometryExporter exporter = new MapGeometryExporter(workspace.RootPath, Encoding.UTF8);

            Assert.Throws<ArgumentException>(() => exporter.ExportMaps(
                new[] { "410000520" },
                output));

            Assert.Empty(Directory.EnumerateDirectories(workspace.RootPath, ".hecate-map-pack-*"));
        }

        [Fact]
        public void ExportMaps_CreatesStagingBesideOutputWithTrailingSeparator()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string output = Path.Combine(workspace.RootPath, "output") + Path.DirectorySeparatorChar;
            string deletedStagingDirectory = null;
            MapGeometryExporter exporter = new MapGeometryExporter(
                workspace.RootPath,
                Encoding.UTF8,
                (path, recursive) =>
                {
                    deletedStagingDirectory = path;
                    Directory.Delete(path, recursive);
                },
                TextWriter.Null);

            Assert.Throws<ArgumentException>(() => exporter.ExportMaps(
                new[] { "410000520" },
                output));

            Assert.Equal(workspace.RootPath, Path.GetDirectoryName(deletedStagingDirectory));
        }

        [Fact]
        public void ExportMaps_RejectsFileSystemRootOutputDirectory()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string root = Path.GetPathRoot(workspace.RootPath);
            MapGeometryExporter exporter = new MapGeometryExporter(workspace.RootPath, Encoding.UTF8);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => exporter.ExportMaps(
                new[] { "410000520" },
                root));

            Assert.Equal("outputDirectory", exception.ParamName);
            Assert.Contains("filesystem root", exception.Message);
        }

        [Fact]
        public void ExportMaps_PreservesContextFailureWhenCleanupAlsoFails()
        {
            using TestWorkspace workspace = new TestWorkspace();
            using StringWriter errorWriter = new StringWriter();
            string output = Path.Combine(workspace.RootPath, "output");
            MapGeometryExporter exporter = new MapGeometryExporter(
                workspace.RootPath,
                Encoding.UTF8,
                (_, _) => throw new IOException("cleanup failed"),
                errorWriter);

            Assert.Throws<ArgumentException>(() => exporter.ExportMaps(
                new[] { "410000520" },
                output));

            Assert.Contains("cleanup failed", errorWriter.ToString());
        }

        [Fact]
        public void CleanupStagingDirectory_ThrowsCleanupOnlyFailure()
        {
            using TestWorkspace workspace = new TestWorkspace();
            using StringWriter errorWriter = new StringWriter();
            string staging = workspace.CreateDirectory("staging");

            IOException exception = Assert.Throws<IOException>(() =>
                MapGeometryExporter.CleanupStagingDirectory(
                    staging,
                    null,
                    (_, _) => throw new IOException("cleanup failed"),
                    errorWriter));

            Assert.Equal("cleanup failed", exception.Message);
            Assert.Equal(string.Empty, errorWriter.ToString());
        }

        [Fact]
        public void RequireSupportedMinimap_RejectsMissingMinimap()
        {
            Wz_Node mapNode = new Wz_Node("000000000.img");

            MapGeometryExporter.UnsupportedMapGeometryException exception = Assert.Throws<
                MapGeometryExporter.UnsupportedMapGeometryException>(
                () => MapGeometryExporter.RequireSupportedMinimap(mapNode, 0));

            Assert.Equal("has no miniMap node", exception.Reason);
        }

        [Fact]
        public void RequireSupportedMinimap_RejectsMissingCanvas()
        {
            Wz_Node mapNode = new Wz_Node("000000000.img");
            mapNode.Nodes.Add("miniMap");

            MapGeometryExporter.UnsupportedMapGeometryException exception = Assert.Throws<
                MapGeometryExporter.UnsupportedMapGeometryException>(
                () => MapGeometryExporter.RequireSupportedMinimap(mapNode, 0));

            Assert.Equal("has no minimap canvas image", exception.Reason);
        }

        [Fact]
        public void RequireSupportedMinimap_RejectsAdditionalCanvasesInStableOrder()
        {
            Wz_Node mapNode = new Wz_Node("993200000.img");
            Wz_Node miniMapNode = mapNode.Nodes.Add("miniMap");
            miniMapNode.Nodes.Add("canvas");
            miniMapNode.Nodes.Add("canvas2");
            miniMapNode.Nodes.Add("canvas1");

            MapGeometryExporter.UnsupportedMapGeometryException exception = Assert.Throws<
                MapGeometryExporter.UnsupportedMapGeometryException>(
                () => MapGeometryExporter.RequireSupportedMinimap(mapNode, 993200000));

            Assert.Equal("has multiple minimap canvases (canvas1, canvas2)", exception.Reason);
        }

        [Fact]
        public void RequireSupportedMinimap_AcceptsSingleCanvas()
        {
            Wz_Node mapNode = new Wz_Node("410000520.img");
            Wz_Node miniMapNode = mapNode.Nodes.Add("miniMap");
            miniMapNode.Nodes.Add("canvas");

            Assert.Same(
                miniMapNode,
                MapGeometryExporter.RequireSupportedMinimap(mapNode, 410000520));
        }

        [Fact]
        public void ReadMinimap_RequiresPositiveTransformValues()
        {
            Wz_Node miniMapNode = CreateMinimapNode();

            MapGeometryExporter.MinimapPayload minimap = MapGeometryExporter.ReadMinimap(
                miniMapNode,
                "410000520.png",
                410000520);

            Assert.Equal(4, minimap.Mag);
            Assert.Equal(800, minimap.CanvasWidth);
            Assert.Equal(600, minimap.CanvasHeight);
        }

        [Theory]
        [InlineData("mag", null)]
        [InlineData("mag", 0)]
        [InlineData("mag", -1)]
        [InlineData("mag", "4")]
        [InlineData("mag", double.NaN)]
        [InlineData("mag", double.PositiveInfinity)]
        [InlineData("mag", 1.5)]
        [InlineData("width", 0)]
        [InlineData("height", 0)]
        public void ReadMinimap_ClassifiesInvalidTransformAsUnsupported(string fieldName, object invalidValue)
        {
            Wz_Node miniMapNode = CreateMinimapNode(fieldName, invalidValue);

            MapGeometryExporter.UnsupportedMapGeometryException exception = Assert.Throws<
                MapGeometryExporter.UnsupportedMapGeometryException>(() =>
                MapGeometryExporter.ReadMinimap(miniMapNode, "410000520.png", 410000520));

            Assert.Equal(
                $"has invalid minimap {fieldName}; expected a finite positive integer",
                exception.Reason);
        }

        [Fact]
        public void ResolveMinimapCanvas_PreservesLinkedResolverFailure()
        {
            Wz_Node miniMapNode = new Wz_Node("miniMap");
            Wz_Node canvasNode = miniMapNode.Nodes.Add("canvas");
            canvasNode.Nodes.Add("source").Value = "Map/Map/Map4/410000520.img/miniMap/canvas";
            InvalidDataException resolverFailure = new InvalidDataException("linked image extraction failed");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                MapGeometryExporter.ResolveMinimapCanvas(
                    miniMapNode,
                    410000520,
                    _ => throw resolverFailure));

            Assert.Same(resolverFailure, exception);
        }

        [Fact]
        public void ResolveMinimapCanvas_RejectsMissingLinkedTargetAsInvalidData()
        {
            Wz_Node miniMapNode = new Wz_Node("miniMap");
            Wz_Node canvasNode = miniMapNode.Nodes.Add("canvas");
            canvasNode.Nodes.Add("_outlink").Value = "Map/Map/Map4/410000520.img/miniMap/canvas";

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                MapGeometryExporter.ResolveMinimapCanvas(miniMapNode, 410000520, _ => null));

            Assert.Contains("could not be resolved", exception.Message);
        }

        [Fact]
        public void RequireMinimapPng_RejectsMalformedCanvasAsInvalidData()
        {
            Wz_Node canvasNode = new Wz_Node("canvas");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                MapGeometryExporter.RequireMinimapPng(canvasNode, 410000520));

            Assert.Contains("does not contain PNG image data", exception.Message);
        }

        [Fact]
        public void ReadMapNames_ReadsOnlyMapImageHierarchy()
        {
            Wz_Node mapStringRoot = new Wz_Node("Map.img");
            Wz_Node category = mapStringRoot.Nodes.Add("MapleRoad");
            Wz_Node map = category.Nodes.Add("100000000");
            map.Nodes.Add("streetName").Value = "Victoria Road";
            map.Nodes.Add("mapName").Value = "Henesys";

            IReadOnlyDictionary<int, string> mapNames = MapGeometryExporter.ReadMapNames(mapStringRoot);

            Assert.Single(mapNames);
            Assert.Equal("Victoria Road：Henesys", mapNames[100000000]);
        }

        [Theory]
        [InlineData("Victoria Road", null, "Victoria Road")]
        [InlineData(null, "Henesys", "Henesys")]
        [InlineData(null, null, "100000000")]
        [InlineData("", " ", "100000000")]
        public void ReadMapNames_UsesAvailableComponentsOrNumericId(
            string streetName,
            string mapName,
            string expected)
        {
            Wz_Node mapStringRoot = new Wz_Node("Map.img");
            Wz_Node map = mapStringRoot.Nodes.Add("MapleRoad").Nodes.Add("100000000");
            if (streetName != null)
            {
                map.Nodes.Add("streetName").Value = streetName;
            }
            if (mapName != null)
            {
                map.Nodes.Add("mapName").Value = mapName;
            }

            IReadOnlyDictionary<int, string> mapNames = MapGeometryExporter.ReadMapNames(mapStringRoot);

            Assert.Equal(expected, mapNames[100000000]);
        }

        [Fact]
        public void ExportRequestedMap_AddsMapContextAndPreservesFailure()
        {
            using TestWorkspace workspace = new TestWorkspace();
            Wz_Image image = new UnextractableWzImage();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                MapGeometryExporter.ExportRequestedMap(
                    new Dictionary<int, Wz_Image> { [410000520] = image },
                    new MapGeometryExporter.NormalizedMapExportRequest(410000520, false),
                    new Dictionary<int, string>(),
                    workspace.RootPath,
                    TextWriter.Null));

            Assert.StartsWith("Failed to export map 410000520:", exception.Message);
            Assert.IsType<InvalidDataException>(exception.InnerException);
        }

        private static Wz_Node CreateMinimapNode(string invalidFieldName = null, object invalidValue = null)
        {
            Wz_Node miniMapNode = new Wz_Node("miniMap");
            Dictionary<string, int> values = new Dictionary<string, int>
            {
                ["mag"] = 4,
                ["width"] = 800,
                ["height"] = 600,
            };
            foreach (KeyValuePair<string, int> value in values)
            {
                if (!string.Equals(value.Key, invalidFieldName, StringComparison.Ordinal))
                {
                    miniMapNode.Nodes.Add(value.Key).Value = value.Value;
                }
            }
            if (invalidFieldName != null && invalidValue != null)
            {
                miniMapNode.Nodes.Add(invalidFieldName).Value = invalidValue;
            }

            return miniMapNode;
        }

        private sealed class UnextractableWzImage : Wz_Image
        {
            public UnextractableWzImage(string name = "410000520.img")
                : base(name, 2, 0, 0, 0, new InMemoryMapleStoryFile())
            {
            }

            public override Stream OpenRead()
            {
                return new MemoryStream(new byte[] { 0x73, 0x00 });
            }
        }

        private sealed class InvalidChecksumWzImage : Wz_Image
        {
            public InvalidChecksumWzImage()
                : base("linked.img", 1, 1, 0, 0, new InMemoryMapleStoryFile(false))
            {
            }

            public override Stream OpenRead()
            {
                return new MemoryStream(new byte[] { 0x00 });
            }
        }

        private sealed class InMemoryMapleStoryFile : IMapleStoryFile
        {
            public InMemoryMapleStoryFile(bool imgCheckDisabled = true)
            {
                WzStructure = new Wz_Structure
                {
                    ImgCheckDisabled = imgCheckDisabled,
                };
            }

            public Wz_Structure WzStructure { get; }

            public Stream FileStream => Stream.Null;

            public object ReadLock { get; } = new object();

            public void Dispose()
            {
            }
        }
    }
}
