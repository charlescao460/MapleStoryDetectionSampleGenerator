import argparse
import hashlib
import re
from pathlib import Path

import fiftyone as fo


def parse_args():
    parser = argparse.ArgumentParser(description="Open a character COCO dataset in FiftyOne.")
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
    return f"character_{stem}_{digest}"


def split_has_images(coco_dir, split):
    labels_path = coco_dir / "annotations" / f"instances_{split}2017.json"
    data_path = coco_dir / f"{split}2017"
    return labels_path.exists() and data_path.exists() and any(data_path.iterdir())


def dataset_paths_exist(dataset):
    return all(Path(sample.filepath).exists() for sample in dataset)


def ensure_dataset(coco_dir, dataset_name, reload):
    source_path = str(coco_dir)
    if dataset_name in fo.list_datasets():
        dataset = fo.load_dataset(dataset_name)
        if not reload and len(dataset) and dataset.info.get("source_coco_dir") == source_path and dataset_paths_exist(dataset):
            return dataset

        fo.delete_dataset(dataset_name)

    dataset = fo.Dataset.from_dir(
        dataset_type=fo.types.COCODetectionDataset,
        data_path=str(coco_dir / "train2017"),
        labels_path=str(coco_dir / "annotations" / "instances_train2017.json"),
        name=dataset_name,
        tags=["train"],
    )
    dataset.persistent = True
    dataset.info["source_coco_dir"] = source_path

    if split_has_images(coco_dir, "val"):
        dataset.add_dir(
            dataset_type=fo.types.COCODetectionDataset,
            data_path=str(coco_dir / "val2017"),
            labels_path=str(coco_dir / "annotations" / "instances_val2017.json"),
            tags=["val"],
        )

    dataset.save()
    return dataset


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
