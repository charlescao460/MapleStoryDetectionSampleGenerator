using System;
using System.Drawing;
using MapleStory.Avatar;
using MapleStory.Avatar.DebugCli;
using WzComparerR2.WzLib;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class AvatarApiTests
    {
        [Fact]
        public void AvatarAppearance_PreservesOrderedPartIds()
        {
            AvatarAppearance appearance = AvatarAppearance.FromPartIds(2000, 12003, 20000, 30000, 1040036);

            Assert.Equal(new[] { 2000, 12003, 20000, 30000, 1040036 }, appearance.PartIds);
        }

        [Fact]
        public void AvatarPose_DefaultsToStandAndDefaultFace()
        {
            AvatarPose pose = new AvatarPose();

            Assert.Equal("stand1", pose.BodyAction);
            Assert.Equal(0, pose.BodyFrame);
            Assert.Equal("default", pose.Emotion);
            Assert.Equal(0, pose.EmotionFrame);
            Assert.Equal(0, pose.WeaponType);
            Assert.Equal(0, pose.WeaponIndex);
            Assert.False(pose.HairCover);
            Assert.False(pose.HasExplicitHairCover);
        }

        [Fact]
        public void AvatarPose_TracksExplicitHairCoverFalse()
        {
            AvatarPose pose = new AvatarPose
            {
                HairCover = false
            };

            Assert.False(pose.HairCover);
            Assert.True(pose.HasExplicitHairCover);
        }

        [Theory]
        [InlineData("Cp", "Cp", true, false)]
        [InlineData("Cp", "CpH1H5", true, true)]
        [InlineData("Cp", null, true, true)]
        [InlineData("cp", "CpH1H5", false, false)]
        [InlineData(null, "CpH1H5", false, false)]
        public void AvatarGenerator_ReadsCapHairCoverMetadata(
            string islot,
            string vslot,
            bool expectedHasAutomaticHairCover,
            bool expectedHairCover)
        {
            Wz_Node partNode = CreatePartNode(islot, vslot);

            bool hasAutomaticHairCover = AvatarGenerator.TryGetAutomaticHairCover(partNode, out bool hairCover);

            Assert.Equal(expectedHasAutomaticHairCover, hasAutomaticHairCover);
            Assert.Equal(expectedHairCover, hairCover);
        }

        [Fact]
        public void AvatarGenerator_LastCapMetadataWins()
        {
            bool? automaticHairCover = null;
            foreach (Wz_Node partNode in new[]
            {
                CreatePartNode("Cp", "CpH1H5"),
                CreatePartNode("So", "CpH1H5"),
                CreatePartNode("Cp", "Cp")
            })
            {
                if (AvatarGenerator.TryGetAutomaticHairCover(partNode, out bool hairCover))
                {
                    automaticHairCover = hairCover;
                }
            }

            Assert.False(automaticHairCover);
        }

        [Fact]
        public void AvatarFrameResult_ExposesBitmapAndBodyBounds()
        {
            using (Bitmap bitmap = new Bitmap(2, 3))
            using (AvatarFrameResult result = new AvatarFrameResult(
                bitmap,
                new Point(10, 20),
                new Point(3, 4),
                5,
                6,
                100,
                0,
                0))
            {
                Assert.Equal(2, result.Width);
                Assert.Equal(3, result.Height);
                Assert.NotSame(bitmap, result.Bitmap);
                Assert.Equal(new Point(3, 4), result.BodyOrigin);
                Assert.Equal(5, result.BodyWidth);
                Assert.Equal(6, result.BodyHeight);
                Assert.Equal(new Rectangle(7, 16, 5, 6), result.BodyBounds);
                Assert.NotEmpty(result.PngBytes);
            }
        }

        [Fact]
        public void AvatarFrameResult_EmptyBodyBoundsWhenNoBodyPixels()
        {
            using (Bitmap bitmap = new Bitmap(2, 3))
            using (AvatarFrameResult result = new AvatarFrameResult(
                bitmap,
                new Point(10, 20),
                Point.Empty,
                0,
                0,
                100,
                0,
                0))
            {
                Assert.Equal(0, result.BodyWidth);
                Assert.Equal(0, result.BodyHeight);
                Assert.Equal(Rectangle.Empty, result.BodyBounds);
            }
        }

        [Fact]
        public void DebugCliParse_RenderOptions_Succeeds()
        {
            AvatarDebugCliOptions options = AvatarDebugCliOptions.Parse(new[]
            {
                "--path", @"C:\MapleStory",
                "--parts", "2000,12003,20000,30000",
                "--action", "walk1",
                "--body-frame", "2",
                "--emotion", "smile",
                "--emotion-frame", "1",
                "--weapon-type", "30",
                "--weapon-index", "2",
                "--out", "avatar.png"
            });

            Assert.Equal(@"C:\MapleStory", options.MapleStoryPath);
            Assert.Equal(new[] { 2000, 12003, 20000, 30000 }, options.PartIds);
            Assert.Equal("walk1", options.Action);
            Assert.Equal(2, options.BodyFrame);
            Assert.Equal("smile", options.Emotion);
            Assert.Equal(1, options.EmotionFrame);
            Assert.Equal(30, options.WeaponType);
            Assert.Equal(2, options.WeaponIndex);
            Assert.Equal("avatar.png", options.OutputPath);

            AvatarPose pose = options.ToPose();
            Assert.Equal(30, pose.WeaponType);
            Assert.Equal(2, pose.WeaponIndex);
        }

        [Fact]
        public void DebugCliParse_ListActions_AllowsNoParts()
        {
            AvatarDebugCliOptions options = AvatarDebugCliOptions.Parse(new[]
            {
                "--list-actions"
            });

            Assert.True(options.ListActions);
            Assert.Empty(options.PartIds);
        }

        [Fact]
        public void DebugCliParse_RenderOptions_AllowsMissingPath()
        {
            AvatarDebugCliOptions options = AvatarDebugCliOptions.Parse(new[]
            {
                "--parts", "2000,12003,20000,30000",
                "--out", "avatar.png"
            });

            Assert.Equal(string.Empty, options.MapleStoryPath);
            Assert.Equal(new[] { 2000, 12003, 20000, 30000 }, options.PartIds);
            Assert.Equal("avatar.png", options.OutputPath);
        }

        [Fact]
        public void DebugCliParse_RenderWithoutParts_Throws()
        {
            Assert.Throws<ArgumentException>(() => AvatarDebugCliOptions.Parse(new[]
            {
                "--path", @"C:\MapleStory",
                "--out", "avatar.png"
            }));
        }

        [Fact]
        public void DebugCliParse_AllBodyFramesRequiresOutputDirectory()
        {
            Assert.Throws<ArgumentException>(() => AvatarDebugCliOptions.Parse(new[]
            {
                "--path", @"C:\MapleStory",
                "--parts", "2000,12003",
                "--all-body-frames"
            }));
        }

        private static Wz_Node CreatePartNode(string islot, string vslot)
        {
            Wz_Node partNode = new Wz_Node("01000000.img");
            Wz_Node infoNode = partNode.Nodes.Add("info");
            if (islot != null)
            {
                infoNode.Nodes.Add("islot").Value = islot;
            }

            if (vslot != null)
            {
                infoNode.Nodes.Add("vslot").Value = vslot;
            }

            return partNode;
        }
    }
}
