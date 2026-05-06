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
mode: character
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
            Assert.Equal(GenerationMode.Character, config.GenerationMode);
            Assert.Equal("dataset", config.OutputName);
            Assert.Equal(1, config.Concurrency);
            Assert.Single(config.Maps);
            Assert.Equal("993134200", config.Maps[0].Id);
            Assert.Empty(config.Maps[0].PostProcessors);
        }

        [Fact]
        public void LoadResolved_ConcurrencyConfigured_Succeeds()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: character
mapleStoryPath: ./maple
concurrency: 6
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

            Assert.Equal(6, config.Concurrency);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void LoadResolved_InvalidConcurrency_Throws(int concurrency)
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, $@"
mode: character
mapleStoryPath: ./maple
concurrency: {concurrency}
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
        public void LoadResolved_MissingMode_Throws()
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
maps:
  - id: 993134200
"));
        }

        [Fact]
        public void LoadResolved_InvalidMode_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mode: mystery
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
"));
        }

        [Fact]
        public void LoadResolved_RuneModeWithCoco_Succeeds()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: rune
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

            Assert.Equal(GenerationMode.Rune, config.GenerationMode);
            Assert.Equal(OutputFormat.Coco, config.OutputFormat);
            Assert.Empty(config.Maps[0].PostProcessors);
        }

        [Theory]
        [InlineData("darknet")]
        [InlineData("tfRecord")]
        public void LoadResolved_RuneModeWithNonCocoOutput_Throws(string format)
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, $@"
mode: rune
mapleStoryPath: ./maple
output:
  format: {format}
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
        public void LoadResolved_RuneModeWithPostProcessors_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mode: rune
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
    avatars:
      - parts: [2000, 12003]
maps:
  - id: 993134200
"));
        }

        [Fact]
        public void LoadResolved_RuneModeWithMapPostProcessors_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mode: rune
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
    postProcessors:
      - type: player
        avatars:
          - parts: [2000, 12003]
"));
        }

        [Fact]
        public void LoadResolved_RootSamplingDefaultsAppliedToMaps()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: character
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
mode: character
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

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: character
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
    count: 2
    actions: [stand1, jump]
    emotions: [default, smile]
    avatars:
      - parts: [2000, 12003, 20000, 30000]
maps:
  - id: 993134200
  - id: 450007010
    postProcessors:
      - type: player
        count: 1
        avatars:
          - parts: [2000, 12003, 20000, 30000, 1040036]
");

            PlayerPostProcessorConfig inherited = Assert.IsType<PlayerPostProcessorConfig>(Assert.Single(config.Maps[0].PostProcessors));
            PlayerPostProcessorConfig replaced = Assert.IsType<PlayerPostProcessorConfig>(Assert.Single(config.Maps[1].PostProcessors));
            Assert.Equal(2, inherited.Count);
            Assert.Equal(new[] { "stand1", "jump" }, inherited.Actions);
            Assert.Equal(new[] { "default", "smile" }, inherited.Emotions);
            Assert.Equal(new[] { 2000, 12003, 20000, 30000 }, Assert.Single(inherited.Avatars).Parts);
            Assert.Equal(1, replaced.Count);
            Assert.Equal(new[] { "stand1" }, replaced.Actions);
            Assert.Equal(new[] { "default" }, replaced.Emotions);
            Assert.Equal(new[] { 2000, 12003, 20000, 30000, 1040036 }, Assert.Single(replaced.Avatars).Parts);
        }

        [Fact]
        public void LoadResolved_EmptyMapPostProcessors_DisablesInheritedProcessors()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: character
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
    avatars:
      - parts: [2000, 12003, 20000, 30000]
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
            CreateMapleStoryTree(workspace, "shared/maple");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: character
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
    avatars:
      - parts: [2000, 12003, 20000, 30000]
maps:
  - id: 993134200
", Path.Combine(configDirectory, "generator.yml"));

            Assert.Equal(Path.GetFullPath(Path.Combine(workspace.RootPath, "shared/maple")), config.MapleStoryPath);
            Assert.Equal(Path.GetFullPath(outputDirectory), config.OutputPath);
            PlayerPostProcessorConfig processor = Assert.IsType<PlayerPostProcessorConfig>(Assert.Single(config.Maps[0].PostProcessors));
            Assert.Equal(new[] { 2000, 12003, 20000, 30000 }, Assert.Single(processor.Avatars).Parts);
        }

        [Fact]
        public void LoadResolved_InvalidProcessorType_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mode: character
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
        public void LoadResolved_PlayerImageDirectory_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ConfigurationException exception = Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mode: character
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
    imageDirectory: ./players
maps:
  - id: 993134200
"));
            Assert.Contains("imageDirectory is no longer supported", exception.Message);
        }

        [Theory]
        [InlineData("missing avatars", @"
postProcessors:
  - type: player
")]
        [InlineData("empty avatars", @"
postProcessors:
  - type: player
    avatars: []
")]
        [InlineData("empty parts", @"
postProcessors:
  - type: player
    avatars:
      - parts: []
")]
        [InlineData("non-integer parts", @"
postProcessors:
  - type: player
    avatars:
      - parts: [2000, abc]
")]
        [InlineData("invalid count", @"
postProcessors:
  - type: player
    count: 0
    avatars:
      - parts: [2000, 12003]
")]
        [InlineData("empty actions", @"
postProcessors:
  - type: player
    actions: []
    avatars:
      - parts: [2000, 12003]
")]
        [InlineData("empty emotions", @"
postProcessors:
  - type: player
    emotions: []
    avatars:
      - parts: [2000, 12003]
")]
        public void LoadResolved_InvalidPlayerProcessor_Throws(string caseName, string postProcessorYaml)
        {
            _ = caseName;
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            string yaml = @"
mode: character
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
" + postProcessorYaml + @"
maps:
  - id: 993134200
";

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, yaml));
        }

        [Fact]
        public void LoadResolved_UnknownKey_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mode: character
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
mode: character
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
mode: character
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


