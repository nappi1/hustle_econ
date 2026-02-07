using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using NUnit.Framework;
using UnityEngine;
using Data;

namespace Tests.Core
{
    public class EconomySystemTests
    {
        private GameObject economyGameObject;
        private GameObject entityGameObject;
        private GameObject timeGameObject;
        private EconomySystem economySystem;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(EntitySystem));
            ResetSingleton(typeof(TimeEnergySystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            entityGameObject = new GameObject("EntitySystem");
            entityGameObject.AddComponent<EntitySystem>();

            economyGameObject = new GameObject("EconomySystem");
            economySystem = economyGameObject.AddComponent<EconomySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (economyGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(economyGameObject);
            }

            if (entityGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(entityGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void GetBalance_NewPlayer_ReturnsZero()
        {
            float balance = economySystem.GetBalance("player");
            Assert.AreEqual(0f, balance, 0.001f, "New player balance should be zero");
        }

        [Test]
        public void AddIncome_IncreasesBalance()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            float balance = economySystem.GetBalance("player");
            Assert.AreEqual(100f, balance, 0.001f, "Balance should increase by income amount");
        }

        [Test]
        public void DeductExpense_DecreasesBalance()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.DeductExpense("player", 40f, EconomySystem.ExpenseType.Food, "food");
            float balance = economySystem.GetBalance("player");
            Assert.AreEqual(60f, balance, 0.001f, "Balance should decrease by expense amount");
        }

        [Test]
        public void DeductExpense_InsufficientFunds_AllowsNegativeBalance()
        {
            economySystem.DeductExpense("player", 50f, EconomySystem.ExpenseType.Fine, "fine");
            float balance = economySystem.GetBalance("player");
            Assert.AreEqual(-50f, balance, 0.001f, "Balance should be allowed to go negative");
        }

        [Test]
        public void DeductExpense_InsufficientFunds_ReturnsFalse()
        {
            bool result = economySystem.DeductExpense("player", 50f, EconomySystem.ExpenseType.Fine, "fine");
            Assert.IsFalse(result, "DeductExpense should return false if insufficient funds");
        }

        [Test]
        public void GetBalance_AfterMultipleTransactions_ReturnsCorrectTotal()
        {
            economySystem.AddIncome("player", 200f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.DeductExpense("player", 25f, EconomySystem.ExpenseType.Food, "food");
            economySystem.AddIncome("player", 50f, EconomySystem.IncomeSource.Gift, "gift");
            economySystem.DeductExpense("player", 10f, EconomySystem.ExpenseType.Utilities, "bill");
            float balance = economySystem.GetBalance("player");
            Assert.AreEqual(215f, balance, 0.001f, "Balance should reflect all transactions");
        }

        [Test]
        public void AddIncome_Salary_CategorizesAsLegal()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(1f, score, 0.001f, "Salary income should be legal");
        }

        [Test]
        public void AddIncome_DrugSale_CategorizesAsIllegal()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.DrugSale, "deal");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(0f, score, 0.001f, "Drug sale income should be illegal");
        }

