using System;
using System.Collections.Generic;
using UnityEngine;
using HustleEconomy.Data;

namespace Core
{
    public class AdultContentSystem : MonoBehaviour
    {
        public enum ClothingCategory
        {
            Professional,
            Casual,
            Provocative,
            Lingerie,
            Fetish,
            Nude
        }

        public enum ClothingModifierType
        {
            ProfessionalRep,
            SocialRep,
            OnlyFansIncome,
            Attractiveness
        }

        public enum BlackmailType
        {
            NudePhotos,
            StreamContent,
            WitnessedSexWork,
            IntimateVideo
        }

        public enum ClientType
        {
            Safe,
            Demanding,
            Dangerous,
            Police
        }

        public enum ClientSatisfaction
        {
            Disappointed,
            Satisfied,
            Delighted
        }

        public enum BlackmailResponse
        {
            Pay,
            Refuse,
            Violence
        }

        public enum SugarEndReason
        {
            PlayerChoice,
            BenefactorEnded
        }

        [System.Serializable]
        public class ClothingItem : Entity
        {
            public ClothingCategory category;
            public float professionalMod;
            public float socialMod;
            public float vanityValue;
            public float onlyFansIncomeMod;
            public float attractivenessMod;
            public bool allowedInOffice;
            public bool allowedInPublic;
        }

        [System.Serializable]
        public struct BlackmailEvent
        {
            public string blackmailerId;
            public BlackmailType type;
            public float demand;
            public DateTime deadline;
            public bool playerPaid;
            public bool contentLeaked;
            public bool violentResponse;
        }

        [System.Serializable]
        public struct AppointmentResult
        {
            public float earnings;
            public bool safetyIssue;
            public bool policeInvolved;
            public float reputationDamage;
            public ClientSatisfaction satisfaction;
        }

        [System.Serializable]
        public struct SugarTerms
        {
            public float monthlyAllowance;
            public int hoursPerWeek;
            public bool intimacyExpected;
            public bool exclusivityRequired;
            public bool publicAppearances;
        }

        private static AdultContentSystem instance;
        public static AdultContentSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<AdultContentSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AdultContentSystem");
                        instance = go.AddComponent<AdultContentSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string> OnClothingChanged;
        public event Action<string, BlackmailType, float> OnBlackmailTriggered;
        public event Action<AppointmentResult> OnEscortAppointmentComplete;
        public event Action<string, SugarTerms> OnSugarRelationshipStarted;
        public event Action<string> OnSugarRelationshipEnded;
        public event Action<string> OnContentLeaked;
        public event Action<string> OnFamilyDiscoveredSexWork;

        private Dictionary<string, ClothingItem> clothingItems;
        private Dictionary<string, BlackmailEvent> activeBlackmail;
        private Dictionary<string, SugarTerms> activeSugarRelationships;
        private List<string> familyNpcIdsForTesting;
        private ClothingItem currentClothing;
        private string playerId = "player";
        private float bodyAttractiveness = 0f;

        private bool useForcedRandom = false;
        private float forcedRandomValue = 0.5f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            clothingItems = new Dictionary<string, ClothingItem>();
            activeBlackmail = new Dictionary<string, BlackmailEvent>();
            activeSugarRelationships = new Dictionary<string, SugarTerms>();
            familyNpcIdsForTesting = new List<string>();
            currentClothing = null;
            bodyAttractiveness = 0f;
        }

        public void EquipClothing(string clothingId)
        {
            ClothingItem item = GetClothingItem(clothingId);
            currentClothing = item;

            string currentLocation = LocationSystem.Instance.GetPlayerLocation(playerId);
            ApplyClothingReputationEffects(item, currentLocation);

            OnClothingChanged?.Invoke(clothingId);
        }

        public float GetClothingModifier(ClothingModifierType type)
        {
            if (currentClothing == null)
            {
                if (type == ClothingModifierType.OnlyFansIncome)
                {
                    return 2.5f * GetBodyAttractivenessMultiplier();
                }

                if (type == ClothingModifierType.ProfessionalRep)
                {
                    return -50f;
                }

                return 0f;
            }

            switch (type)
            {
                case ClothingModifierType.ProfessionalRep:
                    return currentClothing.professionalMod;
                case ClothingModifierType.SocialRep:
                    return currentClothing.socialMod;
                case ClothingModifierType.OnlyFansIncome:
                    return currentClothing.onlyFansIncomeMod * GetBodyAttractivenessMultiplier();
                case ClothingModifierType.Attractiveness:
                    return currentClothing.attractivenessMod;
                default:
                    return 0f;
            }
        }

        public void TriggerBlackmail(string blackmailerId, BlackmailType type, float demand)
        {
            BlackmailEvent blackmail = new BlackmailEvent
            {
                blackmailerId = blackmailerId,
                type = type,
                demand = demand,
                deadline = TimeEnergySystem.Instance.GetCurrentTime().AddDays(7),
                playerPaid = false,
                contentLeaked = false,
                violentResponse = false
            };

            activeBlackmail[blackmailerId] = blackmail;
            OnBlackmailTriggered?.Invoke(blackmailerId, type, demand);
            Debug.LogWarning("TODO: EventSystem blackmail_decision");
        }

