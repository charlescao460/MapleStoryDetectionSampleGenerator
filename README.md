# MapleStoryDetectionSampleGenerator
Generate Machine Learning Samples Object Detection In MapleStory
![](https://github.com/charlescao460/MapleStoryDetectionSampleGenerator/blob/main/pictures/result.png)



# Performance
This generator can generate arbitrarily many annotated samples. All bounding boxes are precisely annotated based on rendering coordinates.

With [YOLOv4](https://github.com/AlexeyAB/darknet/blob/master/cfg/yolov4-custom.cfg) and ~5000 samples, it can achieve 99.8%mAP in test set.


![](https://github.com/charlescao460/MapleStoryDetectionSampleGenerator/blob/main/pictures/chart_yolov4-custom.png)

# Requirement
* .NET 10.0 SDK (10.0.0 or above)

# Build
1. Clone this repository with submodules by </br> `git clone --recursive git@github.com:charlescao460/MapleStoryDetectionSampleGenerator.git`. </br>Note that `--recursive` is necessary.
2. Build `MapleStoryDetectionSampleGenerator.sln`
3. Run `MapleStory.MachineLearningSampleGenerator\bin\Release\net10.0-windows7.0\WzComparerR2.exe` and open MapRender once. Running `WzComparerR2.exe` will generate `Setting.config`, which is required for our MapRender invoker.

# Run
(Assuming assemblies are built with `Release` configuration. `Debug` configuration is similar)
1. Use `WzComparerR2.exe` to find the desired map you want to sample. Assuming `993134200.img` is the map you want in Limina.
2. From the solution root, prepare a YAML config file. A checked-in example is available at `Examples\sample-generator.yml`.
3. If you want synthetic players, configure avatar WZ part IDs in the `player` post-processor. The generator renders player frames on the fly through `MapleStory.Avatar`.
4. Run `dotnet run --project .\MapleStory.MachineLearningSampleGenerator -- --config ".\Examples\sample-generator.yml"`</br>
You can run `.\MapleStory.MachineLearningSampleGenerator.exe --help` for usage hint, or execute the built binary directly with `--config <path>`. The config file is the single source of truth for maps, rendering, output, and post-processors.

Example YAML:
```yaml
mode: character
concurrency: 2

output:
  format: coco
  path: .
  name: sample-generator

render:
  width: 1366
  height: 768

sampling:
  count: 1000
  intervalMs: 0

postProcessors:
  - type: player
    count: 3
    actions: [stand1, walk1, jump]
    emotions: [default]
    avatars:
      - parts: [2000, 12003, 20000, 30000, 1040036, 1060026]
      - parts: [2000, 12003, 20000, 30000, 1040036, 1060026, 1703598]

maps:
  - id: 993134200
  - id: 450007010
    sampling:
      count: 2000
    postProcessors: []
```

Notes about the YAML format:
* `mode` is required. Use `character` for normal map/object samples and `rune` for rune-arrow keypoint samples.
* `concurrency` is optional and controls how many map renders run at once. It defaults to `1`.
* `maps` is required. The legacy sequence form lists explicit map IDs, and each `id` should be the numeric map id without `.img`.
* Root `sampling` and `postProcessors` act as defaults for every map.
* `sampling.count` is required and controls how many uniformly random camera positions are sampled from each map.
* A map-level `sampling` block overrides only the fields it sets.
* A map-level `postProcessors` block replaces the root processor list. `postProcessors: []` disables inherited processors for that map.
* `player.count` is the number of generated player instances added to each sampled screenshot. It defaults to `3` when omitted.
* `player.avatars[].parts` is an ordered list of WZ part IDs. Later IDs replace earlier slot conflicts, matching the avatar generator behavior.
* Relative paths are resolved from the YAML file location.

Rune mode uses the same `render` and `sampling` sections, but requires `output.format: coco` and does not support `postProcessors`. Each output image is a center-square crop with four generated `rune_arrow` annotations and two COCO keypoints per arrow.

Rune mode can also randomly choose maps from all numeric `*.img` map nodes in the MapleStory data. Explicit entries are always included first; `random.count` adds that many additional maps and excludes duplicate explicit IDs. Add `seed` when you need repeatable selection.

Use `maps.allMaps: true` to sample every numeric `*.img` map node. Explicit `entries` are still included first and are not duplicated. `allMaps` cannot be combined with `maps.random`.

```yaml
mode: rune
output:
  format: coco
  path: ./rune-output
  name: rune-sample
render:
  width: 1366
  height: 768
sampling:
  count: 1000
  intervalMs: 0
maps:
  entries:
    - id: 410013660
  random:
    count: 50
    seed: 12345
```

All-map rune example:

```yaml
mode: rune
concurrency: 16
output:
  format: coco
  path: E:\MapleStory-ML\DATA\rune
  name: rune-all-maps
render:
  width: 1366
  height: 768
sampling:
  count: 1
  intervalMs: 0
maps:
  allMaps: true
```

# Note
* Since NPCs look like players, including them without annotation could result a negative effect on our model. Therefore, by default, we changed [WzComparerR2.MapRender/MapData.cs](https://github.com/Kagamia/WzComparerR2/blob/main/WzComparerR2.MapRender/MapData.cs) to prevent any NPC data loaded into map render when invoking from `MapleStory.MachineLearningSampleGenerator.exe`,

# Output Formats
## Tensorflow TFRecord
According to Tensorflow [official document](https://www.tensorflow.org/tutorials/load_data/tfrecord#tfrecords_format_details), the output .tfrecord contains multiple [tf.train.Example](https://www.tensorflow.org/api_docs/python/tf/train/Example) in single file. With each example store in the following formats:

```
uint64 length
uint32 masked_crc32_of_length
byte   data[length]
uint32 masked_crc32_of_data
```
And
```
masked_crc = ((crc >> 15) | (crc << 17)) + 0xa282ead8ul
```
Each `tf.train.Example` is generated by [protobuf-net](https://github.com/protobuf-net/protobuf-net) according to Tensorflow [example.proto](https://github.com/tensorflow/tensorflow/blob/master/tensorflow/core/example/example.proto)

## Darknet
Output directory structure:
```
data/
|---obj/
|   |---1.jpg
|   |---1.txt
|   |---......
|---obj.data
|---obj.names
|---test.txt
|---train.txt
```
`obj.data` contains 
```
classes=2
train=data/train.txt
valid=data/test.txt
names=data/obj.names
backup = backup/
```
And `obj.names` contains the class name for object. `test.txt` and `train.txt` contains samples for testing/training with ratio of 5:95 (5% of images in `obj/` are used for testing).

## COCO
Output directory structure:
```
coco/
|---train2017/
|   |---1.jpg
|   |---2.jpg
|   |---......
|---val2017/
|   |---1000.jpg
|   |---1001.jpg
|   |---......
|---annotations/
|   |---instances_train2017.json
|   |---instances_val2017.json
```
The COCO json is defined as following:
```json
{
  "info": {
    "description": "MapleStory 993134100.img Object Detection Samples - Training",
    "url": "https://github.com/charlescao460/MapleStoryDetectionSampleGenerator",
    "version": "1.0",
    "year": 2021,
    "contributor": "CSR"
  },
  "licenses": [
    {
      "url": "https://github.com/charlescao460/MapleStoryDetectionSampleGenerator/blob/main/LICENSE",
      "id": 1,
      "name": "MIT License"
    }
  ],
  "images": [
    {
      "license": 1,
      "file_name": "30a892e1-7f3d-4c65-bdd1-9d28f1ae5187.jpg",
      "coco_url": "",
      "height": 768,
      "width": 1366,
      "flickr_url": "",
      "id": 1
    },
    ...],
  "categories": [
    {
      "supercategory": "element",
      "id": 1,
      "name": "Mob"
    },
    {
      "supercategory": "element",
      "id": 2,
      "name": "Player"
    }
  ],
  "annotations": [
    {
      "segmentation": [
        [
          524,
          429,
          664,
          429,
          664,
          578,
          524,
          578
        ]
      ],
      "area": 20860,
      "iscrowd": 0,
      "image_id": 1,
      "bbox": [
        524,
        429,
        140,
        149
      ],
      "category_id": 1,
      "id": 1
    },
    ...]
```
Note that `segmentation` covers the area as the same as `bbox` does. No segmentation or masked implemented .
