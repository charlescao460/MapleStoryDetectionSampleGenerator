using System;

namespace MapleStory.Avatar
{
    public sealed class AvatarPartNotFoundException : Exception
    {
        public AvatarPartNotFoundException(int partId)
            : base($"Avatar part {partId:D8}.img was not found under Character.wz.")
        {
            PartId = partId;
        }

        public int PartId { get; }
    }
}
