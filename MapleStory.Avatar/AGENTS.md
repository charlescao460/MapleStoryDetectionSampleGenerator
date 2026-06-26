# AGENTS.md

## Scope
- This project is a headless library wrapper around `WzComparerR2.Avatar.AvatarCanvas`.
- It renders MapleStory avatar frames to transparent PNG bytes/files from WZ part IDs and pose settings.
- YAML/config integration belongs outside this library.

## Critical behavior
- Prefer public `Character` part nodes over `_Canvas` nodes when resolving `{id:D8}.img`. `_Canvas` is a linked-source bucket; using it directly can draw head/ear layers at `(0,0)` and break assembly.
- Preserve explicitly supplied body/head IDs. Only auto-fill a missing counterpart after all explicit IDs are loaded.
- Some skin body/head nodes do not have `info`; the library intentionally handles body/head/face/hair nodes without `info` while normal equipment still goes through `AvatarCanvas.AddPart`.
- `AvatarGenerator` depends on `MapleStory.Common.WzContext` for the shared `PluginManager.FindWz` hook. Activate the context before calls into `AvatarCanvas`.

## Validation
- Build the full solution from repo root:
  `dotnet build MapleStoryDetectionSampleGenerator.sln -c Release`
- Run tests:
  `dotnet test MapleStory.MachineLearningSampleGenerator.Tests -c Release`
- Real smoke render can omit `--path`; the debug CLI falls back to registry lookup:
  `dotnet run --project MapleStory.Avatar.DebugCli -c Release -- --parts 2000,12003,20000,30000,1040036,1060026 --action stand1 --emotion default --out %TEMP%\avatar.png`
- Always visually inspect avatar output after changes to part resolution, body/head sync, frame creation, or coordinate/origin handling.
