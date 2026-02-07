# Implementation Discrepancies and TODOs

This document tracks known mismatches between specs and the current implementation, plus explicit TODOs and deviations made to keep the project compiling and testable.

**Global / Cross-System**
- `TimeEnergySystem` is used instead of `TimeSystem`. Spec-referenced APIs (`GetDeltaGameHours`, `ScheduleRecurringEvent`, `CancelRecurringEvent`) are now implemented on `TimeEnergySystem`.
- Systems referenced in specs but still missing: `CriminalRecordSystem`, `CameraEffects`, `VehicleSystem`, `ClothingSystem`, `PhoneSystem`.
- `EconomySystem` enums now include `IncomeSource.SexWork`, `IncomeSource.SugarRelationship`, `ExpenseType.Blackmail`, `ExpenseType.Personal`; key usages have been updated, but some subsystems still use `Other` by default.
- `JobSystem` now includes `FireAllJobs`, `TriggerWarning(playerId, ...)`, `CheckTerminationForSexWork`, and `GetCurrentJob`.
- `RelationshipSystem` now includes `GetNPCs`, `GetNPC`, NPC `bodyPreferences`, and `PlayerAction.isPositive`.
- Input handling: project uses the new Input System. `InputManager` and `ClickTargetsMinigame` now gate legacy input access behind `ENABLE_LEGACY_INPUT_MANAGER`, but no native Input System bindings exist yet.
- Scene loading: `LocationSystem` now calls `GameManager.LoadScene`; Build Settings currently include `CoreSystems`, `SampleScene`, and `Office`.

**InputManager (`Assets/Scripts/Core/InputManager.cs`)**
- Uses `Core` namespace; spec expects `HustleEconomy.Core`.
- Save/Load APIs from spec are not implemented.
- Legacy `UnityEngine.Input` is used for non-simulated input and is gated by `ENABLE_LEGACY_INPUT_MANAGER`.

**PlayerController (`Assets/Scripts/Core/PlayerController.cs`)**
- Uses `Core` namespace; spec expects `HustleEconomy.Core`.
- Save/Load APIs from spec are not implemented.
- Depends on `InputManager` (legacy input path) and `TimeEnergySystem` (Core namespace).

**CameraController (`Assets/Scripts/Core/CameraController.cs`)**
- Uses `Core` namespace; spec expects `HustleEconomy.Core`.
- Save/Load APIs from spec are not implemented.
- Auto-switch for work is now driven by `ActivitySystem` (work activities trigger first-person).
- PhoneUI integration is partially wired (phone open/close calls camera hooks).

**PhoneUI (`Assets/Scripts/UI/PhoneUI.cs`)**
- Implemented in `UI`, but depends on `Core.InputManager`/`Core.CameraController` (namespace mismatch vs spec expectations).
- Uses `TimeEnergySystem.GetCurrentGameTime()` and `GetGameDate()` wrappers.
- Uses `ActivitySystem.CreateActivity(playerId, ActivityType, context)` overload for phone activities, but `GetActiveActivity` is still not implemented.
- `RelationshipSystem.GetNPC/GetNPCs` is used for name lookup and contacts.
- `ShowNotification` only tracks a Messages notification flag; no per-app notification data beyond bools.

**JobSystem (`Assets/Scripts/Core/JobSystem.cs`)**
- Uses `Core` namespace dependencies; spec expects `HustleEconomy.Core` across systems.
- `TriggerWarning(playerId, ...)` now exists; job-level warnings are `TriggerWarningForJob`.
- Promotion uses `Random.Range(1.2f, 1.4f)`; tests assert the range.
- RelationshipSystem integration for promotions uses `FindAnyObjectByType<Core.RelationshipSystem>()` due to namespace mismatch.
- No ActivitySystem integration; minigame performance/detection are test overrides only.

**SkillSystem (`Assets/Scripts/Core/SkillSystem.cs`)**
- Added `ProcessSkillDecayForTesting` helper to avoid Update timing.
- No ActivitySystem integration; skills improve only via manual calls.
- NPC-specific unlocks only log via `Debug.Log` (no JobSystem unlock APIs exist).

**HeatSystem (`Assets/Scripts/Core/HeatSystem.cs`)**
- Added testing helpers: `SetHeatLevelForTesting`, `SetLastIncreaseForTesting`, `SetHasEvidenceForTesting`, `ProcessHeatDecayForTesting`, `ResolveAuditForTesting`.
- Patrol/detection multipliers are wired to `DetectionSystem.SetPatrolFrequency`/`SetDetectionSensitivity`.
- Audit freeze/unfreeze uses TODO logs; EconomySystem lacks freeze APIs.
- CriminalRecord/Event/Job consequences are TODO logs.
- Uses `TimeEnergySystem` for timestamps; `GetDeltaGameHours` exists but is not used here.
- Added a non-spec decay slow-down when time since last increase is < 1 day (`decayMultiplier = 0.5`).

