# V2 Update (Aligned to Current Codebase)

This V2 spec reflects the current implementation. See docs/IMPLEMENTATION_DISCREPANCIES.md for cross-system gaps.

## Implementation Notes

- Enums include SexWork/SugarRelationship/Blackmail/Personal.
- No OnBalanceChanged event; HUD updates money manually.
- AddIncome/DeductExpense are primary APIs; SetBalance does not exist.

---
3. ECONOMY SYSTEM
Purpose
Manages all money flows, transactions, pricing, and wealth tracking. Handles buying/selling entities, tracking income sources (legal vs illegal), calculating legitimacy scores, and managing debt. Provides the financial backbone for the entire game.

Interface
GetBalance
csharp
float GetBalance(string playerId)
Purpose: Returns player's current money.

Parameters:

playerId: Player ID
Returns: Current balance (can be negative if in debt)

AddIncome
csharp
void AddIncome(string playerId, float amount, IncomeSource source, string description)
Purpose: Add money to player account (paycheck, drug sale, investment return).

Parameters:

playerId: Player receiving money
amount: How much
source: IncomeSource enum (Salary, DrugSale, Investment, Gift, etc.)
description: Details for transaction history
Side effects:

Updates balance
Records transaction in history
Updates income tracking (legal vs illegal)
Broadcasts OnIncomeReceived event
DeductExpense
csharp
bool DeductExpense(string playerId, float amount, ExpenseType type, string description)
Purpose: Remove money from player account (rent, bills, fines, purchases).

Parameters:

playerId: Player paying
amount: How much
type: ExpenseType enum (Rent, Bills, Fine, Purchase, etc.)
description: Details
Returns: true if sufficient funds, false if can't afford (goes into debt if forced)

Side effects:

Updates balance (can go negative)
Records transaction
Broadcasts OnExpensePaid event
If balance goes negative, broadcasts OnDebt event
ProcessTransaction
csharp
bool ProcessTransaction(string buyerId, string sellerId, string entityId, float price)
Purpose: Complete a purchase (transfer entity ownership + money).

Parameters:

buyerId: Who's buying
sellerId: Who's selling (can be "system" for buying from store)
entityId: What's being bought
price: How much
Returns: true if successful, false if buyer can't afford

Side effects:

Deducts money from buyer
Adds money to seller (if not "system")
Transfers entity ownership (calls EntitySystem.TransferOwnership)
Records transaction in both player's histories
Broadcasts OnTransactionComplete event
Example:

csharp
// Player buys car from dealership
ProcessTransaction(
    playerId,
    "system",
    "vehicle_lambo_001",
    200000f
);
GetLegitimacyScore
csharp
float GetLegitimacyScore(string playerId)
Purpose: Calculate how "legitimate" player's wealth appears (0-1 scale).

Returns: Legitimacy score

1.0 = all legal income
0.5 = half legal, half unexplained
0.0 = all illegal/unexplained
Formula: legalIncome / (legalIncome + illegalIncome + unexplainedIncome)

Used by:

Banks (loan approval)
IRS (audit triggers)
NPCs (judgment/respect)
GetTransactionHistory
csharp
List<Transaction> GetTransactionHistory(string playerId, int limit = 50)
Purpose: Returns recent transactions for player.

Parameters:

playerId: Player ID
limit: How many recent transactions (default 50)
Returns: List of Transaction objects, newest first

