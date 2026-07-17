using System;
using System.IO;
using System.Text.Json;
using MapleStory.MachineLearningSampleGenerator;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class MapPackPublisherTests
    {
        [Fact]
        public void Publish_WritesSortedHashPinnedManifestAndPreservesAlignment()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string staging = workspace.CreateDirectory("staging");
            string output = workspace.CreateDirectory("output");
            workspace.CreateFile("output/410000520.alignment.json", "{\"owned_by\":\"hecate\"}");
            WriteMap(workspace, "staging", 410007014, "Second", true);
            WriteMap(workspace, "staging", 410000520, "First", true);

            MapPackPublisher.Publish(staging, output, new[] { 300, 299, 300 }, "2.1.0");

            string manifestPath = Path.Combine(output, MapPackPublisher.ManifestFileName);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = document.RootElement;
            Assert.Equal(MapPackPublisher.SchemaVersion, root.GetProperty("schema_version").GetInt32());
            Assert.Equal("MapleStoryDetectionSampleGenerator", root.GetProperty("producer").GetProperty("name").GetString());
            Assert.Equal("2.1.0", root.GetProperty("producer").GetProperty("version").GetString());
            Assert.Equal(new[] { 299, 300 }, root.GetProperty("source").GetProperty("versions").EnumerateArray().SelectInt32());

            JsonElement.ArrayEnumerator maps = root.GetProperty("maps").EnumerateArray();
            Assert.True(maps.MoveNext());
            Assert.Equal(410000520, maps.Current.GetProperty("map_id").GetInt32());
            Assert.Equal(
                MapPackPublisher.Sha256(Path.Combine(output, "410000520.json")),
                maps.Current.GetProperty("geometry_sha256").GetString());
            Assert.Equal(
                MapPackPublisher.Sha256(Path.Combine(output, "410000520.png")),
                maps.Current.GetProperty("minimap_sha256").GetString());
            Assert.True(maps.MoveNext());
            Assert.Equal(410007014, maps.Current.GetProperty("map_id").GetInt32());
            Assert.False(maps.MoveNext());
            Assert.True(File.Exists(Path.Combine(output, "410000520.alignment.json")));
        }

        [Fact]
        public void Publish_SameInputsProduceIdenticalManifest()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string staging = workspace.CreateDirectory("staging");
            string firstOutput = workspace.CreateDirectory("first");
            string secondOutput = workspace.CreateDirectory("second");
            WriteMap(workspace, "staging", 410000520, "Map", true);

            MapPackPublisher.Publish(staging, firstOutput, new[] { 300 }, "2.1.0");
            MapPackPublisher.Publish(staging, secondOutput, new[] { 300 }, "2.1.0");

            Assert.Equal(
                File.ReadAllBytes(Path.Combine(firstOutput, MapPackPublisher.ManifestFileName)),
                File.ReadAllBytes(Path.Combine(secondOutput, MapPackPublisher.ManifestFileName)));
        }

        [Fact]
        public void Publish_MissingReferencedMinimapDoesNotPublishManifest()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string staging = workspace.CreateDirectory("staging");
            string output = workspace.CreateDirectory("output");
            WriteMap(workspace, "staging", 410000520, "Map", false);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MapPackPublisher.Publish(staging, output, Array.Empty<int>(), "2.1.0"));

            Assert.Contains("missing minimap", exception.Message);
            Assert.False(File.Exists(Path.Combine(output, MapPackPublisher.ManifestFileName)));
        }

        private static void WriteMap(
            TestWorkspace workspace,
            string directory,
            int mapId,
            string mapName,
            bool writeImage)
        {
            string imageName = mapId + ".png";
            string geometry =
                "{\"schema_version\":2,\"map_id\":" + mapId +
                ",\"map_name\":\"" + mapName +
                "\",\"minimap\":{\"image\":\"" + imageName +
                "\"},\"platforms\":[],\"ropes\":[],\"portals\":[]}";
            workspace.CreateFile(Path.Combine(directory, mapId + ".json"), geometry);
            if (writeImage)
            {
                workspace.CreateFile(Path.Combine(directory, imageName), "png-bytes-" + mapId);
            }
        }
    }

    internal static class JsonTestExtensions
    {
        public static int[] SelectInt32(this JsonElement.ArrayEnumerator values)
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (JsonElement value in values)
            {
                result.Add(value.GetInt32());
            }
            return result.ToArray();
        }
    }
}
