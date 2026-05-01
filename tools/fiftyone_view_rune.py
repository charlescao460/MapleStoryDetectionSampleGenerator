import argparse
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path

import fiftyone as fo


def parse_args():
    parser = argparse.ArgumentParser(description="Open a rune COCO keypoint dataset in FiftyOne.")
    parser.add_argument("path", help="Path to the COCO directory, or to a directory containing a coco subdirectory.")
    parser.add_argument("--name", help="FiftyOne dataset name. Defaults to a stable name derived from the path.")
    parser.add_argument("--address", default="localhost", help="FiftyOne app bind address.")
    parser.add_argument("--port", type=int, default=5151, help="FiftyOne app port.")
    parser.add_argument("--reload", action="store_true", help="Delete and reload the FiftyOne dataset.")
    return parser.parse_args()


def resolve_coco_dir(path):
    root = Path(path).expanduser().resolve()
    if (root / "annotations").exists():
        return root

    coco_dir = root / "coco"
    if (coco_dir / "annotations").exists():
        return coco_dir

    raise FileNotFoundError(f"Could not find COCO annotations under '{root}' or '{coco_dir}'.")


def default_dataset_name(coco_dir):
    digest = hashlib.sha1(str(coco_dir).encode("utf-8")).hexdigest()[:10]
    stem = re.sub(r"[^A-Za-z0-9_]+", "_", coco_dir.parent.name or coco_dir.name).strip("_")
    return f"rune_{stem}_{digest}"


def dataset_paths_exist(dataset):
    return all(Path(sample.filepath).exists() for sample in dataset)


def ensure_dataset(coco_dir, dataset_name, reload):
    source_path = str(coco_dir)
    if dataset_name in fo.list_datasets():
        dataset = fo.load_dataset(dataset_name)
        if not reload and len(dataset) and dataset.info.get("source_coco_dir") == source_path and dataset_paths_exist(dataset):
            return dataset

        fo.delete_dataset(dataset_name)

    dataset = fo.Dataset(dataset_name)
    dataset.persistent = True
    dataset.info["source_coco_dir"] = source_path
    dataset.default_skeleton = fo.KeypointSkeleton(labels=["start", "end"], edges=[[0, 1]])
    dataset.skeletons = {
        "keypoints": fo.KeypointSkeleton(labels=["start", "end"], edges=[[0, 1]])
    }

    for split in ("train", "val"):
        add_split(dataset, coco_dir, split)

    dataset.save()
    return dataset


def add_split(dataset, coco_dir, split):
    labels_path = coco_dir / "annotations" / f"instances_{split}2017.json"
    data_path = coco_dir / f"{split}2017"
    if not labels_path.exists() or not data_path.exists():
        return

    with labels_path.open("r", encoding="utf-8") as file:
        coco = json.load(file)

    categories = {
        category["id"]: category.get("name", str(category["id"]))
        for category in coco.get("categories", [])
    }
    annotations_by_image = defaultdict(list)
    for annotation in coco.get("annotations", []):
        annotations_by_image[annotation["image_id"]].append(annotation)

    samples = []
    for image in coco.get("images", []):
        image_path = data_path / image["file_name"]
        if not image_path.exists():
            continue

        width = image["width"]
        height = image["height"]
        detections = []
        keypoints = []
        for annotation in annotations_by_image.get(image["id"], []):
            label = categories.get(annotation.get("category_id"), "rune_arrow")
            detections.append(create_detection(annotation, label, width, height))

            keypoint = create_keypoint(annotation, label, width, height)
            if keypoint is not None:
                keypoints.append(keypoint)

        sample = fo.Sample(filepath=str(image_path), tags=[split])
        sample["detections"] = fo.Detections(detections=detections)
        sample["keypoints"] = fo.Keypoints(keypoints=keypoints)
        samples.append(sample)

    if samples:
        dataset.add_samples(samples)


def create_detection(annotation, label, width, height):
    x, y, box_width, box_height = annotation["bbox"]
    return fo.Detection(
        label=label,
        bounding_box=[
            normalize(x, width),
            normalize(y, height),
            normalize(box_width, width),
            normalize(box_height, height),
        ],
        coco_id=annotation.get("id"),
        area=annotation.get("area"),
        num_keypoints=annotation.get("num_keypoints"),
    )


def create_keypoint(annotation, label, width, height):
    values = annotation.get("keypoints")
    if not values:
        return None

    points = []
    visibility = []
    for index in range(0, len(values), 3):
        x, y, visible = values[index:index + 3]
        if visible <= 0:
            continue

        points.append([normalize(x, width), normalize(y, height)])
        visibility.append(visible)

    if not points:
        return None

    return fo.Keypoint(
        label=label,
        points=points,
        coco_id=annotation.get("id"),
        visibility=visibility,
    )


def normalize(value, denominator):
    if denominator <= 0:
        return 0

    return max(0, min(1, value / denominator))


if __name__ == "__main__":
    args = parse_args()
    coco_dir = resolve_coco_dir(args.path)
    dataset_name = args.name or default_dataset_name(coco_dir)
    session = fo.launch_app(
        ensure_dataset(coco_dir, dataset_name, args.reload),
        address=args.address,
        port=args.port,
    )
    session.wait()
