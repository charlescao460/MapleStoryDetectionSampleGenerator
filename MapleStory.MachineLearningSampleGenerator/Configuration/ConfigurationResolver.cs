using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MapleStory.Common;

namespace MapleStory.MachineLearningSampleGenerator.Configuration
{
    internal sealed class ConfigurationResolver
    {
        public ResolvedRunConfig Resolve(GeneratorConfig config, string configPath)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            string fullConfigPath = Path.GetFullPath(configPath ?? string.Empty);
            string configDirectory = Path.GetDirectoryName(fullConfigPath) ?? Directory.GetCurrentDirectory();
            string mapleStoryPath = ResolveMapleStoryPath(config.MapleStoryPath, configDirectory);
            Encoding textEncoding = ResolveEncoding(config.Encoding);
            OutputFormat outputFormat = ResolveOutputFormat(config.Output?.Format);
            string outputPath = ResolveExistingDirectory(config.Output?.Path, configDirectory, "output.path");
            string outputName = RequireNonEmpty(config.Output?.Name, "output.name");
            int renderWidth = config.Render?.Width ?? 0;
            int renderHeight = config.Render?.Height ?? 0;
            if (renderWidth <= 0)
            {
                throw new ConfigurationException("render.width must be greater than 0.");
            }
            if (renderHeight <= 0)
            {
                throw new ConfigurationException("render.height must be greater than 0.");
            }

            ResolvedSampling defaultSampling = ResolveRootSampling(config.Sampling);
            IReadOnlyList<PostProcessorConfig> defaultPostProcessors =
                ResolvePostProcessors(config.PostProcessors, configDirectory, "postProcessors");

            if (config.Maps == null || config.Maps.Count == 0)
            {
                throw new ConfigurationException("maps must contain at least one entry.");
            }

            List<ResolvedMapConfig> maps = new List<ResolvedMapConfig>();
            for (int i = 0; i < config.Maps.Count; i++)
            {
                MapConfig map = config.Maps[i];
                string mapContext = $"maps[{i}]";
                string id = ResolveMapId(map?.Id, $"{mapContext}.id");
                ResolvedSampling sampling = ResolveMapSampling(defaultSampling, map?.Sampling, $"{mapContext}.sampling");
                IReadOnlyList<PostProcessorConfig> postProcessors = map?.PostProcessors == null
                    ? defaultPostProcessors
                    : ResolvePostProcessors(map.PostProcessors, configDirectory, $"{mapContext}.postProcessors");

                maps.Add(new ResolvedMapConfig(id, sampling.XStep, sampling.YStep, sampling.IntervalMs, postProcessors));
            }

            return new ResolvedRunConfig(
                fullConfigPath,
                mapleStoryPath,
                textEncoding,
                outputFormat,
                outputPath,
                outputName,
                renderWidth,
                renderHeight,
                maps.AsReadOnly());
        }

        private static string ResolveMapleStoryPath(string configuredPath, string configDirectory)
        {
            string mapleStoryPath = configuredPath;
            if (string.IsNullOrWhiteSpace(mapleStoryPath))
            {
                if (!MapleStoryPathHelper.FoundMapleStoryInstalled)
                {
                    throw new ConfigurationException(
                        "Cannot find MapleStory installed location. Set mapleStoryPath in the YAML config or retry as Administrator.");
                }
                mapleStoryPath = MapleStoryPathHelper.MapleStoryInstallDirectory;
            }

            string resolvedPath = ResolvePath(configDirectory, mapleStoryPath);
            if (!Directory.Exists(resolvedPath))
            {
                throw new ConfigurationException($"Configured MapleStory path '{resolvedPath}' does not exist.");
            }

            string baseWzPath = Path.Combine(resolvedPath, MapleStoryPathHelper.MapleStoryBaseWzName);
            if (!File.Exists(baseWzPath))
            {
                throw new ConfigurationException(
                    $"Configured MapleStory path '{resolvedPath}' does not contain '{MapleStoryPathHelper.MapleStoryBaseWzName}'.");
            }

            return resolvedPath;
        }

        private static Encoding ResolveEncoding(string encodingName)
        {
            if (string.IsNullOrWhiteSpace(encodingName))
            {
                return Encoding.Default;
            }

            try
            {
                return Encoding.GetEncoding(encodingName);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                throw new ConfigurationException($"'{encodingName}' is not an available encoding.");
            }
        }

        private static OutputFormat ResolveOutputFormat(string rawFormat)
        {
            if (!Enum.TryParse(rawFormat, true, out OutputFormat outputFormat))
            {
                throw new ConfigurationException($"Unsupported output.format '{rawFormat}'.");
            }

            return outputFormat;
        }

        private static ResolvedSampling ResolveRootSampling(SamplingConfig sampling)
        {
            if (sampling == null)
            {
                throw new ConfigurationException("sampling is required.");
            }

            return ResolveSampling(
                sampling.XStep,
                sampling.YStep,
                sampling.IntervalMs,
                "sampling");
        }

