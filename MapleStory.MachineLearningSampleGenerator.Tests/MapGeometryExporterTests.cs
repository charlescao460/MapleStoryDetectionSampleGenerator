using System;
using System.Collections.Generic;
using MapleStory.MachineLearningSampleGenerator;
using WzComparerR2.WzLib;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class MapGeometryExporterTests
    {
        [Theory]
        [InlineData(40000, "000040000")]
        [InlineData(410000520, "410000520")]
        [InlineData(0, "000000000")]
        public void FormatWzMapId_UsesCanonicalNineDigitName(int mapId, string expected)
        {
            Assert.Equal(expected, MapGeometryExporter.FormatWzMapId(mapId));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1000000000)]
        public void FormatWzMapId_RejectsIdsOutsideNineDigitRange(int mapId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MapGeometryExporter.FormatWzMapId(mapId));
        }

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
    }
}
