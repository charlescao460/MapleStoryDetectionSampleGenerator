using System;
using System.Collections.Generic;
using System.Linq;

namespace MapleStory.Avatar
{
    public sealed class AvatarAppearance
    {
        public static readonly AvatarAppearance Empty = new AvatarAppearance(Array.Empty<int>());

        public AvatarAppearance(IEnumerable<int> partIds)
        {
            if (partIds == null)
            {
                throw new ArgumentNullException(nameof(partIds));
            }

            int[] ids = partIds.ToArray();
            if (ids.Any(id => id < 0))
            {
                throw new ArgumentOutOfRangeException(nameof(partIds), "Avatar part IDs must be non-negative.");
            }

            PartIds = ids;
        }

        public IReadOnlyList<int> PartIds { get; }

        public static AvatarAppearance FromPartIds(params int[] partIds)
        {
            return new AvatarAppearance(partIds);
        }
    }
}
