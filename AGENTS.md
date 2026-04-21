# AGENTS.md

## Scope
- This repository is a Windows-only .NET 10 solution for generating MapleStory object-detection datasets by driving a patched WzComparerR2 MapRender pipeline.

## Project map
- `MapleStory.MachineLearningSampleGenerator`: CLI entry point. It validates arguments, resolves the MapleStory install path, selects the dataset writer, and wires optional post-processing.
- `MapleStory.Sampler`: sampling loop plus dataset/output code. `IDatasetWriter` is the extension point for new output formats. `IPostProcessor` is the extension point for image/label transforms.
- `MapRender.Invoker`: bridge into WzComparerR2. It loads maps, launches the renderer on an STA thread, moves the camera, and captures screenshots plus bounding boxes.
- `MapleStory.Common`: MapleStory path discovery and WZ tree search helpers.
- `WzComparerR2/`: vendored fork/submodule. Treat it as upstream-derived code, not the default place for feature work.

## Behavior that matters
- The main flow is: CLI -> `MapRenderInvoker` -> `Sampler.SampleAll(...)` -> `IDatasetWriter.Write(...)`.
- The current capture path only records mob boxes from the renderer. Player boxes are synthetic and only appear when `--post --players <dir>` is used.
- `CocoWriter` and `DarknetWriter` delete their dataset root before writing. Do not point them at directories that contain anything you need to keep.
- The runtime expects MapleStory data under `Data/Base/Base.wz`. If `--path` is omitted, the app falls back to Windows registry lookup.
- `MapRenderInvoker` relies on reflection and copied upstream WzComparerR2 behavior. `WzComparerR2.MapRender/MapData.cs` is intentionally patched so NPCs are skipped when the generator hosts MapRender.

## Build and validation
- Requires Windows, x64, and .NET SDK 10. Projects target `net10.0-windows7.0`.
- Build from the solution root: `dotnet build MapleStoryDetectionSampleGenerator.sln -c Release`
- Build the full solution for end-to-end validation. The CLI project stages the `WzComparerR2.MapRender` plugin into its own output after build.
- Safe smoke test: `dotnet run --project MapleStory.MachineLearningSampleGenerator -- --help`
- `--help` currently prints usage but exits non-zero because help is routed through the parse-error path. Treat that as a CLI quirk, not a build failure.
- There are no automated test projects in this repo. After code changes, prefer a small single-map sample run and inspect both images and annotations.

## Editing guidance
- Keep `WzComparerR2/` changes minimal and targeted. Most generator changes belong in `MapleStory.MachineLearningSampleGenerator`, `MapleStory.Sampler`, `MapRender.Invoker`, or `MapleStory.Common`.
- If you change screenshot generation, clipping, or coordinate handling, verify the emitted images and annotations together.
- New output formats should implement `IDatasetWriter`. New synthetic augmentation steps should implement `IPostProcessor`.
