using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using MapleStory.Sampler;
using MapRender.Invoker;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public sealed class CocoWriterTests
    {
        [Fact]
        public void Write_RuneArrowIncludesCategoryAndKeypoints()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string outputPath = workspace.CreateDirectory("output");
            CocoWriter writer = new CocoWriter(outputPath, "rune");
            Sample sample = CreateSample(new TargetItem
            {
                X = 2,
                Y = 3,
                Width = 10,
                Height = 5,
                Type = ObjectClass.RuneArrow,
                Keypoints =
                {
                    new TargetKeypoint(2, 5.5f),
                    new TargetKeypoint(12, 5.5f),
                },
            });

            writer.Write(sample);
            writer.Finish();

            using JsonDocument train = ReadJson(outputPath, "instances_train2017.json");
            using JsonDocument validation = ReadJson(outputPath, "instances_val2017.json");
            JsonElement category = train.RootElement.GetProperty("categories").EnumerateArray().Single();
            Assert.Equal("rune", category.GetProperty("supercategory").GetString());
            Assert.Equal("rune_arrow", category.GetProperty("name").GetString());
            Assert.Equal(new[] { "start", "end" },
                category.GetProperty("keypoints").EnumerateArray().Select(value => value.GetString()).ToArray());
            JsonElement skeleton = category.GetProperty("skeleton").EnumerateArray().Single();
            Assert.Equal(new[] { 1, 2 }, skeleton.EnumerateArray().Select(value => value.GetInt32()).ToArray());

            JsonElement annotation = FindSingleAnnotation(train, validation);
            Assert.Equal(2, annotation.GetProperty("num_keypoints").GetInt32());
            Assert.Equal(
                new[] { 2f, 5.5f, 2f, 12f, 5.5f, 2f },
                annotation.GetProperty("keypoints").EnumerateArray().Select(value => value.GetSingle()).ToArray());
        }

        [Fact]
        public void Write_CharacterAnnotationOmitsKeypointFields()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string outputPath = workspace.CreateDirectory("output");
            CocoWriter writer = new CocoWriter(outputPath, "character");
            Sample sample = CreateSample(new TargetItem
            {
                X = 2,
                Y = 3,
                Width = 10,
                Height = 5,
                Type = ObjectClass.Player,
            });

            writer.Write(sample);
            writer.Finish();

            using JsonDocument train = ReadJson(outputPath, "instances_train2017.json");
            using JsonDocument validation = ReadJson(outputPath, "instances_val2017.json");
            JsonElement category = train.RootElement.GetProperty("categories").EnumerateArray().Single();
            Assert.False(category.TryGetProperty("keypoints", out _));
            Assert.False(category.TryGetProperty("skeleton", out _));

            JsonElement annotation = FindSingleAnnotation(train, validation);
            Assert.False(annotation.TryGetProperty("keypoints", out _));
            Assert.False(annotation.TryGetProperty("num_keypoints", out _));
        }

        private static Sample CreateSample(TargetItem item)
        {
            MemoryStream stream = new MemoryStream();
            using (Bitmap bitmap = new Bitmap(20, 20, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                bitmap.Save(stream, ImageFormat.Jpeg);
            }

            stream.Position = 0;
            return new Sample(stream, new[] { item }, 20, 20);
        }

        private static JsonDocument ReadJson(string outputPath, string fileName)
        {
            string path = Path.Combine(outputPath, "coco", "annotations", fileName);
            return JsonDocument.Parse(File.ReadAllText(path));
        }

        private static JsonElement FindSingleAnnotation(params JsonDocument[] documents)
        {
            JsonElement[] annotations = documents
                .SelectMany(document => document.RootElement.GetProperty("annotations").EnumerateArray())
                .ToArray();
            return Assert.Single(annotations);
        }
    }
}
