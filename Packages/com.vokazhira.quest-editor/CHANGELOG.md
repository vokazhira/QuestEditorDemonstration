# Changelog

All notable changes to this package are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-11-02

First MVP release for Unity 2023.1.22f1.

### Added

- `QuestGraphAsset` quest definitions with `[SerializeReference]` nodes, connections, quest settings and a format version.
- Built-in nodes: Start, End (Completed / Failed), Kill / Collect / Complete Wave objectives, Quest Completed condition,
  Gold and Experience rewards. Custom node types are discovered through `[QuestNode]` and `TypeCache`.
- `QuestPortRules` shared by the editor and the validator, including Flow cycle detection.
- Quest Editor window (**Tools → Quest Editor**): quest library with search, create, duplicate and delete; graph canvas
  on GraphView with node search, connections and Undo/Redo; universal node inspector; quest settings; working copy
  with Save / Save As / New / Open and unsaved-changes prompts; diagnostics panel with jump-to-node.
- `QuestGraphValidator` covering start/end, connections, ports, ids, values, reachability, cycles and missing node
  types, with stable diagnostic codes.
- Runtime: `QuestManager`, `QuestGraphRunner`, `QuestRewardDispatcher`, `QuestInstance`, `QuestDatabase`, `QuestEvent`;
  AutoGrant / ManualClaim rewards that are never granted twice; lifecycle events; injectable `ILogger`.
- Progress persistence: `QuestSaveData` DTOs, `IQuestSaveService`, `FileQuestSaveService` with atomic writes, corrupted
  file backup, orphaned and frozen quest records, save versioning.
- Basic Quest Example sample with a demo scene, quests, database and an asset builder.
- EditMode tests for the runtime logic and the editor features.
