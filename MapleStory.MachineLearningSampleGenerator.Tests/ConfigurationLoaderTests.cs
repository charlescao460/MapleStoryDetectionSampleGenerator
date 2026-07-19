using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
  count: 5
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
  count: 5
maps:
  - id: 993134200
");

            Assert.Equal(6, config.Concurrency);
        }

        [Fact]
        public void LoadResolved_GeometryModeDoesNotRequireRenderSamplingOrOutputName()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: geometry
mapleStoryPath: ./maple
output:
  format: geometry
  path: ./output
maps:
  - id: 410000520
");

            Assert.Equal(GenerationMode.Geometry, config.GenerationMode);
            Assert.Equal(OutputFormat.Geometry, config.OutputFormat);
            Assert.Equal("geometry", config.OutputName);
            Assert.Equal(0, config.RenderWidth);
            Assert.Equal(0, config.RenderHeight);
            Assert.False(config.ExportAllMaps);
            Assert.Equal(1, config.Maps[0].Count);
        }

        [Fact]
        public void LoadResolved_GeometryAllMaps_DefersCatalogSelectionToExporter()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");
            FakeMapCatalog mapCatalog = new FakeMapCatalog("100000000");

            ResolvedRunConfig config = LoadResolved(workspace, @"
mode: geometry
mapleStoryPath: ./maple
output:
  format: geometry
  path: ./output
maps:
  allMaps: true
", mapCatalog: mapCatalog);

            Assert.True(config.ExportAllMaps);
            Assert.Empty(config.Maps);
            Assert.Equal(0, mapCatalog.CallCount);
        }

        [Fact]
        public void LoadResolved_GeometryFormatWithCharacterModeThrows()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            ConfigurationException exception = Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, @"
mode: character
mapleStoryPath: ./maple
output:
  format: geometry
  path: ./output
  name: dataset
render:
  width: 1366
  height: 768
sampling:
  count: 5
maps:
  - id: 410000520
"));

            Assert.Contains("requires mode 'geometry'", exception.Message);
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
maps:
  - id: 993134200
    postProcessors:
      - type: player
        avatars:
          - parts: [2000, 12003]
"));
        }

        [Fact]
        public void LoadResolved_RuneModeWithRandomMaps_SelectsRequestedCount()
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
  count: 5
  intervalMs: 7
maps:
  random:
    count: 2
    seed: 123
", mapCatalog: new FakeMapCatalog("100000000", "200000000", "300000000"));

            Assert.Equal(2, config.Maps.Count);
            Assert.All(config.Maps, map =>
            {
                Assert.Equal(5, map.Count);
                Assert.Equal(7, map.IntervalMs);
                Assert.Empty(map.PostProcessors);
            });
        }

        [Fact]
        public void LoadResolved_RuneModeWithRandomMapsAndSeed_IsStable()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");
            const string yaml = @"
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
  count: 5
maps:
  random:
    count: 3
    seed: 12345
";
            FakeMapCatalog mapCatalog = new FakeMapCatalog("100000000", "200000000", "300000000", "400000000", "500000000");

            string[] first = LoadResolved(workspace, yaml, mapCatalog: mapCatalog).Maps.Select(map => map.Id).ToArray();
            string[] second = LoadResolved(workspace, yaml, mapCatalog: mapCatalog).Maps.Select(map => map.Id).ToArray();

            Assert.Equal(first, second);
        }

        [Fact]
        public void LoadResolved_RuneModeWithEntriesAndRandomMaps_ExcludesExplicitIds()
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
  count: 5
maps:
  entries:
    - id: 100000000
  random:
    count: 2
    seed: 10
", mapCatalog: new FakeMapCatalog("100000000", "200000000", "300000000", "400000000"));

            Assert.Equal("100000000", config.Maps[0].Id);
            Assert.Equal(3, config.Maps.Count);
            Assert.Equal(3, config.Maps.Select(map => map.Id).Distinct().Count());
        }

        [Fact]
        public void LoadResolved_AllMaps_SelectsEveryCatalogMapInStableOrder()
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
  count: 1
  intervalMs: 7
maps:
  allMaps: true
", mapCatalog: new FakeMapCatalog("300000000", "100000000", "200000000"));

            Assert.Equal(new[] { "100000000", "200000000", "300000000" }, config.Maps.Select(map => map.Id));
            Assert.All(config.Maps, map =>
            {
                Assert.Equal(1, map.Count);
                Assert.Equal(7, map.IntervalMs);
            });
        }

        [Fact]
        public void LoadResolved_EntriesAndAllMaps_ExcludesExplicitIds()
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
  count: 1
