# How Regression Works Now - COMPLETE FLOW 🔄

## 📊 The Problem We Fixed:

### **OLD ISSUE:**
```
1. Trial 5 completes → saves to o2_run11
2. User clicks "Analyze"
3. TrialRegressionAnalyzer reads cached TextAsset
4. Gets data from o2_run10 (WRONG - old data!)
```

### **NEW SOLUTION:**
```
1. Trial 5 completes → saves to o2_run11
2. TrialParameterManager IMMEDIATELY updates O2_Wide_AllSets.csv
3. User clicks "Analyze"
4. TrialRegressionAnalyzer reads FRESH data from disk
5. Gets data from o2_run11 (CORRECT - latest data!)
```

---

## 🔄 Complete Flow:

### **Step 1: Trial 5 Completes**
```csharp
// In TrialParameterManager.SaveTrialResultToCSV()

1. UpdateOriginalCSV(trialData)
   → Adds oxygen value to o2_run11 column

2. SaveToTimestampedCSV(trialData)
   → Backup file created

3. if (trialData.trialId == 5)
   {
       UpdateO2WideAllSets(); ← NEW! Automatic update
   }
```

**Console Output:**
```
✓ Trial 5 saved: Oxygen=76.0%, Completed=True
=== UPDATING O2_WIDE_ALLSETS.CSV ===
Using last oxygen column: o2_run11 (index 20)
  Trial 1: 78.0%
  Trial 2: 48.0%
  Trial 3: 60.0%
  Trial 4: 78.0%
  Trial 5: 76.0%
✓ Updated O2_Wide_AllSets.csv: Added 5 oxygen values from o2_run11
   Values: 78, 48, 60, 78, 76
```

---

### **Step 2: User Clicks "Analyze"**
```csharp
// In TrialRegressionAnalyzer.CalculateRegression()

1. ReloadCSVFromDisk()
   → Unity Editor: AssetDatabase.Refresh()
   → Forces Unity to see the updated file

2. LoadTrialDataFromCSV()
   → Reads DIRECTLY from disk (not TextAsset cache)
   → File.ReadAllText("Trial_5_runs_.csv")

3. For each trial:
   → Searches backwards from o2_run11 to o2_run1
   → Finds LATEST non-empty value

4. PerformRegressionAnalysis()
   → Calculates correlations
   → Determines status
   → Shows recommendations
```

**Console Output:**
```
=== STARTING REGRESSION ANALYSIS ===
✓ Reloaded Trial_5_runs_.csv from disk
✓ Reading FRESH data from disk: C:/...AquaGuardian/Assets/Data/Trial_5_runs_.csv
Trial 1: Using column 20 (o2_run11) = 78.0%
Trial 2: Using column 20 (o2_run11) = 48.0%
Trial 3: Using column 20 (o2_run11) = 60.0%
Trial 4: Using column 20 (o2_run11) = 78.0%
Trial 5: Using column 20 (o2_run11) = 76.0%
Loaded 5 trials

REGRESSION ANALYSIS
Trials:5 Avg:68.0% Perfect:0 Failed:0
...
```

---

## 📁 Files That Change:

### **1. Trial_5_runs_.csv**
```csv
BEFORE Trial 5:
trial_id,...,o2_run10,o2_run11
1,...,80,
2,...,38,
...

AFTER Trial 5:
trial_id,...,o2_run10,o2_run11
1,...,80,78  ← Updated!
2,...,38,48  ← Updated!
3,...,57,60  ← Updated!
4,...,78,78  ← Updated!
5,...,78,76  ← Updated!
```

### **2. O2_Wide_AllSets.csv**
```csv
BEFORE Trial 5:
timestamp,o2_remaining_1,o2_remaining_2,o2_remaining_3,o2_remaining_4,o2_remaining_5
2025-10-16 11:00,80,38,57,78,78

AFTER Trial 5:
timestamp,o2_remaining_1,o2_remaining_2,o2_remaining_3,o2_remaining_4,o2_remaining_5
2025-10-16 11:00,80,38,57,78,78
2025-10-16 12:00,78,48,60,78,76  ← NEW ROW! Automatically added
```

---

## 🔍 Key Changes:

### **1. TrialParameterManager.cs**
```csharp
public bool SaveTrialResultToCSV(TrialDataModels.TrialData trialData)
{
    // ...
    
    // 3. Update O2_Wide_AllSets.csv if this is trial 5 (end of set)
    if (trialData.trialId == 5)
    {
        UpdateO2WideAllSets(); ← NEW!
    }
    
    // ...
}

private void UpdateO2WideAllSets()
{
    // 1. Read Trial_5_runs_.csv
    // 2. Find LAST o2_run column
    // 3. Extract ALL trial values from that column
    // 4. Append to O2_Wide_AllSets.csv
}
```

### **2. TrialRegressionAnalyzer.cs**
```csharp
public void CalculateRegression()
{
    // CRITICAL: Reload to get latest data!
    ReloadCSVFromDisk(); ← NEW!
    
    if (LoadTrialDataFromCSV())
    {
        // ... analysis
    }
}

private void ReloadCSVFromDisk()
{
    #if UNITY_EDITOR
    UnityEditor.AssetDatabase.Refresh(); ← Forces Unity to reload
    #endif
}

private bool LoadTrialDataFromCSV()
{
    // Read DIRECTLY from disk (not cached TextAsset)
    string csvPath = Path.Combine(Application.dataPath, "Data", "Trial_5_runs_.csv");
    string csvText = File.ReadAllText(csvPath); ← Direct disk read!
    
    // ... parse and load
}
```

---

## ✅ What This Fixes:

