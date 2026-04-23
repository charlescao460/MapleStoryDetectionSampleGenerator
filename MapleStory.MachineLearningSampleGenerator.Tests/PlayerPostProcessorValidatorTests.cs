using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MapleStory.MachineLearningSampleGenerator.Configuration;
using MapleStory.Sampler.PostProcessor;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public sealed class PlayerPostProcessorValidatorTests
    {
        [Fact]
        public void Validate_ValidPlayerInputs_Succeeds()
        {
            FakePlayerFrameSource frameSource = new FakePlayerFrameSource();

            PlayerPostProcessorValidator.Validate(
                new[] { CreateMapConfig(CreatePlayerConfig()) },
                frameSource);

            Assert.Contains("stand1", frameSource.ActionsChecked);
            Assert.Contains("default", frameSource.EmotionsChecked);
        }

        [Fact]
        public void Validate_InvalidAvatarPart_ThrowsConfigurationException()
        {
            FakePlayerFrameSource frameSource = new FakePlayerFrameSource
            {
                InvalidPartId = 99999999,
            };

            ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
                PlayerPostProcessorValidator.Validate(
                    new[] { CreateMapConfig(CreatePlayerConfig(parts: new[] { 2000, 99999999 })) },
                    frameSource));

            Assert.Contains("avatars[0].parts", exception.Message);
            Assert.Contains("99999999", exception.Message);
        }

        [Fact]
        public void Validate_InvalidAction_ThrowsConfigurationException()
        {
            FakePlayerFrameSource frameSource = new FakePlayerFrameSource
            {
                InvalidAction = "madeUpAction",
            };

            ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
                PlayerPostProcessorValidator.Validate(
                    new[] { CreateMapConfig(CreatePlayerConfig(actions: new[] { "madeUpAction" })) },
                    frameSource));

            Assert.Contains("actions[0]", exception.Message);
            Assert.Contains("madeUpAction", exception.Message);
        }

        [Fact]
        public void Validate_InvalidEmotion_ThrowsConfigurationException()
        {
            FakePlayerFrameSource frameSource = new FakePlayerFrameSource
            {
                InvalidEmotion = "madeUpEmotion",
            };

            ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
                PlayerPostProcessorValidator.Validate(
                    new[] { CreateMapConfig(CreatePlayerConfig(emotions: new[] { "madeUpEmotion" })) },
                    frameSource));

            Assert.Contains("emotions[0]", exception.Message);
            Assert.Contains("madeUpEmotion", exception.Message);
        }

        [Fact]
        public void Validate_EmotionWithNoFrames_ThrowsConfigurationException()
        {
            FakePlayerFrameSource frameSource = new FakePlayerFrameSource
            {
                EmotionFrameCount = 0,
            };

            ConfigurationException exception = Assert.Throws<ConfigurationException>(() =>
                PlayerPostProcessorValidator.Validate(
                    new[] { CreateMapConfig(CreatePlayerConfig()) },
                    frameSource));

            Assert.Contains("has no frames", exception.Message);
            Assert.Contains("emotions[0]", exception.Message);
        }

        private static ResolvedMapConfig CreateMapConfig(PlayerPostProcessorConfig playerConfig)
        {
            return new ResolvedMapConfig(
                "993134200",
                5,
                5,
                0,
                new PostProcessorConfig[] { playerConfig });
        }

        private static PlayerPostProcessorConfig CreatePlayerConfig(
            int[] parts = null,
            string[] actions = null,
            string[] emotions = null)
        {
            return new PlayerPostProcessorConfig
            {
                Count = 1,
                Actions = actions ?? new[] { "stand1" },
                Emotions = emotions ?? new[] { "default" },
                Avatars = new[]
                {
                    new PlayerAvatarConfig
                    {
                        Parts = parts ?? new[] { 2000, 12003, 20000, 30000 },
                    },
                },
            };
        }

        private sealed class FakePlayerFrameSource : IPlayerFrameSource
        {
            public int? InvalidPartId { get; set; }

            public string InvalidAction { get; set; }

            public string InvalidEmotion { get; set; }

            public int BodyFrameCount { get; set; } = 1;

            public int EmotionFrameCount { get; set; } = 1;

            public List<string> ActionsChecked { get; } = new List<string>();

            public List<string> EmotionsChecked { get; } = new List<string>();

            public void ValidateAvatar(PlayerAvatar avatar)
            {
                ValidateParts(avatar);
            }

            public int GetBodyFrameCount(PlayerAvatar avatar, string action)
            {
                ValidateParts(avatar);
                ActionsChecked.Add(action);
                if (action == InvalidAction)
                {
                    throw new InvalidOperationException($"Body action '{action}' has no frames.");
                }

                return BodyFrameCount;
            }

            public int GetEmotionFrameCount(PlayerAvatar avatar, string emotion)
            {
                ValidateParts(avatar);
                EmotionsChecked.Add(emotion);
                if (emotion == InvalidEmotion)
                {
                    throw new InvalidOperationException($"Emotion '{emotion}' has no frames for the current face.");
                }

                return EmotionFrameCount;
            }

            public PlayerFrame Render(PlayerAvatar avatar, PlayerFramePose pose)
            {
                return new PlayerFrame(new Bitmap(1, 1), new Rectangle(0, 0, 1, 1));
            }

            private void ValidateParts(PlayerAvatar avatar)
            {
                if (InvalidPartId.HasValue && avatar.PartIds.Contains(InvalidPartId.Value))
                {
                    throw new InvalidOperationException($"Avatar part {InvalidPartId.Value:D8}.img was not found.");
                }
            }
        }
    }
}
