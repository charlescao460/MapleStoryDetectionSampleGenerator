using System;
using MapleStory.MachineLearningSampleGenerator;
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
    }
}
