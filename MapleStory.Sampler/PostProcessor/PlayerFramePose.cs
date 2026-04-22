namespace MapleStory.Sampler.PostProcessor
{
    public sealed class PlayerFramePose
    {
        public string BodyAction { get; set; } = "stand1";

        public int BodyFrame { get; set; }

        public string Emotion { get; set; } = "default";

        public int EmotionFrame { get; set; }
    }
}
