using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace MapleStory.MachineLearningSampleGenerator.Configuration
{
    internal sealed class ConfigurationLoader
    {
        public GeneratorConfig Load(string configPath)
        {
            string fullPath = Path.GetFullPath(configPath ?? string.Empty);
            if (!File.Exists(fullPath))
            {
                throw new ConfigurationException($"Configuration file '{fullPath}' does not exist.");
            }

            using StreamReader reader = new StreamReader(fullPath);
            YamlStream stream = new YamlStream();
            stream.Load(reader);

            if (stream.Documents.Count != 1)
            {
                throw new ConfigurationException("Configuration file must contain exactly one YAML document.");
            }

            if (stream.Documents[0].RootNode is not YamlMappingNode rootNode)
            {
                throw new ConfigurationException("Configuration root must be a mapping.");
            }

            Dictionary<string, YamlNode> root = ToDictionary(rootNode, "root");
            ValidateKeys(
                root,
                "root",
                new[] { "mode", "mapleStoryPath", "encoding", "output", "render", "sampling", "concurrency", "postProcessors", "maps" },
                new[] { "mode", "output", "render", "sampling", "maps" });

            return new GeneratorConfig
            {
                Mode = ReadRequiredString(root, "mode", "mode"),
                MapleStoryPath = ReadOptionalString(root, "mapleStoryPath", "mapleStoryPath"),
                Encoding = ReadOptionalString(root, "encoding", "encoding"),
                Output = ParseOutput(root["output"], "output"),
                Render = ParseRender(root["render"], "render"),
                Sampling = ParseSampling(root["sampling"], "sampling"),
                Concurrency = ReadOptionalInt(root, "concurrency", "concurrency"),
                PostProcessors = root.TryGetValue("postProcessors", out YamlNode processorsNode)
                    ? ParsePostProcessors(processorsNode, "postProcessors")
                    : new List<PostProcessorConfig>(),
                Maps = ParseMaps(root["maps"], "maps"),
            };
        }

        private static OutputConfig ParseOutput(YamlNode node, string context)
        {
            Dictionary<string, YamlNode> output = ToDictionary(RequireMapping(node, context), context);
            ValidateKeys(output, context, new[] { "format", "path", "name" }, new[] { "format", "path", "name" });

            return new OutputConfig
            {
                Format = ReadRequiredString(output, "format", $"{context}.format"),
                Path = ReadRequiredString(output, "path", $"{context}.path"),
                Name = ReadRequiredString(output, "name", $"{context}.name"),
            };
        }

        private static RenderConfig ParseRender(YamlNode node, string context)
        {
            Dictionary<string, YamlNode> render = ToDictionary(RequireMapping(node, context), context);
            ValidateKeys(render, context, new[] { "width", "height" }, new[] { "width", "height" });

            return new RenderConfig
            {
                Width = ReadRequiredInt(render, "width", $"{context}.width"),
                Height = ReadRequiredInt(render, "height", $"{context}.height"),
            };
        }

        private static SamplingConfig ParseSampling(YamlNode node, string context)
        {
            Dictionary<string, YamlNode> sampling = ToDictionary(RequireMapping(node, context), context);
            ValidateKeys(sampling, context, new[] { "xStep", "yStep", "intervalMs" }, Array.Empty<string>());

            return new SamplingConfig
            {
                XStep = ReadOptionalInt(sampling, "xStep", $"{context}.xStep"),
                YStep = ReadOptionalInt(sampling, "yStep", $"{context}.yStep"),
                IntervalMs = ReadOptionalInt(sampling, "intervalMs", $"{context}.intervalMs"),
            };
        }

        private static IList<MapConfig> ParseMaps(YamlNode node, string context)
        {
            YamlSequenceNode sequence = RequireSequence(node, context);
            List<MapConfig> maps = new List<MapConfig>();
            for (int i = 0; i < sequence.Children.Count; i++)
            {
                string mapContext = $"{context}[{i}]";
                Dictionary<string, YamlNode> map = ToDictionary(RequireMapping(sequence.Children[i], mapContext), mapContext);
                ValidateKeys(map, mapContext, new[] { "id", "sampling", "postProcessors" }, new[] { "id" });

                maps.Add(new MapConfig
                {
                    Id = ReadRequiredString(map, "id", $"{mapContext}.id"),
                    Sampling = map.TryGetValue("sampling", out YamlNode samplingNode)
                        ? ParseSampling(samplingNode, $"{mapContext}.sampling")
                        : null,
                    PostProcessors = map.TryGetValue("postProcessors", out YamlNode processorsNode)
                        ? ParsePostProcessors(processorsNode, $"{mapContext}.postProcessors")
                        : null,
                });
            }

            return maps;
        }

        private static IList<PostProcessorConfig> ParsePostProcessors(YamlNode node, string context)
        {
            YamlSequenceNode sequence = RequireSequence(node, context);
            List<PostProcessorConfig> processors = new List<PostProcessorConfig>();

            for (int i = 0; i < sequence.Children.Count; i++)
            {
                string processorContext = $"{context}[{i}]";
                Dictionary<string, YamlNode> processor = ToDictionary(RequireMapping(sequence.Children[i], processorContext), processorContext);
                ValidateKeys(processor, processorContext, new[] { "type", "count", "actions", "emotions", "avatars", "imageDirectory" }, new[] { "type" });

                string type = ReadRequiredString(processor, "type", $"{processorContext}.type").Trim();
                switch (type.ToLowerInvariant())
                {
                    case "player":
                        if (processor.ContainsKey("imageDirectory"))
                        {
                            throw new ConfigurationException(
                                $"{processorContext}.imageDirectory is no longer supported. Configure generated avatars with {processorContext}.avatars[].parts.");
                        }

                        ValidateKeys(processor, processorContext, new[] { "type", "count", "actions", "emotions", "avatars" }, new[] { "type", "avatars" });
                        processors.Add(new PlayerPostProcessorConfig
                        {
                            Count = processor.TryGetValue("count", out YamlNode countNode)
                                ? ParsePositiveInt(countNode, $"{processorContext}.count")
                                : 3,
                            Actions = processor.TryGetValue("actions", out YamlNode actionsNode)
                                ? ParseStringSequence(actionsNode, $"{processorContext}.actions")
                                : new List<string> { "stand1" },
                            Emotions = processor.TryGetValue("emotions", out YamlNode emotionsNode)
                                ? ParseStringSequence(emotionsNode, $"{processorContext}.emotions")
                                : new List<string> { "default" },
                            Avatars = ParsePlayerAvatars(processor["avatars"], $"{processorContext}.avatars"),
                        });
                        break;
                    default:
                        throw new ConfigurationException($"Unsupported post processor type '{type}' at {processorContext}.type.");
                }
            }

            return processors;
        }

        private static IList<PlayerAvatarConfig> ParsePlayerAvatars(YamlNode node, string context)
        {
            YamlSequenceNode sequence = RequireSequence(node, context);
            if (sequence.Children.Count == 0)
            {
                throw new ConfigurationException($"{context} cannot be empty.");
            }

            List<PlayerAvatarConfig> avatars = new List<PlayerAvatarConfig>(sequence.Children.Count);
            for (int i = 0; i < sequence.Children.Count; i++)
            {
                string avatarContext = $"{context}[{i}]";
                Dictionary<string, YamlNode> avatar = ToDictionary(RequireMapping(sequence.Children[i], avatarContext), avatarContext);
                ValidateKeys(avatar, avatarContext, new[] { "parts" }, new[] { "parts" });
                avatars.Add(new PlayerAvatarConfig
                {
                    Parts = ParseNonNegativeIntSequence(avatar["parts"], $"{avatarContext}.parts"),
                });
            }

            return avatars;
        }

        private static Dictionary<string, YamlNode> ToDictionary(YamlMappingNode mapping, string context)
        {
            Dictionary<string, YamlNode> result = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
            foreach (KeyValuePair<YamlNode, YamlNode> pair in mapping.Children)
            {
                if (pair.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
                {
                    throw new ConfigurationException($"All keys in {context} must be non-empty scalars.");
                }
                if (!result.TryAdd(keyNode.Value, pair.Value))
                {
                    throw new ConfigurationException($"Duplicate key '{keyNode.Value}' in {context}.");
                }
            }
            return result;
        }

        private static void ValidateKeys(
            IDictionary<string, YamlNode> values,
            string context,
            IReadOnlyCollection<string> allowedKeys,
            IReadOnlyCollection<string> requiredKeys)
        {
            foreach (string key in values.Keys)
            {
                if (!allowedKeys.Contains(key))
                {
                    throw new ConfigurationException($"Unknown configuration key '{key}' in {context}.");
                }
            }

            foreach (string key in requiredKeys)
            {
                if (!values.ContainsKey(key))
                {
                    throw new ConfigurationException($"Missing required configuration key '{key}' in {context}.");
                }
            }
        }

        private static YamlMappingNode RequireMapping(YamlNode node, string context)
        {
            if (node is not YamlMappingNode mapping)
            {
                throw new ConfigurationException($"{context} must be a mapping.");
            }
            return mapping;
        }

        private static YamlSequenceNode RequireSequence(YamlNode node, string context)
        {
            if (node is not YamlSequenceNode sequence)
            {
                throw new ConfigurationException($"{context} must be a sequence.");
            }
            return sequence;
        }

        private static string ReadRequiredString(IDictionary<string, YamlNode> values, string key, string context)
        {
            return ReadScalar(values[key], context, allowEmpty: false);
        }

        private static string ReadOptionalString(IDictionary<string, YamlNode> values, string key, string context)
        {
            return values.TryGetValue(key, out YamlNode node)
                ? ReadScalar(node, context, allowEmpty: true)
                : string.Empty;
        }

        private static int ReadRequiredInt(IDictionary<string, YamlNode> values, string key, string context)
        {
            return ParseInt(values[key], context);
        }

        private static int? ReadOptionalInt(IDictionary<string, YamlNode> values, string key, string context)
        {
            return values.TryGetValue(key, out YamlNode node)
                ? ParseInt(node, context)
                : null;
        }

        private static string ReadScalar(YamlNode node, string context, bool allowEmpty)
        {
            if (node is not YamlScalarNode scalarNode || scalarNode.Value == null)
            {
                throw new ConfigurationException($"{context} must be a scalar value.");
            }

            if (!allowEmpty && string.IsNullOrWhiteSpace(scalarNode.Value))
            {
                throw new ConfigurationException($"{context} cannot be empty.");
            }

            return scalarNode.Value;
        }

        private static int ParseInt(YamlNode node, string context)
        {
            string rawValue = ReadScalar(node, context, allowEmpty: false);
            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new ConfigurationException($"{context} must be an integer.");
            }
            return value;
        }

        private static int ParsePositiveInt(YamlNode node, string context)
        {
            int value = ParseInt(node, context);
            if (value <= 0)
            {
                throw new ConfigurationException($"{context} must be greater than 0.");
            }

            return value;
        }

        private static IList<int> ParseNonNegativeIntSequence(YamlNode node, string context)
        {
            YamlSequenceNode sequence = RequireSequence(node, context);
            if (sequence.Children.Count == 0)
            {
                throw new ConfigurationException($"{context} cannot be empty.");
            }

            List<int> values = new List<int>(sequence.Children.Count);
            for (int i = 0; i < sequence.Children.Count; i++)
            {
                int value = ParseInt(sequence.Children[i], $"{context}[{i}]");
                if (value < 0)
                {
                    throw new ConfigurationException($"{context}[{i}] cannot be negative.");
                }

                values.Add(value);
            }

            return values;
        }

        private static IList<string> ParseStringSequence(YamlNode node, string context)
        {
            YamlSequenceNode sequence = RequireSequence(node, context);
            if (sequence.Children.Count == 0)
            {
                throw new ConfigurationException($"{context} cannot be empty.");
            }

            List<string> values = new List<string>(sequence.Children.Count);
            for (int i = 0; i < sequence.Children.Count; i++)
            {
                values.Add(ReadScalar(sequence.Children[i], $"{context}[{i}]", allowEmpty: false));
            }

            return values;
        }
    }
}