        [Test]
        public void AddIncome_Investment_CategorizesAsLegal()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Investment, "stocks");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(1f, score, 0.001f, "Investment income should be legal");
        }

        [Test]
        public void AddIncome_Theft_CategorizesAsIllegal()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Theft, "theft");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(0f, score, 0.001f, "Theft income should be illegal");
        }

        [Test]
        public void AddIncome_Other_LargeAmount_CategorizesAsUnexplained()
        {
            economySystem.AddIncome("player", 6000f, EconomySystem.IncomeSource.Other, "cash");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(0f, score, 0.001f, "Large unexplained income should reduce legitimacy");
        }

        [Test]
        public void AddIncome_Other_SmallAmount_DoesNotTrackAsUnexplained()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Other, "cash");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(1f, score, 0.001f, "Small other income should be treated as legal");
        }

        [Test]
        public void AddIncome_Gambling_CategorizesAsLegal()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Gambling, "win");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(1f, score, 0.001f, "Gambling income should be legal for now");
        }

        [Test]
        public void GetLegitimacyScore_NoIncome_Returns1()
        {
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(1f, score, 0.001f, "No income should return legitimacy of 1");
        }

        [Test]
        public void GetLegitimacyScore_AllLegalIncome_Returns1()
        {
            economySystem.AddIncome("player", 200f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Investment, "dividend");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(1f, score, 0.001f, "All legal income should yield legitimacy 1");
        }

        [Test]
        public void GetLegitimacyScore_AllIllegalIncome_Returns0()
        {
            economySystem.AddIncome("player", 200f, EconomySystem.IncomeSource.DrugSale, "deal");
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Theft, "theft");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(0f, score, 0.001f, "All illegal income should yield legitimacy 0");
        }

        [Test]
        public void GetLegitimacyScore_HalfLegalHalfIllegal_Returns0Point5()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.DrugSale, "deal");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.AreEqual(0.5f, score, 0.01f, "Half legal/illegal should yield legitimacy 0.5");
        }

        [Test]
        public void GetLegitimacyScore_WithUnexplainedIncome_ReducesScore()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.AddIncome("player", 6000f, EconomySystem.IncomeSource.Other, "cash");
            float score = economySystem.GetLegitimacyScore("player");
            Assert.Less(score, 1f, "Unexplained income should reduce legitimacy");
        }

        [Test]
        public void GetLegitimacyScore_UpdatesAfterIncomeChanges()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            float scoreAfterLegal = economySystem.GetLegitimacyScore("player");
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Theft, "theft");
            float scoreAfterIllegal = economySystem.GetLegitimacyScore("player");
            Assert.Greater(scoreAfterLegal, scoreAfterIllegal, "Legitimacy should update after income changes");
        }

        [Test]
        public void ProcessTransaction_SufficientFunds_TransfersMoney()
        {
            string buyer = "buyer";
            string seller = "seller";
            economySystem.AddIncome(buyer, 1000f, EconomySystem.IncomeSource.Salary, "funds");
            economySystem.AddIncome(seller, 0f, EconomySystem.IncomeSource.Salary, "seed");

            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Vehicle, new EntityData
            {
                value = 500f,
                owner = seller
            });

            bool success = economySystem.ProcessTransaction(buyer, seller, entity.id, 500f);
            Assert.IsTrue(success, "Transaction should succeed");
            Assert.AreEqual(500f, economySystem.GetBalance(buyer), 0.001f, "Buyer should have balance reduced");
            Assert.AreEqual(500f, economySystem.GetBalance(seller), 0.001f, "Seller should receive payment");
        }

        [Test]
        public void ProcessTransaction_SufficientFunds_TransfersEntity()
        {
            string buyer = "buyer";
            string seller = "seller";
            economySystem.AddIncome(buyer, 1000f, EconomySystem.IncomeSource.Salary, "funds");

            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Property, new EntityData
            {
                value = 500f,
                owner = seller
            });

            bool success = economySystem.ProcessTransaction(buyer, seller, entity.id, 500f);
            Entity updated = EntitySystem.Instance.GetEntity(entity.id);

            Assert.IsTrue(success, "Transaction should succeed");
            Assert.AreEqual(buyer, updated.owner, "Entity ownership should transfer to buyer");
        }

        [Test]
        public void ProcessTransaction_InsufficientFunds_ReturnsFalse()
        {
            string buyer = "buyer";
            string seller = "seller";
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData
            {
                value = 100f,
                owner = seller
            });

            bool success = economySystem.ProcessTransaction(buyer, seller, entity.id, 100f);
            Assert.IsFalse(success, "Transaction should fail if buyer cannot afford");
        }

        [Test]
        public void ProcessTransaction_WithSystemAsSeller_DoesntAddToSellerBalance()
        {
            string buyer = "buyer";
            economySystem.AddIncome(buyer, 1000f, EconomySystem.IncomeSource.Salary, "funds");
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData
            {
                value = 100f,
                owner = "system"
            });

            bool success = economySystem.ProcessTransaction(buyer, "system", entity.id, 100f);
            Assert.IsTrue(success, "Transaction should succeed with system seller");
            Assert.AreEqual(900f, economySystem.GetBalance(buyer), 0.001f, "Buyer should pay price");
        }

        [Test]
        public void ProcessTransaction_RecordsInBothPlayerHistories()
        {
            string buyer = "buyer";
            string seller = "seller";
            economySystem.AddIncome(buyer, 1000f, EconomySystem.IncomeSource.Salary, "funds");
            economySystem.AddIncome(seller, 0f, EconomySystem.IncomeSource.Salary, "seed");

            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Vehicle, new EntityData
            {
                value = 300f,
                owner = seller
            });

            economySystem.ProcessTransaction(buyer, seller, entity.id, 300f);

            List<EconomySystem.Transaction> buyerHistory = economySystem.GetTransactionHistory(buyer);
            List<EconomySystem.Transaction> sellerHistory = economySystem.GetTransactionHistory(seller);

            Assert.IsTrue(buyerHistory.Exists(t => t.entityId == entity.id), "Buyer history should include transaction");
            Assert.IsTrue(sellerHistory.Exists(t => t.entityId == entity.id), "Seller history should include transaction");
        }

        [Test]
        public void ProcessTransaction_BuyerIsSeller_NoTransfer()
        {
            string player = "player";
            economySystem.AddIncome(player, 500f, EconomySystem.IncomeSource.Salary, "funds");
            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData
            {
                value = 100f,
                owner = player
            });

            bool success = economySystem.ProcessTransaction(player, player, entity.id, 100f);
            Assert.IsTrue(success, "Transaction with self should succeed");
            Assert.AreEqual(500f, economySystem.GetBalance(player), 0.001f, "Balance should be unchanged");
        }

        [Test]
        public void ProcessTransaction_WhenSellerDoesNotOwnEntity_ReturnsFalse()
        {
            string buyer = "buyer";
            string seller = "seller";
            economySystem.AddIncome(buyer, 500f, EconomySystem.IncomeSource.Salary, "funds");

            Entity entity = EntitySystem.Instance.CreateEntity(EntityType.Item, new EntityData
            {
                value = 100f,
                owner = "other"
            });

            bool success = economySystem.ProcessTransaction(buyer, seller, entity.id, 100f);
            Assert.IsFalse(success, "Transaction should fail if seller does not own entity");
        }

        [Test]
        public void GetTransactionHistory_ReturnsCorrectTransactions()
        {
            string player = "player";
            economySystem.AddIncome(player, 100f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.DeductExpense(player, 25f, EconomySystem.ExpenseType.Food, "food");
            List<EconomySystem.Transaction> history = economySystem.GetTransactionHistory(player);
            Assert.AreEqual(2, history.Count, "History should include income and expense");
        }

        [Test]
        public void GetTransactionHistory_WithLimit_ReturnsCorrectCount()
        {
            string player = "player";
            economySystem.AddIncome(player, 10f, EconomySystem.IncomeSource.Salary, "pay1");
            economySystem.AddIncome(player, 10f, EconomySystem.IncomeSource.Salary, "pay2");
            economySystem.AddIncome(player, 10f, EconomySystem.IncomeSource.Salary, "pay3");
            List<EconomySystem.Transaction> history = economySystem.GetTransactionHistory(player, 2);
            Assert.AreEqual(2, history.Count, "History should respect limit");
        }

        [Test]
        public void GetTransactionHistory_OrdersNewestFirst()
        {
            string player = "player";
            economySystem.AddIncome(player, 10f, EconomySystem.IncomeSource.Salary, "pay1");
            TimeEnergySystem.Instance.AdvanceTime(5f);
            economySystem.AddIncome(player, 10f, EconomySystem.IncomeSource.Salary, "pay2");
            List<EconomySystem.Transaction> history = economySystem.GetTransactionHistory(player, 2);
            Assert.AreEqual("pay2", history[0].description, "Newest transaction should be first");
        }

        [Test]
        public void GetTransactionHistory_LimitExceedsTotal_ReturnsAll()
        {
            string player = "player";
            economySystem.AddIncome(player, 10f, EconomySystem.IncomeSource.Salary, "pay1");
            List<EconomySystem.Transaction> history = economySystem.GetTransactionHistory(player, 10);
            Assert.AreEqual(1, history.Count, "History should return all if limit exceeds total");
        }

        [Test]
        public void CalculateWealth_CashOnly_ReturnsBalance()
        {
            string player = "player";
            economySystem.AddIncome(player, 200f, EconomySystem.IncomeSource.Salary, "pay");
            float wealth = economySystem.CalculateWealth(player);
            Assert.AreEqual(200f, wealth, 0.001f, "Wealth should equal cash if no entities");
        }

        [Test]
        public void CalculateWealth_WithOwnedEntities_IncludesEntityValues()
        {
            string player = "player";
            economySystem.AddIncome(player, 200f, EconomySystem.IncomeSource.Salary, "pay");
            EntitySystem.Instance.CreateEntity(EntityType.Vehicle, new EntityData
            {
                value = 300f,
                condition = 100f,
                owner = player
            });

            float wealth = economySystem.CalculateWealth(player);
            Assert.AreEqual(500f, wealth, 0.001f, "Wealth should include entity value");
        }

        [Test]
        public void CalculateWealth_WithDebt_CanBeNegative()
        {
            string player = "player";
            economySystem.DeductExpense(player, 100f, EconomySystem.ExpenseType.Rent, "rent");
            float wealth = economySystem.CalculateWealth(player);
            Assert.AreEqual(-100f, wealth, 0.001f, "Wealth can be negative with debt");
        }

        [Test]
        public void TriggerBill_DeductsMoney()
        {
            string player = "player";
            economySystem.AddIncome(player, 100f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.TriggerBill(player, 40f, "rent");
            float balance = economySystem.GetBalance(player);
            Assert.AreEqual(60f, balance, 0.001f, "TriggerBill should deduct expense");
        }

        [Test]
        public void TriggerBill_FiresOnBillDueEvent()
        {
            string player = "player";
            bool fired = false;
            economySystem.OnBillDue += (id, amount, type) => fired = true;
            economySystem.TriggerBill(player, 50f, "rent");
            Assert.IsTrue(fired, "OnBillDue should fire when bill triggered");
        }

        [Test]
        public void DeductExpense_GoingNegative_FiresOnDebtEvent()
        {
            string player = "player";
            float capturedDebt = 0f;
            economySystem.OnDebt += (id, debt) => capturedDebt = debt;
            economySystem.DeductExpense(player, 100f, EconomySystem.ExpenseType.Rent, "rent");
            Assert.AreEqual(100f, capturedDebt, 0.001f, "OnDebt should fire with debt amount");
        }

        [Test]
        public void OnIncomeReceived_WhenIncomeAdded_FiresWithCorrectParameters()
        {
            string capturedPlayer = null;
            float capturedAmount = 0f;
            EconomySystem.IncomeSource capturedSource = EconomySystem.IncomeSource.Other;

            economySystem.OnIncomeReceived += (player, amount, source) =>
            {
                capturedPlayer = player;
                capturedAmount = amount;
                capturedSource = source;
            };

            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");

            Assert.AreEqual("player", capturedPlayer, "Player id should match");
            Assert.AreEqual(100f, capturedAmount, 0.001f, "Amount should match");
            Assert.AreEqual(EconomySystem.IncomeSource.Salary, capturedSource, "Source should match");
        }

        [Test]
        public void OnExpensePaid_WhenExpenseDeducted_FiresWithCorrectParameters()
        {
            string capturedPlayer = null;
            float capturedAmount = 0f;
            EconomySystem.ExpenseType capturedType = EconomySystem.ExpenseType.Other;

            economySystem.OnExpensePaid += (player, amount, type) =>
            {
                capturedPlayer = player;
                capturedAmount = amount;
                capturedType = type;
            };

            economySystem.DeductExpense("player", 25f, EconomySystem.ExpenseType.Food, "food");

            Assert.AreEqual("player", capturedPlayer, "Player id should match");
            Assert.AreEqual(25f, capturedAmount, 0.001f, "Amount should match");
            Assert.AreEqual(EconomySystem.ExpenseType.Food, capturedType, "Expense type should match");
        }

        [Test]
        public void OnDebt_WhenBalanceGoesNegative_FiresWithDebtAmount()
        {
            string capturedPlayer = null;
            float capturedDebt = 0f;
            economySystem.OnDebt += (player, debt) =>
            {
                capturedPlayer = player;
                capturedDebt = debt;
            };

            economySystem.DeductExpense("player", 60f, EconomySystem.ExpenseType.Rent, "rent");

            Assert.AreEqual("player", capturedPlayer, "Player id should match");
            Assert.AreEqual(60f, capturedDebt, 0.001f, "Debt amount should match");
        }

        [Test]
        public void OnTransactionComplete_WhenTransactionProcessed_FiresWithTransaction()
        {
            EconomySystem.Transaction captured = null;
            economySystem.OnTransactionComplete += transaction => captured = transaction;

            economySystem.AddIncome("player", 10f, EconomySystem.IncomeSource.Salary, "pay");

            Assert.IsNotNull(captured, "OnTransactionComplete should fire");
            Assert.AreEqual(10f, captured.amount, 0.001f, "Transaction amount should match");
        }

        [Test]
        public void OnBillDue_WhenBillTriggered_FiresWithCorrectParameters()
        {
            string capturedPlayer = null;
            float capturedAmount = 0f;
            string capturedType = null;
            economySystem.OnBillDue += (player, amount, type) =>
            {
                capturedPlayer = player;
                capturedAmount = amount;
                capturedType = type;
            };

            economySystem.TriggerBill("player", 40f, "rent");

            Assert.AreEqual("player", capturedPlayer, "Player id should match");
            Assert.AreEqual(40f, capturedAmount, 0.001f, "Amount should match");
            Assert.AreEqual("rent", capturedType, "Bill type should match");
        }

        [Test]
        public void AddIncome_NegativeAmount_TreatedAsExpense()
        {
            economySystem.AddIncome("player", 100f, EconomySystem.IncomeSource.Salary, "pay");
            economySystem.AddIncome("player", -20f, EconomySystem.IncomeSource.Salary, "negative");
            float balance = economySystem.GetBalance("player");
            Assert.AreEqual(80f, balance, 0.001f, "Negative income should reduce balance");
        }

        [Test]
        public void DeductExpense_NegativeAmount_TreatedAsIncome()
        {
            economySystem.DeductExpense("player", -30f, EconomySystem.ExpenseType.Other, "negative");
            float balance = economySystem.GetBalance("player");
            Assert.AreEqual(30f, balance, 0.001f, "Negative expense should increase balance");
        }

        [Test]
        public void GetTransactionHistory_WithZeroLimit_ReturnsEmpty()
        {
            economySystem.AddIncome("player", 10f, EconomySystem.IncomeSource.Salary, "pay");
            List<EconomySystem.Transaction> history = economySystem.GetTransactionHistory("player", 0);
            Assert.AreEqual(0, history.Count, "Zero limit should return empty list");
        }

        [Test]
        public void ProcessTransaction_WithMissingEntity_ReturnsFalse()
        {
            string buyer = "buyer";
            string seller = "seller";
            economySystem.AddIncome(buyer, 100f, EconomySystem.IncomeSource.Salary, "funds");

            bool success = economySystem.ProcessTransaction(buyer, seller, "missing", 50f);
            Assert.IsFalse(success, "Missing entity should fail transaction");
        }

        [Test]
        public void GetTransactionHistory_TimestampsUseTimeEnergySystem()
        {
            string player = "player";
            DateTime expected = TimeEnergySystem.Instance.GetCurrentTime();
            economySystem.AddIncome(player, 10f, EconomySystem.IncomeSource.Salary, "pay");
            List<EconomySystem.Transaction> history = economySystem.GetTransactionHistory(player, 1);
            Assert.AreEqual(expected, history[0].timestamp, "Timestamp should match TimeEnergySystem");
        }

        private static void ResetSingleton(Type systemType)
        {
            FieldInfo field = systemType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }
    }
}

