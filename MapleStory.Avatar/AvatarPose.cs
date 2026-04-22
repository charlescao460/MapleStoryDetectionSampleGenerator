namespace MapleStory.Avatar
{
    public sealed class AvatarPose
    {
        public string BodyAction { get; set; } = "stand1";

        public int BodyFrame { get; set; }

        public string Emotion { get; set; } = "default";

        public int EmotionFrame { get; set; }

        public string TamingAction { get; set; }

        public int TamingFrame { get; set; }

        public int WeaponType { get; set; }

        public int WeaponIndex { get; set; }

        public int EarType { get; set; }

        public bool HairCover { get; set; }

        public bool ShowHairShade { get; set; }
    }
}