**LocationSystem (`Assets/Scripts/Core/LocationSystem.cs`)**
- Added local `ActivityType` enum because ActivitySystem was missing at the time.
- Travel cost assumes taxi (`$15`), vehicle logic stubbed (always false).
- Scene load now calls `GameManager.LoadScene`, which requires scenes to be in Build Settings.
- Invitation system is TODO (always returns false).
- Added test helpers: `CreateLocation`, `SetPlayerLocationForTesting`, `AddBannedPlayerForTesting`, `TriggerLocationLockedForTesting`.

**IntoxicationSystem (`Assets/Scripts/Core/IntoxicationSystem.cs`)**
- DUI detection uses forced test override instead of `DetectionSystem.GetObserver`.
- CriminalRecord integration is TODO.
- Uses `EconomySystem.ExpenseType.Fine` for DUI, which exists; reputation hit applied.
- Visual effects integration is TODO.
- Added testing helpers: `CreateConsumableItem`, `SetIntoxicationLevelForTesting`, `SetLicenseStatusForTesting`, `SetDrivingStatusForTesting`, `SetDuiCatchResultForTesting`, `ProcessMetabolismForTesting`.

**AdultContentSystem (`Assets/Scripts/Core/AdultContentSystem.cs`)**
- Uses test-registered clothing items (EntitySystem retrieval is optional; no ClothingSystem).
- Blackmail payments use `ExpenseType.Blackmail`.
- Escort and sugar income use `IncomeSource.SexWork`/`IncomeSource.SugarRelationship`.
- EventSystem, CriminalRecordSystem, JobSystem integrations are TODO logs.
- RelationshipSystem family reactions are stubbed via testing list.
- Sugar recurring allowance and obligations are TODO logs; added `TriggerSugarAllowanceForTesting`.
- Random outcomes are deterministic in tests via `SetForcedRandomForTesting`.

**BodySystem (`Assets/Scripts/Core/BodySystem.cs`)**
- Clothing contribution is TODO and uses a testing override; no ClothingSystem integration.
- NPC preferences are test overrides; RelationshipSystem lacks body preference data.
- Grooming costs use `ExpenseType.Personal`.
- Grooming maintenance applied via explicit test helper rather than scheduled system.
- Maintenance now checks balance before charging; if unaffordable it downgrades without charging (prevents negative balances during maintenance).
- Added testing helpers: `SetFitnessForTesting`, `SetBodyTypeForTesting`, `SetGroomingForTesting`, `SetNpcPreferenceForTesting`, `SetClothingVanityForTesting`, `ProcessFitnessDecayForTesting`, `ApplyGroomingMaintenanceForTesting`, `GetStateForTesting`.

**EventSystem (`Assets/Scripts/Core/EventSystem.cs`)**
- Uses `TimeEnergySystem.ScheduleEvent` for reminders; recurring scheduling is available but not used here.
- Reminder triggering is public (`TriggerReminder`) to enable deterministic testing.
- Auto-generation uses `RelationshipSystem.GetNPC` and avoids creating duplicates.
- `GetNextBirthday`/family member lists are not implemented; family birthdays are scheduled as `now + 30 days`.
- `ActivitySystem` integration is TODO (minigame creation).
- `RelationshipSystem.ObservePlayerAction` for attending events is now called (AttendedEvent action added with zero impact to avoid double counting).

**InventorySystem (`Assets/Scripts/Core/InventorySystem.cs`)**
- Clothing equip effects are TODO only; no `ClothingSystem` integration and no `BodySystem.UpdateAppearance`.
- Vehicle and phone equip effects are TODO only (`VehicleSystem`, `PhoneSystem` missing).
- Item location access uses `LocationSystem.GetPlayerLocation`; no inventory/vehicle ownership checks beyond location access.
- `ExpenseType.Personal` exists but is not used in InventorySystem flows.
- Inventory relies on `EntitySystem` ownership; internal `ownedItems` list is a cache.

**ActivitySystem (`Assets/Scripts/Core/ActivitySystem.cs`)**
- Minigame system integration is wired (start/pause/resume/end and performance sync).
- Camera mode switching is wired for work activities (first-person on/off).
- Detection during work is wired to `DetectionSystem.CheckDetection` with test overrides.
- Skill XP mapping is heuristic based on `minigameId`.
- Work earnings use `JobSystem.GetCurrentJob` when available, otherwise fall back to 20/hr.
- `RelationshipSystem.ObservePlayerAction` for attending events is TODO in `EventSystem`, not ActivitySystem.
- Activity reminders and attention budget rely on internal `requiredAttention` heuristics; no `MinigameSystem` data.
- Test helpers: `SetMinigamePerformanceForTesting`, `SetDetectionForTesting`, `AdvanceActivityTimeForTesting`, `SetActivityPhaseForTesting`.
- `CreateActivity` assigns `playerId` to `"player"`; no multi-player support without API changes.

