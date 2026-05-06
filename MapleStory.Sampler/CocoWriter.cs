using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MapRender.Invoker;

namespace MapleStory.Sampler
{
    public class CocoWriter : IDatasetWriter
    {
        private const double DefaultTestingPortion = 0.05;
        private const string DefaultRootDirectory = "coco";
        private const string TrainingDirectory = "train2017";
        private const string ValidationDirectory = "val2017";
        private const string AnnotationDirectory = "annotations";
        private const string TrainingJson = "instances_train2017.json";
        private const string ValidationJson = "instances_val2017.json";

        public string RootPath { get; }

        public string AnnotationsPath { get; }

        private readonly string _trainingImagesPath;
        private readonly string _validationImagesPath;
        private readonly Random _random;
        private readonly Dictionary<ObjectClass, int> _categoryIds;
        private readonly string _datasetName;
        private readonly object _sync = new object();
        private readonly List<CocoImage> _trainingImages;
        private readonly List<CocoImage> _validationImages;
        private readonly List<CocoCategory> _categories;
        private readonly List<CocoAnnotation> _trainingAnnotations;
        private readonly List<CocoAnnotation> _validationAnnotations;
        private int _nextImageId = 1;
        private int _nextCategoryId = 1;
        private int _nextAnnotationId = 1;

        public CocoWriter(string path, string name)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Target path {path} does not exist. Creating one...");
                Directory.CreateDirectory(path);
            }
            RootPath = Path.Combine(path, DefaultRootDirectory);
            if (!Directory.Exists(RootPath))
            {
                Directory.CreateDirectory(RootPath);
            }
            CleanDirectory(RootPath);
            _trainingImagesPath = Path.Combine(RootPath, TrainingDirectory);
            Directory.CreateDirectory(_trainingImagesPath);
            _validationImagesPath = Path.Combine(RootPath, ValidationDirectory);
            Directory.CreateDirectory(_validationImagesPath);
            AnnotationsPath = Path.Combine(RootPath, AnnotationDirectory);
            Directory.CreateDirectory(AnnotationsPath);

