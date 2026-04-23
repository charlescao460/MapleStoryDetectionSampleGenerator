using System;
using System.Collections.Generic;
using MapleStory.MachineLearningSampleGenerator.Configuration;
using MapleStory.Sampler.PostProcessor;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class PlayerPostProcessorValidator
    {
        public static void Validate(
            IReadOnlyList<ResolvedMapConfig> maps,
            IPlayerFrameSource playerFrameSource)
        {
            if (playerFrameSource == null)
            {
                throw new ArgumentNullException(nameof(playerFrameSource));
            }

            if (maps == null)
            {
                return;
            }

            HashSet<string> validatedActions = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> validatedEmotions = new HashSet<string>(StringComparer.Ordinal);
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                IReadOnlyList<PostProcessorConfig> postProcessors = maps[mapIndex].PostProcessors;
                if (postProcessors == null)
                {
                    continue;
                }

                for (int processorIndex = 0; processorIndex < postProcessors.Count; processorIndex++)
                {
                    if (postProcessors[processorIndex] is PlayerPostProcessorConfig playerConfig)
                    {
                        ValidatePlayerPostProcessor(
                            playerConfig,
                            playerFrameSource,
                            $"maps[{mapIndex}].postProcessors[{processorIndex}]",
                            validatedActions,
                            validatedEmotions);
                    }
                }
            }
        }

        private static void ValidatePlayerPostProcessor(
            PlayerPostProcessorConfig config,
            IPlayerFrameSource playerFrameSource,
            string context,
            ISet<string> validatedActions,
            ISet<string> validatedEmotions)
        {
            for (int avatarIndex = 0; avatarIndex < config.Avatars.Count; avatarIndex++)
            {
                PlayerAvatar avatar = new PlayerAvatar(config.Avatars[avatarIndex].Parts);
                string avatarContext = $"{context}.avatars[{avatarIndex}]";
                ValidateAvatarParts(playerFrameSource, avatar, $"{avatarContext}.parts");

                for (int actionIndex = 0; actionIndex < config.Actions.Count; actionIndex++)
                {
                    string action = config.Actions[actionIndex];
                    string validationKey = $"{avatar.CacheKey}|action|{action}";
                    if (validatedActions.Add(validationKey))
                    {
                        int frameCount = ReadFrameCount(
                            () => playerFrameSource.GetBodyFrameCount(avatar, action),
                            $"{context}.actions[{actionIndex}]",
                            action,
                            avatarContext);
                        if (frameCount <= 0)
                        {
                            throw new ConfigurationException(
                                $"{context}.actions[{actionIndex}] '{action}' has no frames for {avatarContext}.");
                        }
                    }
                }

                for (int emotionIndex = 0; emotionIndex < config.Emotions.Count; emotionIndex++)
                {
                    string emotion = config.Emotions[emotionIndex];
                    string validationKey = $"{avatar.CacheKey}|emotion|{emotion}";
                    if (validatedEmotions.Add(validationKey))
                    {
                        int frameCount = ReadFrameCount(
                            () => playerFrameSource.GetEmotionFrameCount(avatar, emotion),
                            $"{context}.emotions[{emotionIndex}]",
                            emotion,
                            avatarContext);
                        if (frameCount <= 0)
                        {
                            throw new ConfigurationException(
                                $"{context}.emotions[{emotionIndex}] '{emotion}' has no frames for {avatarContext}.");
                        }
                    }
                }
            }
        }

        private static void ValidateAvatarParts(
            IPlayerFrameSource playerFrameSource,
            PlayerAvatar avatar,
            string context)
        {
            try
            {
                playerFrameSource.ValidateAvatar(avatar);
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ConfigurationException($"{context} is invalid: {ex.Message}");
            }
        }

        private static int ReadFrameCount(
            Func<int> read,
            string inputContext,
            string inputValue,
            string avatarContext)
        {
            try
            {
                return read();
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(
                    $"{inputContext} '{inputValue}' is invalid for {avatarContext}: {ex.Message}");
            }
        }
    }
}