maps:
  entries:
    - id: 200000000
  allMaps: true
", mapCatalog: new FakeMapCatalog("100000000", "200000000", "300000000"));

            Assert.Equal(new[] { "200000000", "100000000", "300000000" }, config.Maps.Select(map => map.Id));
            Assert.Equal(3, config.Maps.Select(map => map.Id).Distinct().Count());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void LoadResolved_RandomMapCountMustBePositive_Throws(int count)
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);
            workspace.CreateDirectory("output");

            Assert.Throws<ConfigurationException>(() => LoadResolved(workspace, $@"
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
  count: 5
maps:
  random:
    count: {count}
"));
        }

        [Fact]
        public void LoadResolved_RandomMapCountGreaterThanAvailable_Throws()
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
  count: 5
maps:
  random:
    count: 3
", mapCatalog: new FakeMapCatalog("100000000", "200000000")));
        }

        [Fact]
        public void LoadResolved_RandomMapsWithAllMaps_Throws()
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
  count: 1
maps:
  allMaps: true
  random:
    count: 1
", mapCatalog: new FakeMapCatalog("100000000", "200000000")));
        }

        [Fact]
        public void LoadResolved_CharacterModeWithRandomMaps_Throws()
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
  count: 5
maps:
  random:
    count: 1
", mapCatalog: new FakeMapCatalog("100000000")));
        }

        [Fact]
        public void LoadResolved_UnknownMapsKey_Throws()
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
  count: 5
maps:
  unexpected: true
  random:
    count: 1
"));
        }

        [Fact]
        public void LoadResolved_UnknownRandomMapsKey_Throws()
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
  count: 5
maps:
  random:
    count: 1
    unexpected: true
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
  count: 5
  intervalMs: 7
maps:
  - id: 993134200
  - id: 450007010
");

            Assert.Equal(5, config.Maps[0].Count);
            Assert.Equal(7, config.Maps[0].IntervalMs);
            Assert.Equal(5, config.Maps[1].Count);
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
  count: 5
  intervalMs: 7
maps:
  - id: 993134200
    sampling:
      count: 10
");

            Assert.Equal(10, config.Maps[0].Count);
            Assert.Equal(7, config.Maps[0].IntervalMs);
        }

        [Fact]
        public void LoadResolved_LegacyStepSamplingKeys_Throws()
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
  xStep: 500
  yStep: 500
maps:
  - id: 993134200
"));
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
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
  count: 5
maps:
  - id: 993134200
"));
        }

        [Fact]
        public void LoadResolved_MissingOutputPath_Succeeds()
        {
            using TestWorkspace workspace = new TestWorkspace();
            CreateMapleStoryTree(workspace);

            ResolvedRunConfig config = LoadResolved(workspace, @"
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
  count: 5
maps:
  - id: 993134200
");

            string expectedPath = Path.Combine(workspace.RootPath, "missing-output");
            Assert.Equal(Path.GetFullPath(expectedPath), config.OutputPath);
            Assert.False(Directory.Exists(config.OutputPath));
        }

        private static ResolvedRunConfig LoadResolved(
            TestWorkspace workspace,
            string yaml,
            string configPath = null,
            IMapCatalog mapCatalog = null)
        {
            string resolvedConfigPath = configPath ?? Path.Combine(workspace.RootPath, "generator.yml");
            string directory = Path.GetDirectoryName(resolvedConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedConfigPath, yaml.TrimStart());
            ConfigurationLoader loader = new ConfigurationLoader();
            ConfigurationResolver resolver = mapCatalog == null
                ? new ConfigurationResolver()
                : new ConfigurationResolver(mapCatalog);
            GeneratorConfig config = loader.Load(resolvedConfigPath);
            return resolver.Resolve(config, resolvedConfigPath);
        }

        private static void CreateMapleStoryTree(TestWorkspace workspace, string relativePath = "maple")
        {
            workspace.CreateDirectory(Path.Combine(relativePath, "Data", "Base"));
            workspace.CreateFile(Path.Combine(relativePath, "Data", "Base", "Base.wz"));
        }

        private sealed class FakeMapCatalog : IMapCatalog
        {
            private readonly IReadOnlyList<string> _mapIds;

            public FakeMapCatalog(params string[] mapIds)
            {
                _mapIds = mapIds;
            }

            public int CallCount { get; private set; }

            public IReadOnlyList<string> ListMapIds(string mapleStoryPath, Encoding encoding)
            {
                _ = mapleStoryPath;
                _ = encoding;
                CallCount++;
                return _mapIds;
            }
        }
    }
}