### **OLD Problem:**
```
Run 10: o2_run10 has values
User clicks "Analyze"
→ Gets o2_run10 (correct)

Run 11: o2_run11 gets NEW values
User clicks "Analyze"
→ STILL gets o2_run10! (WRONG - cached!)
```

### **NEW Solution:**
```
Run 10: o2_run10 has values
User clicks "Analyze"
→ Refreshes from disk
→ Gets o2_run10 (correct)

Run 11: o2_run11 gets NEW values
Trial 5 completes → UpdateO2WideAllSets() runs immediately
User clicks "Analyze"
→ Refreshes from disk
→ Gets o2_run11 (CORRECT - latest!)
```

---

## 🎯 Expected Behavior:

### **Your Current Data:**
```
o2_run11 (latest):
Trial 1: 78.0%
Trial 2: 48.0%
Trial 3: 60.0%
Trial 4: 78.0%
Trial 5: 76.0%

Average: 68.0%
Status: "MODERATELY EASY - consider increasing difficulty"
```

### **Console Output You'll See:**
```
=== UPDATING O2_WIDE_ALLSETS.CSV ===
Using last oxygen column: o2_run11 (index 20)
  Trial 1: 78.0%
  Trial 2: 48.0%
  Trial 3: 60.0%
  Trial 4: 78.0%
  Trial 5: 76.0%
✓ Updated O2_Wide_AllSets.csv: Added 5 oxygen values from o2_run11
   Values: 78, 48, 60, 78, 76

[User clicks Analyze]

=== STARTING REGRESSION ANALYSIS ===
✓ Reloaded Trial_5_runs_.csv from disk
✓ Reading FRESH data from disk: ...Trial_5_runs_.csv
Trial 1: Using column 20 (o2_run11) = 78.0%  ← CORRECT!
Trial 2: Using column 20 (o2_run11) = 48.0%
Trial 3: Using column 20 (o2_run11) = 60.0%
Trial 4: Using column 20 (o2_run11) = 78.0%
Trial 5: Using column 20 (o2_run11) = 76.0%
Loaded 5 trials

REGRESSION ANALYSIS
Trials:5 Avg:68.0% Perfect:0 Failed:0
TOP CORRELATIONS:
...
Status: MODERATELY EASY - consider increasing difficulty
```

---

## 🔧 Testing:

### **Test 1: Verify Automatic Update**
1. Run 5 trials
2. Complete Trial 5
3. **Check console** - should see:
   ```
   === UPDATING O2_WIDE_ALLSETS.CSV ===
   ✓ Updated O2_Wide_AllSets.csv: Added 5 oxygen values
   ```
4. **Open `O2_Wide_AllSets.csv`** - should have new row with timestamp

### **Test 2: Verify Fresh Read**
1. After trials, click "Analyze"
2. **Check console** - should see:
   ```
   ✓ Reloaded Trial_5_runs_.csv from disk
   ✓ Reading FRESH data from disk
   Trial 1: Using column 20 (o2_run11) = XX.X%
   ```
3. Column number should match latest column in `Trial_5_runs_.csv`

### **Test 3: Verify Correct Values**
1. Open `Trial_5_runs_.csv` in Excel
2. Look at **last column** with values (e.g., `o2_run11`)
3. Click "Analyze" in game
4. **Compare values** in console with Excel column
5. Should be **EXACT MATCH**

---

## 📊 Data Flow Diagram:

```
┌─────────────────┐
│  Trial 5 Ends   │
└────────┬────────┘
         │
         ├─→ UpdateOriginalCSV()
         │   └─→ Trial_5_runs_.csv updated (o2_run11)
         │
         ├─→ SaveToTimestampedCSV()
         │   └─→ Backup file created
         │
         └─→ UpdateO2WideAllSets() ← AUTOMATIC!
             └─→ O2_Wide_AllSets.csv updated

         [User waits, then clicks "Analyze"]

┌──────────────────┐
│ User Clicks      │
│ "Analyze" Button │
└────────┬─────────┘
         │
         ├─→ ReloadCSVFromDisk()
         │   └─→ Unity refreshes asset cache
         │
         ├─→ LoadTrialDataFromCSV()
         │   └─→ File.ReadAllText() from disk
         │       └─→ Search backwards for last o2_run
         │           └─→ Finds o2_run11 ✓
         │
         └─→ PerformRegressionAnalysis()
             └─→ Uses LATEST data!
```

---

## ⚠️ Important Notes:

1. **O2_Wide_AllSets.csv updates AUTOMATICALLY**
   - No need to click "Analyze" for it to update
   - Updates immediately when Trial 5 completes
   
2. **Fresh data ALWAYS**
   - Reads from disk, not Unity cache
   - Uses `File.ReadAllText()` not `TextAsset.text`
   
3. **Last column ALWAYS**
   - Searches backwards: column 20, 19, 18...
   - Finds first non-empty value
   - That's the LATEST run

4. **Works in Editor AND Build**
   - `AssetDatabase.Refresh()` only in Editor
   - `File.ReadAllText()` works everywhere

---

## 🎓 Summary:

| Action | Old Behavior | New Behavior |
|--------|-------------|--------------|
| Trial 5 completes | Only updates Trial_5_runs_.csv | Updates BOTH Trial_5_runs_.csv AND O2_Wide_AllSets.csv |
| Click "Analyze" | Reads cached TextAsset (old data) | Reads FRESH from disk (latest data) |
| Column search | Searches backwards (correct) | Searches backwards (correct) |
| O2_Wide update | Manual, during Analysis | Automatic, on Trial 5 completion |

---

**Result:** Regression ALWAYS uses the **LATEST** oxygen values! 🎯

_Last updated: 2025-10-16 13:00_


