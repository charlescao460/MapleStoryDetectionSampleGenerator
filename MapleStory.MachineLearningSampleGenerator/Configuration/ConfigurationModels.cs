using System.Collections.Generic;
using System.Text;

namespace MapleStory.MachineLearningSampleGenerator.Configuration
{
    internal sealed class GeneratorConfig
    {
        public string MapleStoryPath { get; set; } = string.Empty;

        public string Encoding { get; set; } = string.Empty;

        public OutputConfig Output { get; set; }

        public RenderConfig Render { get; set; }

        public SamplingConfig Sampling { get; set; }

        public IList<PostProcessorConfig> PostProcessors { get; set; } = new List<PostProcessorConfig>();

        public IList<MapConfig> Maps { get; set; } = new List<MapConfig>();
    }

    internal sealed class RenderConfig
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }

    internal sealed class SamplingConfig
    {
        public int? XStep { get; set; }

        public int? YStep { get; set; }

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
            OutputFormat outputFormat,
            string outputPath,
            string outputName,
            int renderWidth,
            int renderHeight,
            IReadOnlyList<ResolvedMapConfig> maps)
        {
            ConfigPath = configPath;
            MapleStoryPath = mapleStoryPath;
            TextEncoding = textEncoding;
            OutputFormat = outputFormat;
            OutputPath = outputPath;
            OutputName = outputName;
            RenderWidth = renderWidth;
            RenderHeight = renderHeight;
            Maps = maps;
        }

        public string ConfigPath { get; }

        public string MapleStoryPath { get; }

        public Encoding TextEncoding { get; }

        public OutputFormat OutputFormat { get; }

        public string OutputPath { get; }

        public string OutputName { get; }

        public int RenderWidth { get; }

        public int RenderHeight { get; }

        public IReadOnlyList<ResolvedMapConfig> Maps { get; }
    }

    internal sealed class ResolvedMapConfig
    {
        public ResolvedMapConfig(
            string id,
            int xStep,
            int yStep,
            int intervalMs,
            IReadOnlyList<PostProcessorConfig> postProcessors)
        {
            Id = id;
            XStep = xStep;
            YStep = yStep;
            IntervalMs = intervalMs;
            PostProcessors = postProcessors;
        }

        public string Id { get; }

        public int XStep { get; }

        public int YStep { get; }

        public int IntervalMs { get; }

        public IReadOnlyList<PostProcessorConfig> PostProcessors { get; }
    }
}
