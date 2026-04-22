namespace MapleStory.Sampler.PostProcessor
{
    public interface IPlayerFrameSource
    {
        int GetBodyFrameCount(PlayerAvatar avatar, string action);

        int GetEmotionFrameCount(PlayerAvatar avatar, string emotion);

        PlayerFrame Render(PlayerAvatar avatar, PlayerFramePose pose);
    }
}
