using System;
using System.IO;
using MapleStory.MachineLearningSampleGenerator.Configuration;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class ConfigurationLoaderTests
    {
        [Fact]
        public void LoadResolved_ValidMinimalYaml_Succeeds()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: coco
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
maps:
  - id: 993134200
");

            Assert.Equal(OutputFormat.Coco, config.OutputFormat);
            Assert.Equal("dataset", config.OutputName);
            Assert.Single(config.Maps);
            Assert.Equal("993134200", config.Maps[0].Id);
            Assert.Empty(config.Maps[0].PostProcessors);
        }

        [Fact]
        public void LoadResolved_RootSamplingDefaultsAppliedToMaps()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: coco
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
  intervalMs: 7
maps:
  - id: 993134200
  - id: 450007010
");

            Assert.Equal(5, config.Maps[0].XStep);
            Assert.Equal(6, config.Maps[0].YStep);
            Assert.Equal(7, config.Maps[0].IntervalMs);
            Assert.Equal(5, config.Maps[1].XStep);
            Assert.Equal(6, config.Maps[1].YStep);
            Assert.Equal(7, config.Maps[1].IntervalMs);
        }

        [Fact]
        public void LoadResolved_MapSamplingOverride_MergesWithDefaults()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: coco
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
  intervalMs: 7
maps:
  - id: 993134200
    sampling:
      xStep: 10
");

            Assert.Equal(10, config.Maps[0].XStep);
            Assert.Equal(6, config.Maps[0].YStep);
            Assert.Equal(7, config.Maps[0].IntervalMs);
        }

        [Fact]
        public void LoadResolved_MapPostProcessorsReplaceRootPipeline()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");
            string defaultPlayers = workspace.CreateDirectory("players/default");
            string alternatePlayers = workspace.CreateDirectory("players/alternate");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: coco
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
postProcessors:
  - type: player
    imageDirectory: ./players/default
maps:
  - id: 993134200
  - id: 450007010
    postProcessors:
      - type: player
        imageDirectory: ./players/alternate
");

            PlayerPostProcessorConfig inherited = Assert.IsType<PlayerPostProcessorConfig>(Assert.Single(config.Maps[0].PostProcessors));
            PlayerPostProcessorConfig replaced = Assert.IsType<PlayerPostProcessorConfig>(Assert.Single(config.Maps[1].PostProcessors));
            Assert.Equal(Path.GetFullPath(defaultPlayers), inherited.ImageDirectory);
            Assert.Equal(Path.GetFullPath(alternatePlayers), replaced.ImageDirectory);
        }

        [Fact]
        public void LoadResolved_EmptyMapPostProcessors_DisablesInheritedProcessors()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");
            workspace.CreateDirectory("players/default");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: coco
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
postProcessors:
  - type: player
    imageDirectory: ./players/default
maps:
  - id: 993134200
    postProcessors: []
");

            Assert.Empty(config.Maps[0].PostProcessors);
        }

        [Fact]
        public void LoadResolved_RelativePathsResolveFromConfigDirectory()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string configDirectory = workspace.CreateDirectory("configs");
            string outputDirectory = workspace.CreateDirectory("shared/output");
            string playerDirectory = workspace.CreateDirectory("shared/players/default");
            CreateMapleStoryTree(workspace, "shared/maple");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mapleStoryPath: ../shared/maple
output:
  format: coco
  path: ../shared/output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
postProcessors:
  - type: player
    imageDirectory: ../shared/players/default
maps:
  - id: 993134200
", Path.Combine(configDirectory, "generator.yml"));

            Assert.Equal(Path.GetFullPath(Path.Combine(workspace.RootPath, "shared/maple")), config.MapleStoryPath);
            Assert.Equal(Path.GetFullPath(outputDirectory), config.OutputPath);
            PlayerPostProcessorConfig processor = Assert.IsType<PlayerPostProcessorConfig>(Assert.Single(config.Maps[0].PostProcessors));
            Assert.Equal(Path.GetFullPath(playerDirectory), processor.ImageDirectory);
        }

        [Fact]
        public void LoadResolved_InvalidProcessorType_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: coco
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
postProcessors:
  - type: mystery
maps:
  - id: 993134200
"));
        }

        [Fact]
        public void LoadResolved_UnknownKey_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mapleStoryPath: ./maple
unexpected: true
output:
  format: coco
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
maps:
  - id: 993134200
"));
        }

        [Fact]
        public void LoadResolved_InvalidOutputFormat_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: madeup
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
maps:
  - id: 993134200
"));
        }

        [Fact]
        public void LoadResolved_MissingOutputPath_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mapleStoryPath: ./maple
output:
  format: coco
  path: ./missing-output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  xStep: 5
  yStep: 6
maps:
  - id: 993134200
"));
        }

        private static ResolvedRunConfig LoadResolved(TestWorkspace workspace, string yaml, string configPath = null)
        {
            string resolvedConfigPath = configPath ?? Path.Combine(workspace.RootPath, "generator.yml");
            string directory = Path.GetDirectoryName(resolvedConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedConfigPath, yaml.TrimStart());
            ConfigurationLoader loader = new ConfigurationLoader();
            ConfigurationResolver resolver = new ConfigurationResolver();
            GeneratorConfig config = loader.Load(resolvedConfigPath);
            return resolver.Resolve(config, resolvedConfigPath);
        }

        private static void CreateMapleStoryTree(TestWorkspace workspace, string relativePath = "maple")
        {
            workspace.CreateDirectory(Path.Combine(relativePath, "Data", "Base"));
            workspace.CreateFile(Path.Combine(relativePath, "Data", "Base", "Base.wz"));
        }
    }
}
