# Systems Completed & Next Steps

Date: 2026-02-07

## Summary of Current Codebase State

### Implemented Systems (Core + UI)
All systems and UI components listed in the implementation guide exist as scripts and have unit tests. Key systems are present:
- Core systems: Entity, Time/Energy, Economy, Reputation, Relationship, Detection, Job, Skill, Heat, Location, Intoxication, AdultContent, Body, Event, Inventory, Activity, Minigame.
- New components: InputManager, PlayerController, CameraController, HUDController, PhoneUI, MinigameUI, GameManager, InteractionSystem.

### Integration Status (High-Level)
- ActivitySystem -> MinigameSystem: wired (start/pause/resume/end + performance sync).
- ActivitySystem -> CameraController: wired for work activities (first-person toggles).
- ActivitySystem -> DetectionSystem: wired for work detection + JobSystem warnings.
- LocationSystem -> GameManager: wired (scene load via GameManager), guarded for build settings.
- HeatSystem ? DetectionSystem: wired patrol frequency and detection sensitivity.
- EventSystem: now uses RelationshipSystem for attended events without NPC duplication.

### Input System Status
- Project uses the new Input System.
- Legacy input access is gated (ENABLE_LEGACY_INPUT_MANAGER) in InputManager and ClickTargetsMinigame.
- There is still no native Input System binding layer; current runtime input will be inert unless legacy input is enabled.

### Scene/Playtest Status
- Scene loading is functional but requires scenes to be in Build Settings.
- Assets/Scenes now contains `CoreSystems.unity`, `Office.unity`, and `SampleScene.unity`.
- Build Settings currently include `CoreSystems`, `SampleScene`, and `Office`.
- `CoreSystems` has all core systems plus InputManager/GameManager/InteractionSystem and UI roots (HUD/Phone/Minigame UI scripts attached).
- `Office` includes player prefab instance, janitorial closet interaction (`job1`), exit door interaction (`apartment_player`), computer interaction, and 5 dirty spots.

## Remaining Next Steps (From COMPLETE_IMPLEMENTATION_GUIDE.md)

### 1) Run Integration Scenarios
- Core multitasking loop.
- Save/Load flow.
- Detection & consequences cascade.

### 2) Save/Load Expansion
- Currently only GameManager minimal save data exists.
- System-level SaveData APIs are still missing. These should be implemented before full save/load validation.

### 3) Native Input System Migration
- InputManager and ClickTargetsMinigame still rely on legacy input calls gated by `ENABLE_LEGACY_INPUT_MANAGER`.
- Add native Input System bindings and route gameplay/minigame input through them.

### 4) Remaining Spec-Compliance Gaps
- EventSystem event-minigame creation and richer scheduling.
- InteractionSystem shift-schedule/location validation.
- ActivitySystem skill mapping and work-pay derivation hardening.

## Known Discrepancies (Code vs Spec)

### High-Impact / Should Fix Soon
- PhoneUI uses the CreateActivity(playerId, type, context) overload only for drug dealing. Other app activities are not context-based yet.
- ActivitySystem work earnings use JobSystem when available but still fallback to fixed 20/hr; more accurate per-job rates should be driven by JobSystem data for all work activities.
- Skill mapping is still minigameId string heuristics; spec implies a more robust mapping from activity/minigame data.
- InteractionSystem job validation only checks employment + job active; does not validate shift schedule or location constraints.

### Medium-Impact / Spec Compliance
- EventSystem still lacks event minigame creation and more detailed scheduling (birthdays, recurring events beyond a simple schedule).
- HUDController uses GetCurrentGameTime/GetGameDate wrappers rather than spec-provided game date structures.
- EconomySystem lacks OnBalanceChanged event; HUD relies on manual updates.

### Editor Tools (Current State)
- Core/Office scene builders and prefab generators exist under `Assets/Editor`.
- DebugHelpers/TestDataSetup are aligned to runtime APIs and rely on specific IDs:
  - Locations: `office_main`, `apartment_player`
  - Job: `job1`
- Scenes must be in Build Settings for play-mode scene loading.

### Missing Systems (Spec References)
- CriminalRecordSystem
- CameraEffects
- VehicleSystem
- ClothingSystem
- PhoneSystem

## Discrepancies Document Review
`docs/IMPLEMENTATION_DISCREPANCIES.md` is accurate for the current codebase and highlights remaining gaps:
- Missing external systems (CriminalRecordSystem, VehicleSystem, ClothingSystem, PhoneSystem).
- Areas where enums exist but some systems still use generic values.
- Remaining integration/TODO items in runtime systems.

## Recommended Next Work (Pragmatic Order)
1) Run integration scenarios from the guide against current `CoreSystems` + `Office`.
2) Expand Save/Load with per-system data.
3) Migrate to native Input System bindings (avoid legacy input dependency).
4) Resolve highest-impact runtime TODOs (EventSystem minigames, InteractionSystem job validation, Activity pay/skill mapping).

---

If you want, I can:
- Create the CoreSystems scene scaffold
- Build the Office test scene
- Draft SaveData interfaces for all systems
- Update IMPLEMENTATION_DISCREPANCIES.md to reflect the latest changes
