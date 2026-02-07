# Systems Completed & Next Steps

Date: 2026-02-07

## Summary of Current Codebase State

### Implemented Systems (Core + UI)
All systems and UI components listed in the implementation guide exist as scripts and have unit tests. Key systems are present:
- Core systems: Entity, Time/Energy, Economy, Reputation, Relationship, Detection, Job, Skill, Heat, Location, Intoxication, AdultContent, Body, Event, Inventory, Activity, Minigame.
- New components: InputManager, PlayerController, CameraController, HUDController, PhoneUI, MinigameUI, GameManager, InteractionSystem.

### Integration Status (High-Level)
- ActivitySystem ? MinigameSystem: wired (start/pause/resume/end + performance sync).
- ActivitySystem ? CameraController: wired for work activities (first-person toggles).
- ActivitySystem ? DetectionSystem: wired for work detection + JobSystem warnings.
- LocationSystem ? GameManager: wired (scene load via GameManager), guarded for build settings.
- HeatSystem ? DetectionSystem: wired patrol frequency and detection sensitivity.
- EventSystem: now uses RelationshipSystem for attended events without NPC duplication.

### Input System Status
- Project uses the new Input System.
- Legacy input access is gated (ENABLE_LEGACY_INPUT_MANAGER) in InputManager and ClickTargetsMinigame.
- There is still no native Input System binding layer; current runtime input will be inert unless legacy input is enabled.

### Scene/Playtest Status
- Scene loading is functional but requires scenes to be in Build Settings.
- Only SampleScene exists in Assets/Scenes; no Office/Apartment test scenes yet.

## Remaining Next Steps (From COMPLETE_IMPLEMENTATION_GUIDE.md)

### 1) Build the CoreSystems + UI Scene
- Create a persistent scene with all systems on a DontDestroyOnLoad root.
- Build UI canvas containing HUDController, PhoneUI, MinigameUI.

### 2) Build Minimal Office Test Scene
- Environment: floor, walls, desk, janitorial closet, door, dirty spots.
- NPC: boss patrol + detection observer.
- Player: PlayerController + CameraController wired to target.

### 3) Run Integration Scenarios
- Core multitasking loop.
- Save/Load flow.
- Detection & consequences cascade.

### 4) Save/Load Expansion
- Currently only GameManager minimal save data exists.
- System-level SaveData APIs are still missing. These should be implemented before full save/load validation.

## Known Discrepancies (Code vs Spec)

### High-Impact / Should Fix Soon
- PhoneUI still uses current ActivitySystem signature; spec expects CreateActivity(playerId, type, context). We added an overload, but PhoneUI uses it only for drug dealing. Other app activities are not context-based yet.
- ActivitySystem work earnings use JobSystem when available but still fallback to fixed 20/hr; more accurate per-job rates should be driven by JobSystem data for all work activities.
- Skill mapping is still minigameId string heuristics; spec implies a more robust mapping from activity/minigame data.
- InteractionSystem job validation only checks employment + job active; does not validate shift schedule or location constraints.

### Medium-Impact / Spec Compliance
- EventSystem still lacks event minigame creation and more detailed scheduling (birthdays, recurring events beyond a simple schedule).
- HUDController uses GetCurrentGameTime/GetGameDate wrappers rather than spec-provided game date structures.
- EconomySystem lacks OnBalanceChanged event; HUD relies on manual updates.

### Missing Systems (Spec References)
- CriminalRecordSystem
- CameraEffects
- VehicleSystem
- ClothingSystem
- PhoneSystem

## Discrepancies Document Review
`docs/IMPLEMENTATION_DISCREPANCIES.md` is mostly accurate after recent updates, but it still lists several items that are now resolved:
- ActivitySystem wiring (minigame, camera, detection) is done.
- TimeEnergySystem missing APIs are resolved.
- JobSystem and RelationshipSystem missing APIs are resolved.

It still correctly calls out:
- Missing external systems (CriminalRecordSystem, VehicleSystem, ClothingSystem, PhoneSystem).
- Areas where enums exist but some systems still use generic values.
- Incomplete UI and scene scaffolding.

## Recommended Next Work (Pragmatic Order)
1) Build the CoreSystems + UI persistent scene.
2) Build Office test scene and wire interactions.
3) Run integration scenarios from the guide.
4) Expand Save/Load with per-system data.
5) Migrate to native Input System bindings (avoid legacy input dependency).

---

If you want, I can:
- Create the CoreSystems scene scaffold
- Build the Office test scene
- Draft SaveData interfaces for all systems
- Update IMPLEMENTATION_DISCREPANCIES.md to reflect the latest changes