CalculateWealth
csharp
float CalculateWealth(string playerId)
```
**Purpose:** Total net worth (cash + entity values).

**Returns:** Total wealth

**Calculation:**
```
balance + sum(owned entities' values)
TriggerBill
csharp
void TriggerBill(string playerId, float amount, string billType)
Purpose: Schedule recurring expense (rent, utilities, loan payment).

Parameters:

playerId: Player who owes
amount: How much
billType: "rent", "utilities", "loan", etc.
Side effects:

Deducts money (forces debt if can't pay)
Records late payment if balance was insufficient
Broadcasts OnBillDue event
Events
csharp
event Action<string, float, IncomeSource> OnIncomeReceived  // (playerId, amount, source)
event Action<string, float, ExpenseType> OnExpensePaid      // (playerId, amount, type)
event Action<string, float> OnDebt                          // (playerId, debtAmount)
event Action<Transaction> OnTransactionComplete
event Action<string, float, string> OnBillDue               // (playerId, amount, billType)
Data Structures
Transaction
csharp
class Transaction {
    string id;
    DateTime timestamp;
    string fromId;           // Who paid (player or "system")
    string toId;             // Who received (player or "system")
    float amount;
    TransactionType type;    // Income or Expense
    IncomeSource source;     // If income
    ExpenseType expenseType; // If expense
    string entityId;         // If buying/selling entity (nullable)
    string description;
    bool isLegal;            // Traceable/legitimate transaction
}
IncomeSource (enum)
csharp
enum IncomeSource {
    Salary,           // Legal job paycheck
    DrugSale,         // Illegal
    Investment,       // Legal
    BusinessProfit,   // Legal or illegal depending on business
    Gift,             // From NPC
    Theft,            // Illegal
    Gambling,         // Legal but suspicious if frequent/large
    Other
}
ExpenseType (enum)
csharp
enum ExpenseType {
    Rent,
    Utilities,
    Food,
    Transportation,
    Fine,             // Legal penalty
    Purchase,         // Buying entity
    LoanPayment,
    Bribe,            // Illegal
    Other
}
TransactionType (enum)
csharp
enum TransactionType {
    Income,
    Expense
}
WealthProfile (tracking)
csharp
class WealthProfile {
    string playerId;
    float balance;
    float legalIncome;      // Cumulative
    float illegalIncome;    // Cumulative
    float unexplainedIncome; // Large cash deposits with no source
    List<Transaction> history;
    float legitimacyScore;  // Calculated
}
Dependencies
Reads from:

EntitySystem (entity values for wealth calculation, ownership for transactions)
Writes to:

EntitySystem (calls TransferOwnership during transactions)
Subscribed to by:

JobSystem (salary payments)
RelationshipSystem (financial status affects relationships)
ReputationSystem (wealth/legitimacy affects social reputation)
HeatSystem (large illegal income increases heat)
Implementation Notes
Legitimacy Tracking:

csharp
void AddIncome(string playerId, float amount, IncomeSource source, string description) {
    WealthProfile profile = GetProfile(playerId);
    profile.balance += amount;
    
    // Categorize income
    switch (source) {
        case IncomeSource.Salary:
        case IncomeSource.Investment:
            profile.legalIncome += amount;
            break;
        case IncomeSource.DrugSale:
        case IncomeSource.Theft:
            profile.illegalIncome += amount;
            break;
        case IncomeSource.Other:
            if (amount > 5000f) { // Large unexplained cash
                profile.unexplainedIncome += amount;
            }
            break;
    }
    
    // Record transaction
    Transaction tx = new Transaction {
        // ... populate fields
        isLegal = IsLegalSource(source)
    };
    profile.history.Add(tx);
    
    // Update legitimacy
    profile.legitimacyScore = CalculateLegitimacy(profile);
    
    OnIncomeReceived?.Invoke(playerId, amount, source);
}

float CalculateLegitimacy(WealthProfile profile) {
    float totalIncome = profile.legalIncome + profile.illegalIncome + profile.unexplainedIncome;
    if (totalIncome == 0) return 1.0f; // No income = technically legitimate
    return profile.legalIncome / totalIncome;
}
Debt Handling:

csharp
bool DeductExpense(string playerId, float amount, ExpenseType type, string description) {
    WealthProfile profile = GetProfile(playerId);
    bool canAfford = profile.balance >= amount;
    
    // Deduct regardless (can go negative)
    profile.balance -= amount;
    
    // Record transaction
    Transaction tx = new Transaction { /* ... */ };
    profile.history.Add(tx);
    
    OnExpensePaid?.Invoke(playerId, amount, type);
    
    // If went into debt
    if (profile.balance < 0) {
        OnDebt?.Invoke(playerId, -profile.balance);
    }
    
    return canAfford;
}
```

---

## **Edge Cases**

1. **Transaction with self:** Allowed but does nothing (money stays same)
2. **Buying entity player already owns:** Should fail (check EntitySystem first)
3. **Negative income:** Treated as expense
4. **Legitimacy calculation with no income:** Returns 1.0 (technically clean)
5. **Debt exceeds threshold:** Could trigger creditors/repo (handled by consequence system)

---

## **Testing Checklist**
```
[ ] GetBalance returns correct amount
[ ] AddIncome increases balance correctly
[ ] AddIncome categorizes legal/illegal income
[ ] DeductExpense decreases balance
[ ] DeductExpense allows negative balance (debt)
[ ] OnDebt fires when balance goes negative
[ ] ProcessTransaction transfers money and entity
[ ] ProcessTransaction fails if buyer can't afford
[ ] GetLegitimacyScore calculates correctly
[ ] Legitimacy score updates as income changes
[ ] GetTransactionHistory returns correct transactions
[ ] CalculateWealth includes cash + entity values
[ ] TriggerBill deducts recurring expenses
[ ] All transaction types record correctly in history