        public void RespondToBlackmail(string blackmailerId, BlackmailResponse response)
        {
            if (!activeBlackmail.TryGetValue(blackmailerId, out BlackmailEvent blackmail))
            {
                Debug.LogWarning($"RespondToBlackmail: no active blackmail from {blackmailerId}");
                return;
            }

            switch (response)
            {
                case BlackmailResponse.Pay:
                    bool canAfford = EconomySystem.Instance.DeductExpense(
                        playerId,
                        blackmail.demand,
                        EconomySystem.ExpenseType.Other,
                        $"Blackmail payment to {blackmailerId}"
                    );

                    if (canAfford)
                    {
                        blackmail.playerPaid = true;
                    }
                    else
                    {
                        blackmail.contentLeaked = true;
                        LeakContent(blackmail.type);
                    }
                    break;
                case BlackmailResponse.Refuse:
                    blackmail.contentLeaked = true;
                    LeakContent(blackmail.type);
                    break;
                case BlackmailResponse.Violence:
                    blackmail.violentResponse = true;
                    bool success = GetRandomValue() > 0.5f;
                    if (success)
                    {
                        ReputationSystem.Instance.ModifyReputation(
                            playerId,
                            ReputationSystem.ReputationTrack.Criminal,
                            5f,
                            "Violent resolution"
                        );
                    }
                    else
                    {
                        Debug.LogWarning("TODO: CriminalRecordSystem AddOffense (Assault)");
                        LeakContent(blackmail.type);
                    }
                    break;
            }

            activeBlackmail.Remove(blackmailerId);
        }

        public AppointmentResult StartEscortAppointment(ClientType clientType)
        {
            AppointmentResult result = new AppointmentResult
            {
                earnings = 0f,
                safetyIssue = false,
                policeInvolved = false,
                reputationDamage = 0f,
                satisfaction = ClientSatisfaction.Satisfied
            };

            float baseEarnings = 500f;
            float durationHours = GetRandomRange(1f, 3f);
            TimeEnergySystem.Instance.AdvanceTime(durationHours * 60f);

            switch (clientType)
            {
                case ClientType.Safe:
                    result.earnings = baseEarnings;
                    result.safetyIssue = false;
                    result.policeInvolved = false;
                    result.satisfaction = ClientSatisfaction.Satisfied;
                    break;
                case ClientType.Demanding:
                    bool complied = GetRandomValue() > 0.5f;
                    result.earnings = complied ? baseEarnings * 1.5f : baseEarnings * 0.7f;
                    result.safetyIssue = GetRandomValue() < 0.2f;
                    result.satisfaction = complied ? ClientSatisfaction.Delighted : ClientSatisfaction.Disappointed;
                    break;
                case ClientType.Dangerous:
                    result.safetyIssue = GetRandomValue() < 0.7f;
                    if (result.safetyIssue)
                    {
                        result.earnings = 0f;
                        TimeEnergySystem.Instance.ModifyEnergy(-50f, "Escorting violence");
                        Debug.LogWarning("TODO: EventSystem escort_violence");
                        result.satisfaction = ClientSatisfaction.Disappointed;
                    }
                    else
                    {
                        result.earnings = baseEarnings * 2f;
                        result.satisfaction = ClientSatisfaction.Delighted;
                    }
                    break;
                case ClientType.Police:
                    result.policeInvolved = true;
                    result.earnings = 0f;
                    Debug.LogWarning("TODO: CriminalRecordSystem AddOffense (Prostitution)");
                    ReputationSystem.Instance.ModifyReputation(
                        playerId,
                        ReputationSystem.ReputationTrack.Legal,
                        -25f,
                        "Prostitution arrest"
                    );
                    TimeEnergySystem.Instance.AdvanceTime(24f * 60f);
                    result.satisfaction = ClientSatisfaction.Disappointed;
                    break;
            }

            if (result.earnings > 0f)
            {
                EconomySystem.Instance.AddIncome(
                    playerId,
                    result.earnings,
                    EconomySystem.IncomeSource.Other,
                    "Escort appointment"
                );
            }

            result.reputationDamage = 5f;
            ReputationSystem.Instance.ModifyReputation(
                playerId,
                ReputationSystem.ReputationTrack.Social,
                -result.reputationDamage,
                "Sex work"
            );

            OnEscortAppointmentComplete?.Invoke(result);
            return result;
        }

        public void StartSugarRelationship(string benefactorId, SugarTerms terms)
        {
            activeSugarRelationships[benefactorId] = terms;

            Debug.LogWarning("TODO: Schedule recurring allowance");
            Debug.LogWarning("TODO: Schedule sugar obligations");

            RelationshipSystem.Instance.ModifyRelationship(benefactorId, 30f, "Sugar relationship started");
            OnSugarRelationshipStarted?.Invoke(benefactorId, terms);
        }