        private static ResolvedSampling ResolveMapSampling(
            ResolvedSampling defaults,
            SamplingConfig sampling,
            string context)
        {
            if (sampling == null)
            {
                return defaults;
            }

            int? xStep = sampling.XStep ?? defaults.XStep;
            int? yStep = sampling.YStep ?? defaults.YStep;
            int? intervalMs = sampling.IntervalMs ?? defaults.IntervalMs;
            return ResolveSampling(xStep, yStep, intervalMs, context);
        }

        private static ResolvedSampling ResolveSampling(int? xStep, int? yStep, int? intervalMs, string context)
        {
            if (!xStep.HasValue)
            {
                throw new ConfigurationException($"{context}.xStep is required.");
            }
            if (!yStep.HasValue)
            {
                throw new ConfigurationException($"{context}.yStep is required.");
            }
            if (xStep.Value <= 0)
            {
                throw new ConfigurationException($"{context}.xStep must be greater than 0.");
            }
            if (yStep.Value <= 0)
            {
                throw new ConfigurationException($"{context}.yStep must be greater than 0.");
            }

            int resolvedInterval = intervalMs ?? 0;
            if (resolvedInterval < 0)
            {
                throw new ConfigurationException($"{context}.intervalMs cannot be negative.");
            }

            return new ResolvedSampling(xStep.Value, yStep.Value, resolvedInterval);
        }

        private static IReadOnlyList<PostProcessorConfig> ResolvePostProcessors(
            IEnumerable<PostProcessorConfig> postProcessors,
            string configDirectory,
            string context)
        {
            List<PostProcessorConfig> resolved = new List<PostProcessorConfig>();
            if (postProcessors == null)
            {
                return resolved.AsReadOnly();
            }

            int index = 0;
            foreach (PostProcessorConfig postProcessor in postProcessors)
            {
                switch (postProcessor)
                {
                    case PlayerPostProcessorConfig playerConfig:
                        resolved.Add(ResolvePlayerPostProcessor(playerConfig, $"{context}[{index}]"));
                        break;
                    case null:
                        throw new ConfigurationException($"{context}[{index}] cannot be null.");
                    default:
                        throw new ConfigurationException($"Unsupported post processor type '{postProcessor.Type}' in {context}[{index}].");
                }
                index++;
            }

            return resolved.AsReadOnly();
        }

        private static PlayerPostProcessorConfig ResolvePlayerPostProcessor(
            PlayerPostProcessorConfig config,
            string context)
        {
            if (config.Count <= 0)
            {
                throw new ConfigurationException($"{context}.count must be greater than 0.");
            }

            List<string> actions = ResolveRequiredStringList(config.Actions, $"{context}.actions");
            List<string> emotions = ResolveRequiredStringList(config.Emotions, $"{context}.emotions");
            if (config.Avatars == null || config.Avatars.Count == 0)
            {
                throw new ConfigurationException($"{context}.avatars cannot be empty.");
            }

            List<PlayerAvatarConfig> avatars = new List<PlayerAvatarConfig>(config.Avatars.Count);
            for (int i = 0; i < config.Avatars.Count; i++)
            {
                PlayerAvatarConfig avatar = config.Avatars[i];
                string avatarContext = $"{context}.avatars[{i}].parts";
                if (avatar?.Parts == null || avatar.Parts.Count == 0)
                {
                    throw new ConfigurationException($"{avatarContext} cannot be empty.");
                }

                if (avatar.Parts.Any(part => part < 0))
                {
                    throw new ConfigurationException($"{avatarContext} cannot contain negative IDs.");
                }

                avatars.Add(new PlayerAvatarConfig
                {
                    Parts = avatar.Parts.ToList(),
                });
            }

            return new PlayerPostProcessorConfig
            {
                Count = config.Count,
                Actions = actions,
                Emotions = emotions,
                Avatars = avatars,
            };
        }

        private static List<string> ResolveRequiredStringList(IList<string> values, string context)
        {
            if (values == null || values.Count == 0)
            {
                throw new ConfigurationException($"{context} cannot be empty.");
            }

            List<string> resolved = new List<string>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                resolved.Add(RequireNonEmpty(values[i], $"{context}[{i}]"));
            }

            return resolved;
        }

        private static string ResolveMapId(string mapId, string context)
        {
            string resolvedId = RequireNonEmpty(mapId, context);
            if (!resolvedId.All(char.IsDigit))
            {
                throw new ConfigurationException($"{context} must contain digits only.");
            }

            return resolvedId;
        }

        private static string ResolveExistingDirectory(string path, string configDirectory, string context)
        {
            string resolvedPath = ResolvePath(configDirectory, RequireNonEmpty(path, context));
            if (!Directory.Exists(resolvedPath))
            {
                throw new ConfigurationException($"{context} '{resolvedPath}' does not exist.");
            }
            return resolvedPath;
        }

        private static string ResolvePath(string configDirectory, string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(configDirectory, path));
        }

        private static string RequireNonEmpty(string value, string context)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationException($"{context} cannot be empty.");
            }
            return value;
        }

        private readonly struct ResolvedSampling
        {
            public ResolvedSampling(int xStep, int yStep, int intervalMs)
            {
                XStep = xStep;
                YStep = yStep;
                IntervalMs = intervalMs;
            }

            public int XStep { get; }

            public int YStep { get; }

            public int IntervalMs { get; }
        }
    }
}
