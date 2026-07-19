using MapleStory.Common;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class WzContextTests
    {
        [Fact]
        public void OrderMsPackPaths_UsesStableOrdinalOrder()
        {
            string[] paths =
            {
                @"C:\MapleStory\Packs\b.ms",
                @"C:\MapleStory\Packs\a.ms",
                @"C:\MapleStory\Packs\A.ms",
            };

            Assert.Equal(
                new[]
                {
                    @"C:\MapleStory\Packs\A.ms",
                    @"C:\MapleStory\Packs\a.ms",
                    @"C:\MapleStory\Packs\b.ms",
                },
                WzContext.OrderMsPackPaths(paths));
        }
    }
}
