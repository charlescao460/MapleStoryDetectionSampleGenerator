using System.Collections.Generic;
using System.Text;

namespace MapleStory.MachineLearningSampleGenerator.Configuration
{
    internal sealed class GeneratorConfig
    {
        public string Mode { get; set; } = string.Empty;

        public string MapleStoryPath { get; set; } = string.Empty;

        public string Encoding { get; set; } = string.Empty;

        public OutputConfig Output { get; set; }

        public RenderConfig Render { get; set; }

        public SamplingConfig Sampling { get; set; }

        public int? Concurrency { get; set; }

        public IList<PostProcessorConfig> PostProcessors { get; set; } = new List<PostProcessorConfig>();

        public IList<MapConfig> Maps { get; set; } = new List<MapConfig>();

        public RandomMapConfig RandomMaps { get; set; }
    }

    internal sealed class RenderConfig
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }

    internal sealed class SamplingConfig
    {
        public int? Count { get; set; }

        public int? IntervalMs { get; set; }
    }

    internal sealed class OutputConfig
    {
        public string Format { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    internal sealed class MapConfig
    {
        public string Id { get; set; } = string.Empty;

        public SamplingConfig Sampling { get; set; }

        public IList<PostProcessorConfig> PostProcessors { get; set; }
    }

    internal sealed class RandomMapConfig
    {
        public int Count { get; set; }

        public int? Seed { get; set; }
    }

    internal abstract class PostProcessorConfig
    {
        protected PostProcessorConfig(string type)
        {
            Type = type;
        }

        public string Type { get; }
    }

    internal sealed class PlayerPostProcessorConfig : PostProcessorConfig
    {
        public PlayerPostProcessorConfig() : base("player")
        {
        }

        public int Count { get; set; } = 3;

        public IList<string> Actions { get; set; } = new List<string> { "stand1" };

        public IList<string> Emotions { get; set; } = new List<string> { "default" };

        public IList<PlayerAvatarConfig> Avatars { get; set; } = new List<PlayerAvatarConfig>();
    }

    internal sealed class PlayerAvatarConfig
    {
        public IList<int> Parts { get; set; } = new List<int>();
    }

    internal sealed class ResolvedRunConfig
    {
        public ResolvedRunConfig(
            string configPath,
            string mapleStoryPath,
            Encoding textEncoding,
            GenerationMode generationMode,
            OutputFormat outputFormat,
            string outputPath,
            string outputName,
            int renderWidth,
            int renderHeight,
            int concurrency,
            IReadOnlyList<ResolvedMapConfig> maps)
        {
            ConfigPath = configPath;
            MapleStoryPath = mapleStoryPath;
            TextEncoding = textEncoding;
            GenerationMode = generationMode;
            OutputFormat = outputFormat;
            OutputPath = outputPath;
            OutputName = outputName;
            RenderWidth = renderWidth;
            RenderHeight = renderHeight;
            Concurrency = concurrency;
            Maps = maps;
        }

        public string ConfigPath { get; }

        public string MapleStoryPath { get; }

        public Encoding TextEncoding { get; }

        public GenerationMode GenerationMode { get; }

        public OutputFormat OutputFormat { get; }

        public string OutputPath { get; }

        public string OutputName { get; }

        public int RenderWidth { get; }

        public int RenderHeight { get; }

        public int Concurrency { get; }

        public IReadOnlyList<ResolvedMapConfig> Maps { get; }
    }

    internal sealed class ResolvedMapConfig
    {
        public ResolvedMapConfig(
            string id,
            int count,
            int intervalMs,
            IReadOnlyList<PostProcessorConfig> postProcessors)
        {
            Id = id;
            Count = count;
            IntervalMs = intervalMs;
            PostProcessors = postProcessors;
        }

        public string Id { get; }

        public int Count { get; }

        public int IntervalMs { get; }

        public IReadOnlyList<PostProcessorConfig> PostProcessors { get; }
    }
}
