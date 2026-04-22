using System;
using System.Collections.Generic;
using System.Linq;

namespace MapleStory.Sampler.PostProcessor
{
    public sealed class PlayerAvatar
    {
        public PlayerAvatar(IEnumerable<int> partIds)
        {
            if (partIds == null)
            {
                throw new ArgumentNullException(nameof(partIds));
            }

            int[] ids = partIds.ToArray();
            if (ids.Length == 0)
            {
                throw new ArgumentException("Player avatar parts cannot be empty.", nameof(partIds));
            }

            PartIds = ids;
            CacheKey = string.Join(",", ids);
        }

        public IReadOnlyList<int> PartIds { get; }

        public string CacheKey { get; }
    }
}
