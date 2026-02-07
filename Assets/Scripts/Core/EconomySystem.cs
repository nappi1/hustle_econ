using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Data;

namespace Core
{
    public class EconomySystem : MonoBehaviour
    {
        [System.Serializable]
        public class Transaction
        {
            public string id;
            public DateTime timestamp;
            public string fromId;
            public string toId;
            public float amount;
            public TransactionType type;
            public IncomeSource source;
            public ExpenseType expenseType;
            public string entityId;
            public string description;
            public bool isLegal;
        }

        [System.Serializable]
        public class WealthProfile
        {
            public string playerId;
            public float balance;
            public float legalIncome;
            public float illegalIncome;
            public float unexplainedIncome;
            public List<Transaction> history;
            public float legitimacyScore;
        }

        public enum IncomeSource
        {
            Salary,
            DrugSale,
            Investment,
            BusinessProfit,
            Gift,
            Theft,
            Gambling,
            SexWork,
            SugarRelationship,
            Other
        }

        public enum ExpenseType
        {
            Rent,
            Utilities,
            Food,
            Transportation,
            Fine,
            Purchase,
            LoanPayment,
            Bribe,
            Blackmail,
            Personal,
            Other
        }

        public enum TransactionType
        {
            Income,
            Expense
        }

        private static EconomySystem instance;
        public static EconomySystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<EconomySystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EconomySystem");
                        instance = go.AddComponent<EconomySystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string, float, IncomeSource> OnIncomeReceived;
        public event Action<string, float, ExpenseType> OnExpensePaid;
        public event Action<string, float> OnDebt;
        public event Action<Transaction> OnTransactionComplete;
        public event Action<string, float, string> OnBillDue;

        private Dictionary<string, WealthProfile> profiles;

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
            profiles = new Dictionary<string, WealthProfile>();
        }

        public float GetBalance(string playerId)
        {
            WealthProfile profile = GetOrCreateProfile(playerId);
            return profile.balance;
        }

        public void AddIncome(string playerId, float amount, IncomeSource source, string description)
        {
            if (amount < 0f)
            {
                Debug.LogWarning("AddIncome: negative amount treated as expense");
                DeductExpense(playerId, -amount, ExpenseType.Other, description);
                return;
            }

            WealthProfile profile = GetOrCreateProfile(playerId);
            profile.balance += amount;

            switch (source)
            {
                case IncomeSource.Salary:
                case IncomeSource.Investment:
                case IncomeSource.BusinessProfit:
                case IncomeSource.Gambling:
                case IncomeSource.Gift:
                case IncomeSource.SugarRelationship:
                    profile.legalIncome += amount;
                    break;
                case IncomeSource.DrugSale:
                case IncomeSource.Theft:
                case IncomeSource.SexWork:
                    profile.illegalIncome += amount;
                    break;
                case IncomeSource.Other:
                    if (amount > 5000f)
                    {
                        profile.unexplainedIncome += amount;
                    }
                    else
                    {
                        profile.legalIncome += amount;
                    }
                    break;
            }

            RecalculateLegitimacy(profile);

            Transaction transaction = CreateTransaction(
                playerId,
                "system",
                amount,
                TransactionType.Income,
                source,
                ExpenseType.Other,
                null,
                description,
                IsLegalIncome(source)
            );

            profile.history.Add(transaction);

            OnIncomeReceived?.Invoke(playerId, amount, source);
            OnTransactionComplete?.Invoke(transaction);
        }

        public bool DeductExpense(string playerId, float amount, ExpenseType type, string description)
        {
            if (amount < 0f)
            {
                Debug.LogWarning("DeductExpense: negative amount treated as income");
                AddIncome(playerId, -amount, IncomeSource.Other, description);
                return true;
            }

            WealthProfile profile = GetOrCreateProfile(playerId);
            bool wasInDebt = profile.balance < 0f;
            bool canAfford = profile.balance >= amount;

            profile.balance -= amount;

            Transaction transaction = CreateTransaction(
                playerId,
                "system",
                amount,
                TransactionType.Expense,
                IncomeSource.Other,
                type,
                null,
                description,
                type != ExpenseType.Bribe && type != ExpenseType.Blackmail
            );

            profile.history.Add(transaction);

            OnExpensePaid?.Invoke(playerId, amount, type);
            OnTransactionComplete?.Invoke(transaction);

            if (!wasInDebt && profile.balance < 0f)
            {
                OnDebt?.Invoke(playerId, -profile.balance);
            }

            return canAfford;
        }