**MinigameSystem (`Assets/Scripts/Core/MinigameSystem.cs`)**
- Only `ClickTargets` is implemented; all other types use `StubMinigame` (per spec, others deferred).
- ClickTargets is logic-only; UI creation and destruction are TODOs.
- Input handling is not used in tests; `SimulateClickForTesting` bypasses Unity input.
- `MinigameInstance.startTime` uses `TimeEnergySystem.GetCurrentTime()` when available (fallback to `DateTime.UtcNow`).
- Performance changes use the >5 threshold, but tests advance time via `AdvanceMinigameTimeForTesting`.
- `PauseMinigame`/`ResumeMinigame` are state-only; no UI pause state or input gating beyond state.
- ActivitySystem now calls `MinigameSystem.StartMinigame/GetPerformance/EndMinigame`.

**MinigameUI (`Assets/Scripts/UI/MinigameUI.cs`)**
- Depends on `Core.InputManager`/`Core.CameraController` and `Core.ActivitySystem` (namespace mismatch vs spec).
- Uses `MinigameSystem.StartMinigame(minigameId, activityId)`; spec expects a config-based Start method (no config support in current MinigameSystem).
- ClickTargets UI uses placeholder target positions and does not read target data from MinigameSystem behavior (MinigameSystem does not expose targets).
- Input routing updates MinigameSystem counters directly (no `RecordAction` API exists).
- Context mapping is simplified: `ActivityType.Physical -> WorldSpace`, `Screen -> PhoneScreen`, `Passive -> Fullscreen` (no rich context strings in ActivitySystem).

**HUDController (`Assets/Scripts/UI/HUDController.cs`)**
- Uses `TimeEnergySystem.GetCurrentGameTime()` and `GetGameDate()` wrappers.
- Energy uses `TimeEnergySystem.GetEnergyLevel()` with a fixed max of `100f`; no `GetEnergy(playerId)`/`GetMaxEnergy(playerId)` APIs exist.
- EconomySystem has no `OnBalanceChanged`, so HUD relies on manual `UpdateMoneyDisplay` calls or test helper.
- Activity display uses `GetActiveActivities("player")` instead of spec???s `GetActiveActivity`.
- PhoneUI events are wired when available; no HUD prompt system implemented.

**GameManager (`Assets/Scripts/Core/GameManager.cs`)**
- Uses `Core` namespace; spec expects `HustleEconomy.Core`.
- Initializes systems via `Instance.ToString()` (no explicit `Initialize()` in most systems); no strict ordering enforcement beyond access.
- Save/Load only writes minimal `GameSaveData` (version, date, playtime, currentScene). No system `SaveData` integration (APIs missing).
- Scene loading is no-op in EditMode and does not notify LocationSystem (`OnSceneLoaded` API missing).
- Pause/Resume uses `InputManager` context UI and `Time.timeScale`; no pause menu integration.

**InteractionSystem (`Assets/Scripts/Core/InteractionSystem.cs`)**
- Uses `Core` namespace; spec expects `HustleEconomy.Core`.
- Uses `InventorySystem.AddItem/RemoveItem` (no `PickupItem/DropItem` API exists).
- Job validation checks `HasJob` and job active; travel validation uses `LocationSystem.CanTravelTo`.
- `HandleExamine` uses `Entity.id` and generic message (Entity has no `name`/`description` fields).
- `LocationSystem.TravelToLocation` is used (spec references `TravelTo`).
- Prompt integration with `HUDController` is not implemented (no HUD prompt APIs).
**Skill/Heat/Intoxication/Location Test Adjustments**
- Added explicit test helpers for deterministic outcomes (decay loops, DUI outcomes, patrol multipliers) because Update-driven logic and external integrations are not present.

**Editor Tools (`Assets/Editor/*.cs`)**
- Core scene and office scene builders exist and align with current runtime APIs.
- DebugHelpers/TestDataSetup now use runtime APIs instead of removed helpers (SetBalance/SetGameTimeForTesting, etc.).
- Editor tools rely on scenes being present in Build Settings and consistent IDs (`office_main`, `apartment_player`, `job1`).

If you want any of these gaps resolved, the best next step is to align systems with the new APIs/enums and complete the remaining missing systems.

**Remaining Steps From COMPLETE_IMPLEMENTATION_GUIDE.md**
- Integration API additions are complete, but several systems still need to use the new APIs/enums.
- Integration wiring is complete for:
  - ActivitySystem -> MinigameSystem (start/pause/resume/end and performance sync).
  - ActivitySystem -> CameraController (auto-switch for first-person activities).
  - ActivitySystem -> DetectionSystem (work detection and warning flow).
  - LocationSystem -> GameManager (scene load).
- UI/Scene setup (manual project steps):
  - Verify and polish existing `CoreSystems` and `Office` scene wiring (references, prompts, detection flow).
  - Add/iterate additional scenes (for example Apartment) as needed for travel loops.
  - Hook and validate PlayerController/CameraController targets in each playable scene.
- Integration tests and playtest loop after wiring is complete.