        public void EndSugarRelationship(string benefactorId, SugarEndReason reason)
        {
            if (!activeSugarRelationships.ContainsKey(benefactorId))
            {
                return;
            }

            activeSugarRelationships.Remove(benefactorId);
            Debug.LogWarning("TODO: Cancel recurring allowance");

            float relationshipChange = reason == SugarEndReason.PlayerChoice ? -10f : -30f;
            RelationshipSystem.Instance.ModifyRelationship(benefactorId, relationshipChange, "Sugar relationship ended");

            if (reason == SugarEndReason.BenefactorEnded)
            {
                if (GetRandomValue() < 0.3f)
                {
                    TriggerBlackmail(benefactorId, BlackmailType.IntimateVideo, GetRandomRange(5000f, 20000f));
                }
            }

            OnSugarRelationshipEnded?.Invoke(benefactorId);
        }

        public void TriggerSugarAllowanceForTesting(string benefactorId)
        {
            if (!activeSugarRelationships.TryGetValue(benefactorId, out SugarTerms terms))
            {
                return;
            }

            EconomySystem.Instance.AddIncome(
                playerId,
                terms.monthlyAllowance,
                EconomySystem.IncomeSource.Other,
                $"Monthly allowance from {benefactorId}"
            );
        }

        public void RegisterClothingItemForTesting(ClothingItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
            {
                return;
            }

            clothingItems[item.id] = item;
        }

        public void AddFamilyNpcForTesting(string npcId)
        {
            if (!familyNpcIdsForTesting.Contains(npcId))
            {
                familyNpcIdsForTesting.Add(npcId);
            }
        }

        public void SetPlayerIdForTesting(string id)
        {
            playerId = string.IsNullOrEmpty(id) ? "player" : id;
        }

        public void SetBodyAttractivenessForTesting(float value)
        {
            bodyAttractiveness = Mathf.Clamp(value, 0f, 100f);
        }

        public void SetForcedRandomForTesting(float value)
        {
            useForcedRandom = true;
            forcedRandomValue = Mathf.Clamp01(value);
        }

        public void ClearForcedRandomForTesting()
        {
            useForcedRandom = false;
        }

        private ClothingItem GetClothingItem(string clothingId)
        {
            if (string.IsNullOrEmpty(clothingId))
            {
                return null;
            }

            if (clothingItems.TryGetValue(clothingId, out ClothingItem item))
            {
                return item;
            }

            Entity entity = EntitySystem.Instance.GetEntity(clothingId);
            return entity as ClothingItem;
        }

        private void ApplyClothingReputationEffects(ClothingItem item, string locationId)
        {
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(locationId))
            {
                return;
            }

            string location = locationId.ToLowerInvariant();
            if (location.Contains("office"))
            {
                if (item.category == ClothingCategory.Professional)
                {
                    ReputationSystem.Instance.ModifyReputation(playerId, ReputationSystem.ReputationTrack.Professional, 2f, "Professional attire");
                }
                else if (item.category == ClothingCategory.Provocative || item.category == ClothingCategory.Lingerie || item.category == ClothingCategory.Nude)
                {
                    ReputationSystem.Instance.ModifyReputation(playerId, ReputationSystem.ReputationTrack.Professional, -10f, "Inappropriate attire");
                    Debug.LogWarning("TODO: JobSystem TriggerWarning (dress code)");
                }
            }
            else if (location.Contains("club"))
            {
                if (item.category == ClothingCategory.Provocative)
                {
                    ReputationSystem.Instance.ModifyReputation(playerId, ReputationSystem.ReputationTrack.Social, 5f, "Stylish club attire");
                }
            }
        }

        private void LeakContent(BlackmailType type)
        {
            OnContentLeaked?.Invoke(type.ToString());

            ReputationSystem.Instance.ModifyReputation(playerId, ReputationSystem.ReputationTrack.Social, -30f, "Content leaked");
            ReputationSystem.Instance.ModifyReputation(playerId, ReputationSystem.ReputationTrack.Professional, -40f, "Adult content exposed");

            foreach (string npcId in familyNpcIdsForTesting)
            {
                OnFamilyDiscoveredSexWork?.Invoke(npcId);
                RelationshipSystem.Instance.ModifyRelationship(npcId, -30f, "Adult content leaked");
            }

            Debug.LogWarning("TODO: RelationshipSystem family NPC reactions");
            Debug.LogWarning("TODO: JobSystem CheckTerminationForSexWork");
        }

        private float GetRandomValue()
        {
            return useForcedRandom ? forcedRandomValue : UnityEngine.Random.value;
        }

        private float GetRandomRange(float min, float max)
        {
            if (useForcedRandom)
            {
                return min + (max - min) * forcedRandomValue;
            }

            return UnityEngine.Random.Range(min, max);
        }

        private float GetBodyAttractivenessMultiplier()
        {
            return 1f + (bodyAttractiveness / 100f) * 0.5f;
        }
    }
}
