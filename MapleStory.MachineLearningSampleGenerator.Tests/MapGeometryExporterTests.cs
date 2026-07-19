using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private sealed class UnextractableWzImage : Wz_Image
        {
            public UnextractableWzImage()
                : base("410000520.img", 2, 0, 0, 0, new InMemoryMapleStoryFile())
            {
            }

            public override Stream OpenRead()
            {
                return new MemoryStream(new byte[] { 0x73, 0x00 });
            }
        }

        private sealed class InMemoryMapleStoryFile : IMapleStoryFile
        {
            public InMemoryMapleStoryFile()
            {
                WzStructure = new Wz_Structure
                {
                    ImgCheckDisabled = true,
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
