using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

            List<StagedMap> stagedMaps = Directory.EnumerateFiles(stagingDirectory, "*.json")
                .Select(CreateStagedMap)
                .OrderBy(map => map.Entry.MapId)
                .ToList();
            if (stagedMaps.Count == 0)
            {
                throw new InvalidOperationException("A map pack must contain at least one geometry file.");
            }
            if (stagedMaps.Select(map => map.Entry.MapId).Distinct().Count() != stagedMaps.Count)
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
                Maps = stagedMaps.Select(map => map.Entry).ToList(),
            };

            Directory.CreateDirectory(outputDirectory);
            foreach (StagedMap map in stagedMaps)
            {
                PublishAsset(
                    outputDirectory,
                    map.Entry.GeometryFile,
                    map.Entry.GeometrySha256,
                    pending => File.WriteAllBytes(pending, map.GeometryContent));
                PublishAsset(
                    outputDirectory,
                    map.Entry.MinimapFile,
                    map.Entry.MinimapSha256,
                    pending => File.Copy(map.MinimapSourcePath, pending));
            }

            string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions) + "\n";
            PublishManifest(outputDirectory, manifestJson);
        }

        internal static string Sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static StagedMap CreateStagedMap(string geometryPath)
        {
            string geometryJson = File.ReadAllText(geometryPath);
            using JsonDocument document = JsonDocument.Parse(geometryJson);
            JsonElement root = document.RootElement;
            int mapId = root.GetProperty("map_id").GetInt32();
            string expectedStem = mapId.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(Path.GetFileNameWithoutExtension(geometryPath), expectedStem, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Geometry file '{geometryPath}' does not match map id {mapId}.");
            }

            if (!root.TryGetProperty("minimap", out JsonElement minimap) ||
                !minimap.TryGetProperty("image", out JsonElement image) ||
                image.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(image.GetString()))
            {
                throw new InvalidOperationException($"Geometry file '{geometryPath}' must reference a minimap image.");
            }

            string sourceMinimapFile = image.GetString();
            ValidatePackFileName(sourceMinimapFile, "minimap image");
            if (!string.Equals(Path.GetExtension(sourceMinimapFile), ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Geometry file '{geometryPath}' must reference a PNG minimap image.");
            }

            string minimapPath = Path.Combine(Path.GetDirectoryName(geometryPath), sourceMinimapFile);
            if (!File.Exists(minimapPath))
            {
                throw new InvalidOperationException($"Geometry file '{geometryPath}' references missing minimap '{sourceMinimapFile}'.");
            }

            string minimapHash = Sha256(minimapPath);
            string minimapFile = ContentAddressedFileName(mapId, minimapHash, ".png");
            JsonNode publishedGeometry = JsonNode.Parse(geometryJson);
            publishedGeometry["minimap"]["image"] = minimapFile;
            byte[] geometryContent = new UTF8Encoding(false).GetBytes(publishedGeometry.ToJsonString(JsonOptions) + "\n");
            string geometryHash = Sha256(geometryContent);
            string geometryFile = ContentAddressedFileName(mapId, geometryHash, ".json");

            return new StagedMap
            {
                Entry = new MapPackEntry
                {
                    MapId = mapId,
                    MapName = root.TryGetProperty("map_name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                        ? name.GetString()
                        : string.Empty,
                    GeometryFile = geometryFile,
                    GeometrySha256 = geometryHash,
                    MinimapFile = minimapFile,
                    MinimapSha256 = minimapHash,
                },
                GeometryContent = geometryContent,
                MinimapSourcePath = minimapPath,
            };
        }

        private static string ContentAddressedFileName(int mapId, string hash, string extension)
        {
            return mapId.ToString(CultureInfo.InvariantCulture) + "." + hash + extension;
        }

        private static string Sha256(byte[] content)
        {
            return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        private static void PublishAsset(
            string outputDirectory,
            string fileName,
            string expectedHash,
            Action<string> writePending)
        {
            ValidatePackFileName(fileName, "pack file");
            string target = Path.Combine(outputDirectory, fileName);
            if (File.Exists(target))
            {
                ValidatePublishedAsset(target, expectedHash);
                return;
            }

            string pending = Path.Combine(
                outputDirectory,
                "." + fileName + "." + Guid.NewGuid().ToString("N") + ".pending");
            try
            {
                writePending(pending);
                ValidatePublishedAsset(pending, expectedHash);
                try
                {
                    File.Move(pending, target);
                }
                catch (IOException) when (File.Exists(target))
                {
                    ValidatePublishedAsset(target, expectedHash);
                }
            }
            finally
            {
                if (File.Exists(pending))
                {
                    File.Delete(pending);
                }
            }
        }

        private static void ValidatePublishedAsset(string path, string expectedHash)
        {
            if (!string.Equals(Sha256(path), expectedHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Immutable pack asset '{path}' does not match its content hash.");
            }
        }

        private static void PublishManifest(string outputDirectory, string manifestJson)
        {
            string pending = Path.Combine(
                outputDirectory,
                "." + ManifestFileName + "." + Guid.NewGuid().ToString("N") + ".pending");
            try
            {
                File.WriteAllText(pending, manifestJson, new UTF8Encoding(false));
                File.Move(pending, Path.Combine(outputDirectory, ManifestFileName), true);
            }
            finally
            {
                if (File.Exists(pending))
                {
                    File.Delete(pending);
                }
            }
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

        private sealed class StagedMap
        {
            public MapPackEntry Entry { get; set; }

            public byte[] GeometryContent { get; set; }

            public string MinimapSourcePath { get; set; }
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
