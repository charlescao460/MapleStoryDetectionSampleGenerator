using System;
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
    }
}
