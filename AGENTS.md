# AGENTS.md

## Scope
- This repository is a Windows-only .NET 10 solution for generating MapleStory object-detection datasets through a patched WzComparerR2 MapRender pipeline and Hecate map-geometry packs directly from WZ data.

## Project map
- `MapleStory.MachineLearningSampleGenerator`: CLI entry point. It validates arguments, resolves the MapleStory install path, and dispatches either rendered dataset generation or direct geometry export. `MapGeometryExporter` creates Hecate geometry assets; `MapPackPublisher` publishes their manifest and content-addressed files.
- `MapleStory.Sampler`: sampling loop plus rendered dataset/output code. `IDatasetWriter` is the extension point for new rendered formats. `IPostProcessor` is the extension point for image/label transforms.
- `MapRender.Invoker`: bridge into WzComparerR2. It loads maps, launches the renderer on an STA thread, moves the camera, and captures screenshots plus bounding boxes.
- `MapleStory.Common`: MapleStory path discovery and WZ tree search helpers.
- `WzComparerR2/`: vendored fork/submodule. Treat it as upstream-derived code, not the default place for feature work.

## Behavior that matters
- Rendered dataset flow: CLI -> `MapRenderInvoker` -> `Sampler.SampleAll(...)` -> `IDatasetWriter.Write(...)`.
- Geometry flow: CLI -> `MapGeometryExporter` -> `MapPackPublisher`; it does not launch MapRender or use an `IDatasetWriter`.
- The current capture path only records mob boxes from the renderer. Player boxes are synthetic and only appear when the YAML `player` post-processor generates avatars from WZ part IDs.
- `CocoWriter` and `DarknetWriter` delete their dataset root before writing. Do not point them at directories that contain anything you need to keep.
- Geometry export stages raw files in a temporary sibling of the output directory. Publication does not clear the output directory: it preserves alignment sidecars and prior content-addressed assets, writes immutable assets first, and atomically replaces `map-pack.json` only after every included map succeeds.
- The runtime expects MapleStory data under `Data/Base/Base.wz`. If `mapleStoryPath` is omitted from the YAML config, the app falls back to Windows registry lookup.
- `MapRenderInvoker` relies on reflection and copied upstream WzComparerR2 behavior. `WzComparerR2.MapRender/MapData.cs` is intentionally patched so NPCs are skipped when the generator hosts MapRender.

## Build and validation
- Requires Windows, x64, and .NET SDK 10. Projects target `net10.0-windows7.0`.
- Build from the solution root: `dotnet build MapleStoryDetectionSampleGenerator.sln -c Release`
- Build the full solution for end-to-end validation. The CLI project stages the `WzComparerR2.MapRender` plugin into its own output after build.
- Safe smoke test: `dotnet run --project MapleStory.MachineLearningSampleGenerator -- --help`
- Run automated tests with `dotnet test MapleStory.MachineLearningSampleGenerator.Tests/MapleStory.MachineLearningSampleGenerator.Tests.csproj -c Release`.
- After capture changes, also prefer a small single-map sample run and inspect both images and annotations. After geometry changes, inspect `map-pack.json`, verify its SHA-256 values, and load the referenced geometry and minimap together.

## Editing guidance
- Keep `WzComparerR2/` changes minimal and targeted. Most generator changes belong in `MapleStory.MachineLearningSampleGenerator`, `MapleStory.Sampler`, `MapRender.Invoker`, or `MapleStory.Common`.
- If you change screenshot generation, clipping, or coordinate handling, verify the emitted images and annotations together.
- New rendered dataset formats should implement `IDatasetWriter`. Direct WZ exports should follow the geometry exporter/publisher branch. New synthetic augmentation steps should implement `IPostProcessor`.
