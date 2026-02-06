# Implementation Discrepancies and TODOs

This document tracks known mismatches between specs and the current implementation, plus explicit TODOs and deviations made to keep the project compiling and testable.

**Global / Cross-System**
- Namespaces in existing core systems are `Core` while newer systems are `HustleEconomy.Core`. This is inconsistent with the Master Implementation Guide.
- `TimeEnergySystem` is used instead of `TimeSystem`. Specs reference `TimeSystem.GetDeltaGameHours`, `ScheduleRecurringEvent`, and `CancelRecurringEvent`, which are not implemented.
- Systems referenced in specs but still missing: `CriminalRecordSystem`, `CameraEffects`, `MinigameSystem`, `VehicleSystem`, `ClothingSystem`, `PhoneSystem`.
- `EconomySystem` enums are missing spec values: `IncomeSource.SexWork`, `IncomeSource.SugarRelationship`, `ExpenseType.Blackmail`, `ExpenseType.Personal`.
- `JobSystem` missing APIs referenced in specs: `FireAllJobs(playerId, reason)`, `TriggerWarning(playerId, ...)`, `CheckTerminationForSexWork(playerId)`.
- `RelationshipSystem` missing APIs referenced in specs: `GetNPCsByType`, `GetNPC`, NPC `bodyPreferences`, and `PlayerAction.isPositive`.

**JobSystem (`Assets/Scripts/Core/JobSystem.cs`)**
- Uses `Core` namespace dependencies; spec expects `HustleEconomy.Core` across systems.
- `TriggerWarning` fires job-level warnings by `jobId` only, not `playerId` (spec references player-level warning in some contexts).
- Promotion uses `Random.Range(1.2f, 1.4f)`; tests assert the range.
- RelationshipSystem integration for promotions uses `FindAnyObjectByType<Core.RelationshipSystem>()` due to namespace mismatch.
- No ActivitySystem integration; minigame performance/detection are test overrides only.

**SkillSystem (`Assets/Scripts/Core/SkillSystem.cs`)**
- Added `ProcessSkillDecayForTesting` helper to avoid Update timing.
- No ActivitySystem integration; skills improve only via manual calls.
- NPC-specific unlocks only log via `Debug.Log` (no JobSystem unlock APIs exist).

**HeatSystem (`Assets/Scripts/Core/HeatSystem.cs`)**
- Added testing helpers: `SetHeatLevelForTesting`, `SetLastIncreaseForTesting`, `SetHasEvidenceForTesting`, `ProcessHeatDecayForTesting`, `ResolveAuditForTesting`.
- Patrol/detection multipliers tracked internally; no DetectionSystem API exists for patrol frequency or sensitivity.
- Audit freeze/unfreeze uses TODO logs; EconomySystem lacks freeze APIs.
- CriminalRecord/Event/Job consequences are TODO logs.
- Used `TimeEnergySystem` for timestamps; `GetDeltaGameHours` not available.
- Added a non-spec decay slow-down when time since last increase is < 1 day (`decayMultiplier = 0.5`).

**LocationSystem (`Assets/Scripts/Core/LocationSystem.cs`)**
- Added local `ActivityType` enum because ActivitySystem was missing at the time.
- Travel cost assumes taxi (`$15`), vehicle logic stubbed (always false).
- Scene load is a `Debug.Log`; actual scene loading not implemented.
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
- Blackmail payments use `ExpenseType.Other` (no `Blackmail` type).
- Escort and sugar income use `IncomeSource.Other` (missing `SexWork`/`SugarRelationship`).
- EventSystem, CriminalRecordSystem, JobSystem integrations are TODO logs.
- RelationshipSystem family reactions are stubbed via testing list.
- Sugar recurring allowance and obligations are TODO logs; added `TriggerSugarAllowanceForTesting`.
- Random outcomes are deterministic in tests via `SetForcedRandomForTesting`.

**BodySystem (`Assets/Scripts/Core/BodySystem.cs`)**
- Clothing contribution is TODO and uses a testing override; no ClothingSystem integration.
- NPC preferences are test overrides; RelationshipSystem lacks body preference data.
- Grooming costs use `ExpenseType.Other` (no `Personal` type).
- Grooming maintenance applied via explicit test helper rather than scheduled system.
- Maintenance now checks balance before charging; if unaffordable it downgrades without charging (prevents negative balances during maintenance).
- Added testing helpers: `SetFitnessForTesting`, `SetBodyTypeForTesting`, `SetGroomingForTesting`, `SetNpcPreferenceForTesting`, `SetClothingVanityForTesting`, `ProcessFitnessDecayForTesting`, `ApplyGroomingMaintenanceForTesting`, `GetStateForTesting`.

**EventSystem (`Assets/Scripts/Core/EventSystem.cs`)**
- Uses `TimeEnergySystem.ScheduleEvent` for reminders; no `GetDeltaGameHours` or recurring scheduling exists.
- Reminder triggering is public (`TriggerReminder`) to enable deterministic testing.
- Auto-generation uses `RelationshipSystem.CreateNPC` (side effect) to obtain name; spec expects `GetNPC` and should not create duplicates.
- `GetNextBirthday`/family member lists are not implemented; family birthdays are scheduled as `now + 30 days`.
- `ActivitySystem` integration is TODO (minigame creation).
- `RelationshipSystem.ObservePlayerAction` for attending events is TODO (missing ActionType in current system).

**InventorySystem (`Assets/Scripts/Core/InventorySystem.cs`)**
- Clothing equip effects are TODO only; no `ClothingSystem` integration and no `BodySystem.UpdateAppearance`.
- Vehicle and phone equip effects are TODO only (`VehicleSystem`, `PhoneSystem` missing).
- Item location access uses `LocationSystem.GetPlayerLocation`; no inventory/vehicle ownership checks beyond location access.
- `ExpenseType.Personal` is not used (EconomySystem lacks it).
- Inventory relies on `EntitySystem` ownership; internal `ownedItems` list is a cache.

**ActivitySystem (`Assets/Scripts/Core/ActivitySystem.cs`)**
- Minigame system integration is TODO (start/pause/resume/end logs only).
- Camera mode switching is TODO (no `CameraController`).
- Detection during work uses test flags only; no `DetectionSystem.CheckDetection` wiring.
- Skill XP always mapped to `SkillType.Social`; mapping from `minigameId` is TODO.
- Work earnings use a fixed hourly rate (`20f`); `JobSystem.GetCurrentJob` not implemented.
- `RelationshipSystem.ObservePlayerAction` for attending events is TODO in `EventSystem`, not ActivitySystem.
- Activity reminders and attention budget rely on internal `requiredAttention` heuristics; no `MinigameSystem` data.
- Test helpers: `SetMinigamePerformanceForTesting`, `SetDetectionForTesting`, `AdvanceActivityTimeForTesting`, `SetActivityPhaseForTesting`.
- `CreateActivity` assigns `playerId` to `"player"`; no multi-player support without API changes.

**Skill/Heat/Intoxication/Location Test Adjustments**
- Added explicit test helpers for deterministic outcomes (decay loops, DUI outcomes, patrol multipliers) because Update-driven logic and external integrations are not present.

If you want any of these gaps resolved, the best next step is to add the missing system APIs or align the enums and namespaces project-wide.
