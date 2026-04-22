using System.Collections.Generic;
using System.Linq;
using MapleStory.MachineLearningSampleGenerator.Configuration;
using MapleStory.Sampler.PostProcessor;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class PostProcessorFactory
    {
        public static IReadOnlyList<IPostProcessor> Create(
            IReadOnlyList<PostProcessorConfig> configs,
            IPlayerFrameSource playerFrameSource)
        {
            List<IPostProcessor> processors = new List<IPostProcessor>();
            if (configs == null)
            {
                return processors.AsReadOnly();
            }

            foreach (PostProcessorConfig config in configs)
            {
                switch (config)
                {
                    case PlayerPostProcessorConfig playerConfig:
                        processors.Add(new PlayerProcessor(
                            playerConfig.Avatars.Select(avatar => new PlayerAvatar(avatar.Parts)),
                            playerConfig.Actions,
                            playerConfig.Emotions,
                            playerConfig.Count,
                            playerFrameSource));
                        break;
                    default:
                        throw new ConfigurationException($"Unsupported post processor type '{config?.Type}'.");
                }
            }

            return processors.AsReadOnly();
        }
    }
}