            _random = new Random();
            _categoryIds = new Dictionary<ObjectClass, int>();
            _datasetName = name;
            _trainingImages = new List<CocoImage>();
            _validationImages = new List<CocoImage>();
            _categories = new List<CocoCategory>();
            _trainingAnnotations = new List<CocoAnnotation>();
            _validationAnnotations = new List<CocoAnnotation>();
        }

        public void Write(Sample sample)
        {
            lock (_sync)
            {
                double rand = _random.NextDouble();
                List<CocoAnnotation> annotationsToAdd;
                List<CocoImage> imagesToAdd;
                string pathToWrite;
                if (rand < DefaultTestingPortion)
                {
                    annotationsToAdd = _validationAnnotations;
                    imagesToAdd = _validationImages;
                    pathToWrite = _validationImagesPath;
                }
                else
                {
                    annotationsToAdd = _trainingAnnotations;
                    imagesToAdd = _trainingImages;
                    pathToWrite = _trainingImagesPath;
                }
                // Write image
                string jpgFileName = sample.Guid + ".jpg";
                using FileStream imageStream = new FileStream(Path.Combine(pathToWrite, jpgFileName), FileMode.CreateNew);
                sample.ImageStream.WriteTo(imageStream);
                imageStream.Flush();
                CocoImage cocoImage = new CocoImage(jpgFileName, sample.Width, sample.Height, _nextImageId++);
                imagesToAdd.Add(cocoImage);

                // Write annotations
                GetAnnotationFromSample(sample, cocoImage.Id, ref annotationsToAdd);
            }
        }

        public void Finish()
        {
            lock (_sync)
            {
                CocoLicense license = new CocoLicense();
                // Write training set
                CocoJson trainingJson = new CocoJson(new CocoInfo($"{_datasetName} - Training"),
                    license,
                    _trainingImages,
                    _categories,
                    _trainingAnnotations);
                using FileStream trainingStream =
                    new FileStream(Path.Combine(AnnotationsPath, TrainingJson), FileMode.CreateNew);
                using Utf8JsonWriter trainingWriter = new Utf8JsonWriter(trainingStream);
                JsonSerializer.Serialize(trainingWriter, trainingJson);

                // Write validation set
                CocoJson validationJson = new CocoJson(new CocoInfo($"{_datasetName} - Training"),
                    license,
                    _validationImages,
                    _categories,
                    _validationAnnotations);
                using FileStream validationStream =
                    new FileStream(Path.Combine(AnnotationsPath, ValidationJson), FileMode.CreateNew);
                using Utf8JsonWriter validationWriter = new Utf8JsonWriter(validationStream);
                JsonSerializer.Serialize(validationWriter, validationJson);

                // Flush buffer (if any)
                trainingWriter.Flush();
                trainingStream.Flush();
                validationWriter.Flush();
                validationStream.Flush();
            }
        }

        private void GetAnnotationFromSample(Sample sample, int imageId, ref List<CocoAnnotation> dstList)
        {
            foreach (var sampleItem in sample.Items)
            {
                int categoryId = GetCategoryId(sampleItem.Type);
                CocoAnnotation toAdd = new CocoAnnotation(imageId, sampleItem.X, sampleItem.Y, sampleItem.Width,
                    sampleItem.Height, categoryId, _nextAnnotationId++, sampleItem.Keypoints);
                dstList.Add(toAdd);
            }
        }

        private int GetCategoryId(ObjectClass type)
        {
            if (_categoryIds.TryGetValue(type, out int categoryId))
            {
                return categoryId;
            }

            categoryId = _nextCategoryId++;
            _categoryIds.Add(type, categoryId);
            _categories.Add(CreateCategory(type, categoryId));
            return categoryId;
        }

        private static CocoCategory CreateCategory(ObjectClass type, int categoryId)
        {
            if (type == ObjectClass.RuneArrow)
            {
                return new CocoCategory(
                    "rune",
                    "rune_arrow",
                    categoryId,
                    new[] { "start", "end" },
                    new[] { new[] { 1, 2 } });
            }

            return new CocoCategory("element", type.ToString(), categoryId);
        }

        private static void CleanDirectory(string path)
        {
            if (Directory.EnumerateFileSystemEntries(path).Any())
            {
                Console.WriteLine("Warning: Target RootPath Is Not Empty. Cleaning target path...");
                DirectoryInfo directory = new DirectoryInfo(path);
                foreach (var fileInfo in directory.GetFiles())
                {
                    fileInfo.Delete();
                }
                foreach (var directoryInfo in directory.GetDirectories())
                {
                    directoryInfo.Delete(true);
                }
            }
        }

        #region JsonDefinitions
        private class CocoInfo
        {
            [JsonPropertyName("description")]
            public string Description { get; }

            [JsonPropertyName("url")]
            public string Url { get; } = @"https://github.com/charlescao460/MapleStoryDetectionSampleGenerator";

            [JsonPropertyName("version")]
            public string Version { get; } = "1.0";

            [JsonPropertyName("year")]
            public int Year { get; } = DateTime.Today.Year;

            [JsonPropertyName("contributor")]
            public string Contributor { get; } = "CSR";

            [JsonPropertyName("date_created")]
            public string DateCreated = DateTime.Today.ToLongDateString();

            public CocoInfo(string description)
            {
                Description = description;
            }
        }

        private class CocoLicense
        {
            [JsonPropertyName("url")]
            public string Url { get; } = @"https://github.com/charlescao460/MapleStoryDetectionSampleGenerator/blob/main/LICENSE";

            [JsonPropertyName("id")]
            public int Id { get; } = 1;

            [JsonPropertyName("name")]
            public string Name { get; } = "MIT License";
        }

        private class CocoImage
        {
            [JsonPropertyName("license")]
            public int License { get; } = 1;

            [JsonPropertyName("file_name")]
            public string FileName { get; }

            [JsonPropertyName("coco_url")]
            public string CocoUrl { get; } = string.Empty;

            [JsonPropertyName("height")]
            public int Height { get; }

            [JsonPropertyName("width")]
            public int Width { get; }

            [JsonPropertyName("date_captured")]
            public string DateCaptured = $"{DateTime.Now:G}";

            [JsonPropertyName("flickr_url")]
            public string FlickrUrl { get; } = string.Empty;

            [JsonPropertyName("id")]
            public int Id { get; }

            public CocoImage(string fileName, int width, int height, int id)
            {
                FileName = fileName;
                Width = width;
                Height = height;
                Id = id;
            }
        }

        private class CocoCategory
        {
            [JsonPropertyName("supercategory")]
            public string SuperCategory { get; }

            [JsonPropertyName("id")]
            public int Id { get; }

            [JsonPropertyName("name")]
            public string Name { get; }

            [JsonPropertyName("keypoints")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string[] Keypoints { get; }

            [JsonPropertyName("skeleton")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int[][] Skeleton { get; }

            public CocoCategory(
                string superCategory,
                string name,
                int id,
                string[] keypoints = null,
                int[][] skeleton = null)
            {
                SuperCategory = superCategory;
                Name = name;
                Id = id;
                Keypoints = keypoints;
                Skeleton = skeleton;
            }
        }

        private class CocoAnnotation
        {
            [JsonPropertyName("segmentation")]
            public float[][] Segmentation { get; }

            [JsonPropertyName("area")]
            public float Area { get; }

            [JsonPropertyName("iscrowd")]
            public int IsCrowd { get; } = 0;

            [JsonPropertyName("image_id")]
            public int ImageId { get; }

            [JsonPropertyName("bbox")]
            public float[] Bbox { get; }

            [JsonPropertyName("category_id")]
            public int CategoryId { get; }

            [JsonPropertyName("keypoints")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public float[] Keypoints { get; }

            [JsonPropertyName("num_keypoints")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? NumKeypoints { get; }

            [JsonPropertyName("id")]
            public int Id { get; }

            public CocoAnnotation(
                int imageId,
                float x,
                float y,
                float width,
                float height,
                int categoryId,
                int id,
                IList<TargetKeypoint> keypoints)
            {
                Segmentation = new float[][]
                {
                    new float[] { x, y, x + width, y, x + width, y + height, x, y + height }
                };
                Area = width * height;
                ImageId = imageId;
                Bbox = new float[] { x, y, width, height };
                CategoryId = categoryId;
                Id = id;
                if (keypoints != null && keypoints.Count > 0)
                {
                    Keypoints = keypoints
                        .SelectMany(keypoint => new[] { keypoint.X, keypoint.Y, (float)keypoint.Visibility })
                        .ToArray();
                    NumKeypoints = keypoints.Count(keypoint => keypoint.Visibility > 0);
                }
            }
        }

        private class CocoJson
        {
            [JsonPropertyName("info")]
            public CocoInfo Info { get; }

            [JsonPropertyName("licenses")]
            public CocoLicense[] Licenses { get; }

            [JsonPropertyName("images")]
            public IList<CocoImage> Images { get; }

            [JsonPropertyName("categories")]
            public IList<CocoCategory> Categories { get; }

            [JsonPropertyName("annotations")]
            public IList<CocoAnnotation> Annotations { get; }

            public CocoJson(CocoInfo info, CocoLicense license, IList<CocoImage> images, IList<CocoCategory> categories,
                IList<CocoAnnotation> annotations)
            {
                Info = info;
                Licenses = new[] { license };
                Images = images;
                Categories = categories;
                Annotations = annotations;
            }
        }

        #endregion
    }
}
