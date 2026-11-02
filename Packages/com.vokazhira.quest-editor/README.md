# Quest Editor

[Русская версия](README.ru.md)

A visual, graph-based quest editor for Unity and a runtime system that executes the quests.

You build a quest from nodes in **Tools → Quest Editor**, connect them, set their parameters and save the quest
as an asset. The same asset runs in the game without any Editor API.

```
[Start] → [Kill: goblin ×10] → [Condition: quest "intro" completed?] ─True──→ [End: Completed] ← [Reward: 100 gold]
                                                                       │                         ← [Reward: 25 XP]
                                                                       └False─→ [End: Failed]
```

- **Unity:** 2023.1.22f1 (the package declares `"unity": "2023.1"`).
- **Dependencies:** built-in modules only (`jsonserialize`, `uielements`). No third-party packages.
- **Extensible:** new objective, condition and reward types are added in your own project without changing the package.

## Contents

- [Installation](#installation)
- [Package structure](#package-structure)
- [Your first quest](#your-first-quest)
- [Nodes and connections](#nodes-and-connections)
- [Validation](#validation)
- [Running quests: QuestManager](#running-quests-questmanager)
- [Saving progress](#saving-progress)
- [Custom objectives, conditions and rewards](#custom-objectives-conditions-and-rewards)
- [Renaming node classes: `[MovedFrom]`](#renaming-node-classes-movedfrom)
- [Sample](#sample)
- [Running the tests](#running-the-tests)
- [Limitations](#limitations)
- [Roadmap](#roadmap)

## Installation

**From a Git URL.** In Unity open **Window → Package Manager → + → Add package from git URL…** and enter:

```
https://github.com/vokazhira/QuestEditorDemonstration.git?path=/Packages/com.vokazhira.quest-editor
```

To pin a branch or tag, append `#<branch-or-tag>` to the URL.

**Locally.** Copy the `com.vokazhira.quest-editor` folder into your project's `Packages/` folder (an embedded package),
or use **+ → Add package from disk…** and select its `package.json`.

## Package structure

```
com.vokazhira.quest-editor/
├── package.json, README.md, README.ru.md, CHANGELOG.md, LICENSE.md
├── Runtime/        Vokazhira.QuestEditor          (no UnityEditor references)
│   ├── Graph/          QuestGraphAsset, QuestNodeData, QuestEdgeData, ports, QuestPortRules, [QuestNode]
│   ├── Nodes/          Start, End, Objectives/, Conditions/, Rewards/
│   ├── Validation/     QuestGraphValidator, QuestDiagnostic, diagnostic codes
│   ├── Execution/      QuestManager, QuestInstance, QuestGraphRunner, QuestRewardDispatcher, QuestEvent, QuestDatabase
│   ├── Services/       ICurrencyService, IExperienceService, IRewardHandler<T>, built-in reward handlers
│   └── Persistence/    QuestSaveData DTOs, IQuestSaveService, FileQuestSaveService
├── Editor/         Vokazhira.QuestEditor.Editor   (editor window, graph view, inspector, library)
├── Tests/          EditMode tests (Runtime logic and Editor features)
└── Samples~/BasicQuestExample/
```

All runtime types live in the single namespace `Vokazhira.QuestEditor`; editor types in `Vokazhira.QuestEditor.Editor`.

## Your first quest

1. Open **Tools → Quest Editor**.
2. Press **New**. A draft with a **Start** node and an **End (Completed)** node appears.
3. Press **Settings** and set a unique **Quest Id** (for example `forest_cleanup`), a display name and a reward mode.
4. Right-click the canvas → **Create Node** (or press **Space**) → **Objectives/Kill Enemies**. The node appears under the cursor.
5. Select the node; in the **Inspector** on the right set **Enemy Id** = `goblin` and **Required Count** = `10`.
6. Drag connections: **Start.Next → Kill.In**, **Kill.Done → End.In**.
7. Add **Rewards/Gold** and connect **Gold.Reward → End.Rewards**.
8. Press **Validate**. The bottom panel lists errors and warnings; click a line to jump to the node.
9. Press **Save** and choose where to store the asset.
10. Create a **Quest Database** (**Create → Quest Editor → Quest Database**) and press **Collect All Project Quests** in its inspector.

Double-clicking a quest asset in the Project window also opens it in the editor. Every change goes through Undo
(**Ctrl+Z / Ctrl+Y**). The window edits a working copy: the asset changes only on **Save**, and closing the window or
opening another quest with unsaved changes asks **Save / Don't Save / Cancel**.

## Nodes and connections

There are two kinds of ports: **Flow** (execution order) and **Reward** (rewards attached to an end).

| Node | Inputs | Outputs | Purpose |
|---|---|---|---|
| **Start** | — | Flow `Next` (single) | Entry point. Exactly one per graph. |
| **Objective** | Flow `In` (multi) | Flow `Done` (single) | Waits for game events until progress reaches the required count. |
| **Condition** | Flow `In` (multi) | Flow `True`, `False` (single) | Instant check and branching. |
| **Reward** | — | Reward `Reward` (single) | A reward description; not part of the flow. |
| **End** | Flow `In` (multi), Reward `Rewards` (multi, Completed only) | — | Finishes the quest as **Completed** or **Failed**. |

Connection rules (`QuestPortRules`, shared by the editor and the validator): output → input only; the same port kind
only; no self-connections; port capacity is respected; Flow cycles are not allowed. Incompatible ports are not
highlighted while dragging a connection; a connection that would close a cycle is rejected with a message.

**Built-in types**

| Type | Fields | Reacts to |
|---|---|---|
| `KillObjective` | `enemyId`, `requiredCount` | `QuestEvent.EnemyKilled(enemyId)` |
| `CollectObjective` | `itemId`, `requiredCount` | `QuestEvent.ItemCollected(itemId)` |
| `CompleteWaveObjective` | `waveId` (empty = any wave), `requiredCount` | `QuestEvent.WaveCompleted(waveId)` |
| `QuestCompletedCondition` | `questId` | `True` if that quest is `Completed` when the condition is reached |
| `GoldReward`, `ExperienceReward` | `amount` | handled by `GoldRewardHandler` / `ExperienceRewardHandler` |

All objectives also have `ObjectiveId` (progress is saved under it), `Title`, `Description` and `IsOptional`
(stored only in this version). Every reward has a stable `RewardId`.

Rewards are granted **only when the quest reaches End (Completed)**, never in the middle of the flow.

## Validation

`QuestGraphValidator.Validate(graph, otherQuests)` returns a list of `QuestDiagnostic` (severity, code, message, node id).
The editor runs it on open, on **Validate** and on **Save**; the library marks invalid quests with an error icon.
A graph with errors can still be saved (work is never lost), but **`QuestManager` refuses to start it**.

Errors (`QE…`): missing or several Start nodes, no End, broken connections, incompatible ports or exceeded capacity,
duplicate connections, empty or duplicate `questId`, duplicate node / objective / reward ids, invalid values
(`requiredCount ≤ 0`, `amount ≤ 0`, empty `enemyId` / `itemId`…), unconnected Flow ports, no End reachable from Start,
Flow cycles, missing node classes, rewards connected to something other than End (Completed).

Warnings (`QW…`): nodes unreachable from Start, unconnected rewards, objectives without a description,
Completed ends without rewards. The full list of codes is in `QuestDiagnosticCodes`.

## Running quests: QuestManager

`QuestManager` is a plain C# class created by your game — not a MonoBehaviour and not a singleton.

```csharp
using UnityEngine;
using Vokazhira.QuestEditor;

public sealed class GameQuests : MonoBehaviour
{
    [SerializeField] private QuestDatabase database;
    private QuestManager quests;

    private void Awake()
    {
        quests = new QuestManager(database, new FileQuestSaveService());
        quests.RegisterRewardHandler(new GoldRewardHandler(myWallet));            // ICurrencyService from your game
        quests.RegisterRewardHandler(new ExperienceRewardHandler(myExperience));  // IExperienceService from your game
        quests.QuestCompleted += quest => Debug.Log($"Completed: {quest.QuestId}");
        quests.Load();
    }

    public void OnGoblinKilled() => quests.Report(QuestEvent.EnemyKilled("goblin"));

    private void OnApplicationQuit() => quests.Save();
    private void OnDestroy() => quests.Dispose();
}
```

- **Lifecycle:** `CanStart`, `Start`, `Abandon` (→ Failed), `Report`, `Claim`, `GetState`, `GetInstance`, `GetInstances`,
  `GetValidationErrors`.
- **States:** `Available → Active → Claimable → Completed`, or `Failed`.
- **Rewards:** `AutoGrant` grants rewards as soon as the quest reaches End (Completed); `ManualClaim` makes the quest
  `Claimable` and grants rewards on `Claim(questId)`. If a reward cannot be granted (no handler, or the handler returned
  `false`), the quest stays `Claimable` and a later `Claim` grants only the missing rewards — a reward is never granted twice.
- **Events:** `QuestStarted`, `QuestProgressChanged`, `QuestClaimable`, `QuestCompleted`, `QuestFailed`,
  `QuestRewardGranted`, `QuestRewardFailed`. `Dispose()` removes all subscribers and handlers.
- **Services:** implement `ICurrencyService` and `IExperienceService` in your game. Any other reward type gets its own
  `IRewardHandler<TReward>` (see below). Handlers are looked up by the exact reward type first, then by its base types.
- **Logging:** pass a `UnityEngine.ILogger` as the third constructor argument to route the manager's messages;
  the default is `Debug.unityLogger`.
- Events after a quest has finished are ignored; progress never exceeds the required count.

Register reward handlers **before** `Load()`: loading may advance a quest whose objective is already satisfied and grant rewards.

## Saving progress

Quest definitions are assets; player progress is stored separately as JSON (`JsonUtility`, no `PlayerPrefs`).

- `Capture()` / `Restore(QuestSaveData)` give you the data if your game has its own save system.
- `Save()` / `Load()` use the `IQuestSaveService` passed to the constructor. `FileQuestSaveService` writes
  `Application.persistentDataPath/quests.json` by default; the write is atomic (temporary file, then replace).
- A missing file loads as an empty state. A corrupted or empty file is not loaded: `Load()` returns `false`, the reason
  is logged and a copy is kept as `quests.json.corrupt`.
- Progress continues from the saved node; granted rewards are never granted again.
- Records of quests that are not in the database are kept as "orphaned" and written back unchanged.
- A quest that cannot continue (its saved node no longer exists, unknown state, or the graph now has errors) is
  **frozen** (`QuestInstance.IsFrozen`): it ignores events and its record is saved back unchanged.
- A save made by a newer format version is refused; the current state is kept.

## Custom objectives, conditions and rewards

A node type is a `[Serializable]`, non-generic class with a public parameterless constructor and the `[QuestNode]`
attribute. Put it anywhere in your project: the editor finds it through `TypeCache` and adds it to the node menu.

**Objective** — "talk to an NPC":

```csharp
using System;
using UnityEngine;
using Vokazhira.QuestEditor;

[Serializable]
[QuestNode("Objectives/Talk To NPC", "#43A047")]
public sealed class TalkObjective : ObjectiveNodeData
{
    [SerializeField, Tooltip("NPC identifier.")] private string npcId = string.Empty;

    public override int RequiredCount => 1;

    public override bool TryGetProgress(in QuestEvent e, out int delta) =>
        QuestObjectiveMatching.TryMatch(e, "NpcTalked", npcId, false, out delta);

    public override void Validate(QuestValidationContext context)
    {
        base.Validate(context);
        if (string.IsNullOrWhiteSpace(npcId)) context.AddError(QuestDiagnosticCodes.InvalidValue, "npcId is empty.");
    }

    public override string GetSummary() => $"Talk to {npcId}";
}

// In the game: quests.Report(new QuestEvent("NpcTalked", "elder"));
```

**Condition** — "another quest has failed". Conditions read quest states through `IQuestConditionContext`:

```csharp
using System;
using UnityEngine;
using Vokazhira.QuestEditor;

[Serializable]
[QuestNode("Conditions/Quest Failed", "#FB8C00")]
public sealed class QuestFailedCondition : ConditionNodeData
{
    [SerializeField, Tooltip("questId of the quest to check.")] private string questId = string.Empty;

    public override bool Evaluate(IQuestConditionContext context) =>
        context != null && context.GetState(questId) == QuestState.Failed;

    public override void Validate(QuestValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(questId)) context.AddError(QuestDiagnosticCodes.InvalidValue, "questId is empty.");
    }

    public override string GetSummary() => $"Quest '{questId}' failed?";
}
```

**Reward** — "give an item", with its handler:

```csharp
using System;
using UnityEngine;
using Vokazhira.QuestEditor;

[Serializable]
[QuestNode("Rewards/Item", "#6D4C41")]
public sealed class ItemReward : RewardNodeData
{
    [SerializeField] private string itemId = string.Empty;
    [SerializeField, Min(1)] private int count = 1;

    public string ItemId => itemId;
    public int Count => count;

    public override string GetSummary() => $"+{count} {itemId}";
}

public sealed class ItemRewardHandler : IRewardHandler<ItemReward>
{
    private readonly IInventory inventory;   // your game's inventory

    public ItemRewardHandler(IInventory inventory) => this.inventory = inventory;

    public bool TryGrant(ItemReward reward, string questId) => inventory.Add(reward.ItemId, reward.Count);
}

// quests.RegisterRewardHandler(new ItemRewardHandler(inventory));
```

## Renaming node classes: `[MovedFrom]`

Nodes are stored with `[SerializeReference]`, which records the class name, namespace and assembly. If you rename or
move a node class, existing quests lose that node (the validator reports `QE018`, and the editor names the missing class).
Keep the data by adding `[MovedFrom]` with the old location:

```csharp
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[QuestNode("Objectives/Talk To NPC")]
[MovedFrom(false, sourceNamespace: "MyGame.Quests", sourceAssembly: "MyGame", sourceClassName: "TalkToNpcObjective")]
public sealed class TalkObjective : ObjectiveNodeData { /* ... */ }
```

Do not use generic types as node classes.

## Sample

**Package Manager → Quest Editor → Samples → Basic Quest Example → Import.** The sample contains two quests
("The Elder's Letter" and the "Forest Cleanup" example above), a quest database and the `QuestDemo` scene: a bootstrap
that creates the `QuestManager` and simple gold/experience services, and an IMGUI interface with "Kill goblin" /
"Find letter" buttons, quest progress, Start / Claim / Abandon, gold and XP, event messages and Save / Load / reset.
**Tools → Quest Editor → Demo → Build Demo Assets** rebuilds the sample assets. The sample uses only the public API.

In the sample, gold and experience are not saved: the package saves only quest progress, and the demo services keep
their values in memory.

## Running the tests

The package has EditMode tests for the runtime logic (`Vokazhira.QuestEditor.Tests.Runtime`) and editor features
(`Vokazhira.QuestEditor.Tests.Editor`).

- **In the Editor:** **Window → General → Test Runner → EditMode → Run All**.
- **Command line** (the project must be closed in the Editor):

  ```
  "<Unity 2023.1.22f1>/Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -logFile Logs/tests-editmode.log
  ```

Tests of an embedded package appear automatically. For a package installed from a Git URL, add it to `testables`
in `Packages/manifest.json`: `"testables": ["com.vokazhira.quest-editor"]`.

## Limitations

- One active node per quest: no parallel branches; Flow cycles are not allowed.
- Conditions can read only quest states (`IQuestConditionContext.GetState`); no arbitrary expressions or game data yet.
- A quest can be started only from `Available`; completed or failed quests cannot be repeated.
- `IsOptional` on objectives is stored but not used.
- No copy/paste of nodes and no minimap in the graph editor.
- Unsaved changes in the editor do not survive an Editor restart (Unity asks to save them when closing).
- No localization: texts in quest assets are stored as plain strings.
- One save format version; no migrations yet.

## Roadmap

Parallel objectives, more conditions (numbers, inventory, object state), a Play Mode debugger window,
build-time quest validation, JSON export/import, localization.

## License

MIT — see [LICENSE.md](LICENSE.md).
