using System;
using System.Drawing;
using MapleStory.Avatar;
using MapleStory.Avatar.DebugCli;
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
    }
}
