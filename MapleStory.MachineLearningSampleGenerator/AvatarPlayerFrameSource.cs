using System;
using MapleStory.Avatar;
using MapleStory.Sampler.PostProcessor;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal sealed class AvatarPlayerFrameSource : IPlayerFrameSource
    {
        private readonly AvatarGenerator _generator;

        public AvatarPlayerFrameSource(AvatarGenerator generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public int GetBodyFrameCount(PlayerAvatar avatar, string action)
        {
            return _generator.GetBodyFrameCount(new AvatarAppearance(avatar.PartIds), action);
        }

        public int GetEmotionFrameCount(PlayerAvatar avatar, string emotion)
        {
            return _generator.GetEmotionFrameCount(new AvatarAppearance(avatar.PartIds), emotion);
        }

        public PlayerFrame Render(PlayerAvatar avatar, PlayerFramePose pose)
        {
            using AvatarFrameResult result = _generator.RenderFrame(
                new AvatarAppearance(avatar.PartIds),
                new AvatarPose
                {
                    BodyAction = pose.BodyAction,
                    BodyFrame = pose.BodyFrame,
                    Emotion = pose.Emotion,
                    EmotionFrame = pose.EmotionFrame,
                });
            return new PlayerFrame(result.Bitmap, result.BodyBounds);
        }
    }
}
