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
                new[] { "mapleStoryPath", "encoding", "output", "render", "sampling", "postProcessors", "maps" },
                new[] { "output", "render", "sampling", "maps" });

            return new GeneratorConfig
            {
                MapleStoryPath = ReadOptionalString(root, "mapleStoryPath", "mapleStoryPath"),
                Encoding = ReadOptionalString(root, "encoding", "encoding"),
                Output = ParseOutput(root["output"], "output"),
                Render = ParseRender(root["render"], "render"),
                Sampling = ParseSampling(root["sampling"], "sampling"),
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
                ValidateKeys(processor, processorContext, new[] { "type", "imageDirectory" }, new[] { "type" });

                string type = ReadRequiredString(processor, "type", $"{processorContext}.type").Trim();
                switch (type.ToLowerInvariant())
                {
                    case "player":
                        ValidateKeys(processor, processorContext, new[] { "type", "imageDirectory" }, new[] { "type", "imageDirectory" });
                        processors.Add(new PlayerPostProcessorConfig
                        {
                            ImageDirectory = ReadRequiredString(processor, "imageDirectory", $"{processorContext}.imageDirectory"),
                        });
                        break;
                    default:
                        throw new ConfigurationException($"Unsupported post processor type '{type}' at {processorContext}.type.");
                }
            }

            return processors;
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
    }
}