        public bool ProcessTransaction(string buyerId, string sellerId, string entityId, float price)
        {
            if (string.IsNullOrEmpty(buyerId) || string.IsNullOrEmpty(sellerId))
            {
                Debug.LogWarning("ProcessTransaction: buyerId or sellerId is null or empty");
                return false;
            }

            if (buyerId == sellerId)
            {
                return true;
            }

            WealthProfile buyerProfile = GetOrCreateProfile(buyerId);
            if (buyerProfile.balance < price)
            {
                return false;
            }

            Entity entity = EntitySystem.Instance.GetEntity(entityId);
            if (entity == null)
            {
                Debug.LogWarning($"ProcessTransaction: Entity {entityId} not found");
                return false;
            }

            if (sellerId != "system" && !string.Equals(entity.owner, sellerId, StringComparison.Ordinal))
            {
                Debug.LogWarning("ProcessTransaction: seller does not own entity");
                return false;
            }

            buyerProfile.balance -= price;

            if (sellerId != "system")
            {
                WealthProfile sellerProfile = GetOrCreateProfile(sellerId);
                sellerProfile.balance += price;

                Transaction sellerTransaction = CreateTransaction(
                    sellerId,
                    buyerId,
                    price,
                    TransactionType.Income,
                    IncomeSource.Other,
                    ExpenseType.Other,
                    entityId,
                    $"Sale of {entityId}",
                    true
                );
                sellerProfile.history.Add(sellerTransaction);
                OnTransactionComplete?.Invoke(sellerTransaction);
            }

            EntitySystem.Instance.TransferOwnership(entityId, buyerId);

            Transaction buyerTransaction = CreateTransaction(
                buyerId,
                sellerId,
                price,
                TransactionType.Expense,
                IncomeSource.Other,
                ExpenseType.Purchase,
                entityId,
                $"Purchase of {entityId}",
                true
            );

            buyerProfile.history.Add(buyerTransaction);

            OnTransactionComplete?.Invoke(buyerTransaction);
            return true;
        }

        public float GetLegitimacyScore(string playerId)
        {
            WealthProfile profile = GetOrCreateProfile(playerId);
            return profile.legitimacyScore;
        }

        public List<Transaction> GetTransactionHistory(string playerId, int limit = 50)
        {
            WealthProfile profile = GetOrCreateProfile(playerId);
            if (limit <= 0)
            {
                return new List<Transaction>();
            }

            return profile.history
                .OrderByDescending(entry => entry.timestamp)
                .Take(limit)
                .ToList();
        }

        public float CalculateWealth(string playerId)
        {
            WealthProfile profile = GetOrCreateProfile(playerId);
            float cash = profile.balance;
            float entityValue = 0f;

            List<Entity> ownedEntities = EntitySystem.Instance.GetEntitiesByOwner(playerId);
            foreach (Entity entity in ownedEntities)
            {
                entityValue += EntitySystem.Instance.GetEntityValue(entity.id);
            }

            return cash + entityValue;
        }

        public void TriggerBill(string playerId, float amount, string billType)
        {
            OnBillDue?.Invoke(playerId, amount, billType);
            DeductExpense(playerId, amount, ExpenseType.Rent, $"{billType} payment");
        }

        private WealthProfile GetOrCreateProfile(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("GetOrCreateProfile: playerId is null or empty");
                playerId = "player";
            }

            if (!profiles.TryGetValue(playerId, out WealthProfile profile))
            {
                profile = new WealthProfile
                {
                    playerId = playerId,
                    balance = 0f,
                    legalIncome = 0f,
                    illegalIncome = 0f,
                    unexplainedIncome = 0f,
                    history = new List<Transaction>(),
                    legitimacyScore = 1.0f
                };
                profiles[playerId] = profile;
            }

            return profile;
        }

        private Transaction CreateTransaction(
            string fromId,
            string toId,
            float amount,
            TransactionType type,
            IncomeSource source,
            ExpenseType expenseType,
            string entityId,
            string description,
            bool isLegal
        )
        {
            DateTime timestamp = TimeEnergySystem.Instance.GetCurrentTime();
            return new Transaction
            {
                id = Guid.NewGuid().ToString("N"),
                timestamp = timestamp,
                fromId = fromId,
                toId = toId,
                amount = amount,
                type = type,
                source = source,
                expenseType = expenseType,
                entityId = entityId,
                description = description,
                isLegal = isLegal
            };
        }

        private void RecalculateLegitimacy(WealthProfile profile)
        {
            float totalIncome = profile.legalIncome + profile.illegalIncome + profile.unexplainedIncome;
            profile.legitimacyScore = totalIncome == 0f ? 1.0f : profile.legalIncome / totalIncome;
        }

        private static bool IsLegalIncome(IncomeSource source)
        {
            switch (source)
            {
                case IncomeSource.DrugSale:
                case IncomeSource.Theft:
                case IncomeSource.SexWork:
                    return false;
                default:
                    return true;
            }
        }
    }
}

