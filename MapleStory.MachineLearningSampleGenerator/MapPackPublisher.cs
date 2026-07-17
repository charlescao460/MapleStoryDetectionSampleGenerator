using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class MapPackPublisher
    {
        internal const int SchemaVersion = 1;
        internal const string ManifestFileName = "map-pack.json";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static void Publish(
            string stagingDirectory,
            string outputDirectory,
            IReadOnlyList<int> sourceVersions,
            string producerVersion)
        {
            if (string.IsNullOrWhiteSpace(stagingDirectory))
            {
                throw new ArgumentException("Staging directory cannot be empty.", nameof(stagingDirectory));
            }
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
            }

            List<MapPackEntry> maps = Directory.EnumerateFiles(stagingDirectory, "*.json")
                .Select(CreateEntry)
                .OrderBy(entry => entry.MapId)
                .ToList();
            if (maps.Count == 0)
            {
                throw new InvalidOperationException("A map pack must contain at least one geometry file.");
            }
            if (maps.Select(entry => entry.MapId).Distinct().Count() != maps.Count)
            {
                throw new InvalidOperationException("A map pack cannot contain duplicate map ids.");
            }

            MapPackManifest manifest = new MapPackManifest
            {
                SchemaVersion = SchemaVersion,
                Producer = new ProducerPayload
                {
                    Name = "MapleStoryDetectionSampleGenerator",
                    Version = producerVersion ?? string.Empty,
                },
                Source = new SourcePayload
                {
                    Kind = "wz",
                    Versions = (sourceVersions ?? Array.Empty<int>())
                        .Where(version => version > 0)
                        .Distinct()
                        .OrderBy(version => version)
                        .ToList(),
                },
                Maps = maps,
            };

            Directory.CreateDirectory(outputDirectory);
            foreach (MapPackEntry map in maps)
            {
                PublishFile(stagingDirectory, outputDirectory, map.GeometryFile);
                if (map.MinimapFile != null)
                {
                    PublishFile(stagingDirectory, outputDirectory, map.MinimapFile);
                }
            }

            string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions) + "\n";
            string pendingManifest = Path.Combine(outputDirectory, ManifestFileName + ".pending");
            File.WriteAllText(pendingManifest, manifestJson, new UTF8Encoding(false));
            File.Move(pendingManifest, Path.Combine(outputDirectory, ManifestFileName), true);
        }

        internal static string Sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static MapPackEntry CreateEntry(string geometryPath)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(geometryPath));
            JsonElement root = document.RootElement;
            int mapId = root.GetProperty("map_id").GetInt32();
            string expectedStem = mapId.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(Path.GetFileNameWithoutExtension(geometryPath), expectedStem, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Geometry file '{geometryPath}' does not match map id {mapId}.");
            }

            string minimapFile = null;
            string minimapHash = null;
            if (root.TryGetProperty("minimap", out JsonElement minimap) &&
                minimap.TryGetProperty("image", out JsonElement image) &&
                image.ValueKind == JsonValueKind.String)
            {
                minimapFile = image.GetString();
                ValidatePackFileName(minimapFile, "minimap image");
                string minimapPath = Path.Combine(Path.GetDirectoryName(geometryPath), minimapFile);
                if (!File.Exists(minimapPath))
                {
                    throw new InvalidOperationException($"Geometry file '{geometryPath}' references missing minimap '{minimapFile}'.");
                }
                minimapHash = Sha256(minimapPath);
            }

            string geometryFile = Path.GetFileName(geometryPath);
            return new MapPackEntry
            {
                MapId = mapId,
                MapName = root.TryGetProperty("map_name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString()
                    : string.Empty,
                GeometryFile = geometryFile,
                GeometrySha256 = Sha256(geometryPath),
                MinimapFile = minimapFile,
                MinimapSha256 = minimapHash,
            };
        }

        private static void PublishFile(string stagingDirectory, string outputDirectory, string fileName)
        {
            ValidatePackFileName(fileName, "pack file");
            string source = Path.Combine(stagingDirectory, fileName);
            string pending = Path.Combine(outputDirectory, fileName + ".pending");
            File.Copy(source, pending, true);
            File.Move(pending, Path.Combine(outputDirectory, fileName), true);
        }

        private static void ValidatePackFileName(string fileName, string description)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The {description} must be a plain file name.");
            }
        }

        private sealed class MapPackManifest
        {
            [JsonPropertyName("schema_version")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("producer")]
            public ProducerPayload Producer { get; set; }

            [JsonPropertyName("source")]
            public SourcePayload Source { get; set; }

            [JsonPropertyName("maps")]
            public List<MapPackEntry> Maps { get; set; }
        }

        private sealed class ProducerPayload
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("version")]
            public string Version { get; set; }
        }

        private sealed class SourcePayload
        {
            [JsonPropertyName("kind")]
            public string Kind { get; set; }

            [JsonPropertyName("versions")]
            public List<int> Versions { get; set; }
        }

        private sealed class MapPackEntry
        {
            [JsonPropertyName("map_id")]
            public int MapId { get; set; }

            [JsonPropertyName("map_name")]
            public string MapName { get; set; }

            [JsonPropertyName("geometry_file")]
            public string GeometryFile { get; set; }

            [JsonPropertyName("geometry_sha256")]
            public string GeometrySha256 { get; set; }

            [JsonPropertyName("minimap_file")]
            public string MinimapFile { get; set; }

            [JsonPropertyName("minimap_sha256")]
            public string MinimapSha256 { get; set; }
        }
    }
}
