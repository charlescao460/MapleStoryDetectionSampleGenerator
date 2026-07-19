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
        private readonly IMapCatalog _mapCatalog;

        public ConfigurationResolver()
            : this(new WzMapCatalog())
        {
        }

        internal ConfigurationResolver(IMapCatalog mapCatalog)
        {
            _mapCatalog = mapCatalog ?? throw new ArgumentNullException(nameof(mapCatalog));
        }

        public ResolvedRunConfig Resolve(GeneratorConfig config, string configPath)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            string fullConfigPath = Path.GetFullPath(configPath ?? string.Empty);
            string configDirectory = Path.GetDirectoryName(fullConfigPath) ?? Directory.GetCurrentDirectory();
            GenerationMode generationMode = ResolveGenerationMode(config.Mode);
            string mapleStoryPath = ResolveMapleStoryPath(config.MapleStoryPath, configDirectory);
            Encoding textEncoding = ResolveEncoding(config.Encoding);
            OutputFormat outputFormat = ResolveOutputFormat(config.Output?.Format);
            ValidateModeOutput(generationMode, outputFormat);
            string outputPath = ResolveDirectoryPath(config.Output?.Path, configDirectory, "output.path");
            string outputName = generationMode == GenerationMode.Geometry
                ? (string.IsNullOrWhiteSpace(config.Output?.Name) ? "geometry" : config.Output.Name)
                : RequireNonEmpty(config.Output?.Name, "output.name");
            int renderWidth = config.Render?.Width ?? 0;
            int renderHeight = config.Render?.Height ?? 0;
            if (generationMode != GenerationMode.Geometry)
            {
                if (renderWidth <= 0)
                {
                    throw new ConfigurationException("render.width must be greater than 0.");
                }
                if (renderHeight <= 0)
                {
                    throw new ConfigurationException("render.height must be greater than 0.");
                }
            }
            int concurrency = ResolveConcurrency(config.Concurrency);

            ResolvedSampling defaultSampling = generationMode == GenerationMode.Geometry
                ? new ResolvedSampling(1, 0)
                : ResolveRootSampling(config.Sampling);
            IReadOnlyList<PostProcessorConfig> defaultPostProcessors =
                ResolvePostProcessors(config.PostProcessors, configDirectory, "postProcessors");

            if ((config.Maps == null || config.Maps.Count == 0) && config.RandomMaps == null && !config.AllMaps)
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

                maps.Add(new ResolvedMapConfig(id, sampling.Count, sampling.IntervalMs, postProcessors));
            }

            AppendRandomMaps(
                generationMode,
                config.RandomMaps,
                config.AllMaps,
                mapleStoryPath,
                textEncoding,
                defaultSampling,
                maps);

            bool exportAllMaps = generationMode == GenerationMode.Geometry && config.AllMaps;
            AppendAllMaps(
                config.AllMaps && !exportAllMaps,
                mapleStoryPath,
                textEncoding,
                defaultSampling,
                defaultPostProcessors,
                maps);

            if (maps.Count == 0 && !exportAllMaps)
            {
                throw new ConfigurationException("maps did not resolve to any available map IDs.");
            }

            ValidateModePostProcessors(generationMode, config, maps);

            return new ResolvedRunConfig(
                fullConfigPath,
                mapleStoryPath,
                textEncoding,
                generationMode,
                outputFormat,
                outputPath,
                outputName,
                renderWidth,
                renderHeight,
                concurrency,
                exportAllMaps,
                maps.AsReadOnly());
        }

        private void AppendRandomMaps(
            GenerationMode generationMode,
            RandomMapConfig randomMaps,
            bool allMaps,
            string mapleStoryPath,
            Encoding textEncoding,
            ResolvedSampling defaultSampling,
            List<ResolvedMapConfig> maps)
        {
            if (randomMaps == null)
            {
                return;
            }

            if (allMaps)
            {
                throw new ConfigurationException("maps.random cannot be combined with maps.allMaps.");
            }

            if (generationMode != GenerationMode.Rune)
            {
                throw new ConfigurationException("maps.random is only supported when mode is 'rune'.");
            }

            List<string> candidates = ListCandidateMapIds(mapleStoryPath, textEncoding, maps);

            if (randomMaps.Count > candidates.Count)
            {
                throw new ConfigurationException(
                    $"maps.random.count requested {randomMaps.Count} maps, but only {candidates.Count} candidate maps are available.");
            }

            Random random = randomMaps.Seed.HasValue
                ? new Random(randomMaps.Seed.Value)
                : new Random();
            Shuffle(candidates, random);
            foreach (string id in candidates.Take(randomMaps.Count))
            {
                maps.Add(new ResolvedMapConfig(
                    id,
                    defaultSampling.Count,
                    defaultSampling.IntervalMs,
                    Array.Empty<PostProcessorConfig>(),
                    MapSelectionSource.Random));
            }
        }

        private void AppendAllMaps(
            bool allMaps,
            string mapleStoryPath,
            Encoding textEncoding,
            ResolvedSampling defaultSampling,
            IReadOnlyList<PostProcessorConfig> defaultPostProcessors,
            List<ResolvedMapConfig> maps)
        {
            if (!allMaps)
            {
                return;
            }

            foreach (string id in ListCandidateMapIds(mapleStoryPath, textEncoding, maps))
            {
                maps.Add(new ResolvedMapConfig(
                    id,
                    defaultSampling.Count,
                    defaultSampling.IntervalMs,
                    defaultPostProcessors,
                    MapSelectionSource.AllMaps));
            }
        }

        private List<string> ListCandidateMapIds(
            string mapleStoryPath,
            Encoding textEncoding,
            IEnumerable<ResolvedMapConfig> existingMaps)
        {
            HashSet<string> existingIds = new HashSet<string>(
                existingMaps.Select(map => map.Id),
                StringComparer.Ordinal);
            return _mapCatalog
                .ListMapIds(mapleStoryPath, textEncoding)
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.All(char.IsDigit))
                .Where(id => !existingIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private static int ResolveConcurrency(int? concurrency)
        {
            int resolvedConcurrency = concurrency ?? 1;
            if (resolvedConcurrency <= 0)
            {
                throw new ConfigurationException("concurrency must be greater than 0.");
            }

            return resolvedConcurrency;
        }

        private static GenerationMode ResolveGenerationMode(string rawMode)
        {
            if (!Enum.TryParse(rawMode, true, out GenerationMode generationMode))
            {
                throw new ConfigurationException($"Unsupported mode '{rawMode}'. Use 'character', 'rune', or 'geometry'.");
            }

            return generationMode;
        }

        private static void ValidateModeOutput(GenerationMode generationMode, OutputFormat outputFormat)
        {
            if (generationMode == GenerationMode.Rune && outputFormat != OutputFormat.Coco)
            {
                throw new ConfigurationException("mode 'rune' only supports output.format 'coco'.");
            }
            if (generationMode == GenerationMode.Geometry && outputFormat != OutputFormat.Geometry)
            {
                throw new ConfigurationException("mode 'geometry' only supports output.format 'geometry'.");
            }
            if (generationMode != GenerationMode.Geometry && outputFormat == OutputFormat.Geometry)
            {
                throw new ConfigurationException("output.format 'geometry' requires mode 'geometry'.");
            }
        }

        private static void ValidateModePostProcessors(
            GenerationMode generationMode,
            GeneratorConfig config,
            IReadOnlyList<ResolvedMapConfig> maps)
        {
            if (generationMode != GenerationMode.Rune && generationMode != GenerationMode.Geometry)
            {
                return;
            }

            string modeName = generationMode == GenerationMode.Rune ? "rune" : "geometry";
            if (config.PostProcessors != null && config.PostProcessors.Count > 0)
            {
                throw new ConfigurationException($"mode '{modeName}' does not support postProcessors.");
            }

            if (config.Maps != null)
            {
                for (int i = 0; i < config.Maps.Count; i++)
                {
                    if (config.Maps[i]?.PostProcessors != null && config.Maps[i].PostProcessors.Count > 0)
                    {
                        throw new ConfigurationException($"mode '{modeName}' does not support maps[{i}].postProcessors.");
                    }
                }
            }

            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i].PostProcessors.Count > 0)
                {
                    throw new ConfigurationException($"mode '{modeName}' does not support maps[{i}].postProcessors.");
                }
            }
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
                sampling.Count,
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

            int? count = sampling.Count ?? defaults.Count;
            int? intervalMs = sampling.IntervalMs ?? defaults.IntervalMs;
            return ResolveSampling(count, intervalMs, context);
        }

        private static ResolvedSampling ResolveSampling(int? count, int? intervalMs, string context)
        {
            if (!count.HasValue)
            {
                throw new ConfigurationException($"{context}.count is required.");
            }
            if (count.Value <= 0)
            {
                throw new ConfigurationException($"{context}.count must be greater than 0.");
            }

            int resolvedInterval = intervalMs ?? 0;
            if (resolvedInterval < 0)
            {
                throw new ConfigurationException($"{context}.intervalMs cannot be negative.");
            }

            return new ResolvedSampling(count.Value, resolvedInterval);
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

        private static string ResolveDirectoryPath(string path, string configDirectory, string context)
        {
            return ResolvePath(configDirectory, RequireNonEmpty(path, context));
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
            public ResolvedSampling(int count, int intervalMs)
            {
                Count = count;
                IntervalMs = intervalMs;
            }

            public int Count { get; }

            public int IntervalMs { get; }
        }
    }
}
