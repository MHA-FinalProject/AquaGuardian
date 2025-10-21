# Data Flow & Component Connections

## Quick Reference: How Components Work Together

### 📊 **Component Dependency Map**

```
TrialRegressionAlgorithm.cs (Main Coordinator)
    ├──> TrialDataLoader.cs (loads CSV data)
    ├──> TrialReportGenerator.cs (generates reports)
    └──> ML/OxygenPredictor.cs (ML predictions)
            ├──> ML/FeatureNormalizer.cs (normalize data)
            ├──> ML/MultipleLinearRegression.cs (regression model)
            │       └──> ML/MatrixHelper.cs (matrix operations)
            └──> TrialDataModels.cs (data structures)
```

---

## 🔄 **Step-by-Step Analysis Flow**

### **User Action → Result**

```
1. User clicks "Analyze" button
   └─> TrialRegressionUI.CalculateRegression()

2. Load trial data from CSV
   └─> TrialDataLoader.LoadTrialDataFromCSV()
       ├─ Reads Trial_5_runs_.csv
       ├─ Reads Trial_Random_Parameters.csv
       └─ Merges data (random replaces regular)

3. Run regression analysis
   └─> TrialRegressionAlgorithm.PerformRegressionAnalysis()
       │
       ├─> Create OxygenPredictor instance
       │   └─ Sets topKFeatures = 4
       │
       ├─> Train ML model
       │   └─> OxygenPredictor.TrainModel()
       │       ├─> FeatureNormalizer.FitTransform(data)
       │       │   └─ Normalizes: (x - mean) / std
       │       │
       │       ├─> Feature Selection (if < 10 samples)
       │       │   ├─ Train with all features
       │       │   ├─ Calculate importance
       │       │   └─ Select top-4 features
       │       │
       │       └─> MultipleLinearRegression.Fit()
       │           └─> Solve: β = (X'X + αI)⁻¹X'y
       │               └─> Uses MatrixHelper for operations
       │
       ├─> Validate model
       │   └─> MultipleLinearRegression.KFoldCV()
       │       └─ Returns: RMSE, MAE, R²
       │
       ├─> Find optimal parameters
       │   └─> OxygenPredictor.FindOptimalParameters(5.0%)
       │       └─ Grid search for best params
       │
       ├─> Generate summary report
       │   └─> TrialReportGenerator.GenerateSummaryReport()
       │       └─ Compact text for UI
       │
       ├─> Generate full report
       │   └─> TrialReportGenerator.GenerateFullReport()
       │       └─ Detailed analysis with CV metrics
       │
       └─> Save to file
           └─> TrialReportGenerator.SaveToFile()
               └─ Assets/Data/RegressionResults/

4. Display results in UI
   └─> Show summary in regression panel
```

---

## 📁 **File Responsibilities**

| File | Responsibility | Used By |
|------|---------------|---------|
| **TrialRegressionAlgorithm.cs** | Main coordinator | TrialRegressionUI |
| **TrialDataLoader.cs** | Load CSV data | TrialRegressionAlgorithm |
| **TrialReportGenerator.cs** | Generate reports | TrialRegressionAlgorithm |
| **ML/OxygenPredictor.cs** | ML interface | TrialRegressionAlgorithm |
| **ML/FeatureNormalizer.cs** | Normalize features | OxygenPredictor |
| **ML/MultipleLinearRegression.cs** | Ridge regression | OxygenPredictor |
| **ML/MatrixHelper.cs** | Matrix operations | MultipleLinearRegression |
| **TrialDataModels.cs** | Data structures | All components |

---

## 🎯 **Key Integrations**

### **1. ML Pipeline Integration**

```csharp
// In TrialRegressionAlgorithm.cs

// Create predictor (uses ML/OxygenPredictor.cs)
var predictor = new OxygenPredictor { topKFeatures = 4 };

// Train model
// → Uses FeatureNormalizer
// → Uses MultipleLinearRegression
// → Uses MatrixHelper
predictor.TrainModel(trials, enableFeatureSelection: true);

// Get model for validation
// → Returns MultipleLinearRegression instance
var model = predictor.GetModel();

// Validate with K-Fold CV
// → Uses MultipleLinearRegression.KFoldCV()
var (rmse, mae, r2) = model.KFoldCV(X, y, kFolds);

// Find optimal parameters
// → Uses grid search + prediction
var optimal = predictor.FindOptimalParameters(5.0f);
```

### **2. Data Loading Integration**

```csharp
// In TrialRegressionAlgorithm.cs

// Delegates to TrialDataLoader.cs
public static List<TrialData> LoadTrialDataFromCSV()
{
    return TrialDataLoader.LoadTrialDataFromCSV();
}

// TrialDataLoader handles:
// - CSV parsing
// - Data merging (regular + random)
// - Oxygen column detection
// - Data validation
```

### **3. Report Generation Integration**

```csharp
// In TrialRegressionAlgorithm.cs

// Generate summary (uses TrialReportGenerator.cs)
result.summaryText = TrialReportGenerator.GenerateSummaryReport(
    trials, avgError, predictor, optimalParams);

// Generate full report
result.fullDetailsText = TrialReportGenerator.GenerateFullReport(
    trials, avgError, perfectTrials, failedTrials, 
    avgOxygen, predictor, optimalParams, 
    cvRmse, cvMae, cvR2, kFolds);

// Save to file
TrialReportGenerator.SaveToFile(
    result.fullDetailsText, trials, totalTrials);
```

---

## 🔧 **ML Component Details**

### **OxygenPredictor.cs** (Main ML Interface)
```csharp
// Public API
.TrainModel(trials, enableFeatureSelection)
.PredictOxygen(trial)
.FindOptimalParameters(targetOxygen)
.GetFeatureImportance()
.GetModel()

// Internal uses:
- FeatureNormalizer (for scaling)
- MultipleLinearRegression (for modeling)
```

### **MultipleLinearRegression.cs** (Core Algorithm)
```csharp
// Public API
.Fit(X, y)                    // Train model
.Predict(X)                   // Make predictions
.KFoldCV(X, y, k)            // Cross-validation
.CalculateMetrics(y_true, y_pred)  // RMSE, MAE, R²

// Internal uses:
- MatrixHelper (for X'X, inverse, etc.)
```

### **FeatureNormalizer.cs** (Preprocessing)
```csharp
// Public API
.Fit(data)                    // Calculate stats
.Transform(data)              // Apply normalization
.FitTransform(data)          // Fit + transform
.InverseTransform(data)      // Reverse normalization
```

### **MatrixHelper.cs** (Linear Algebra)
```csharp
// Public API (static methods)
.Transpose(matrix)
.Multiply(A, B)
.Inverse(matrix)
.AddInterceptColumn(X)
```

---

## 🎨 **Visual Component Flow**

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER INTERFACE                           │
│                    (TrialRegressionUI.cs)                       │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ↓
┌─────────────────────────────────────────────────────────────────┐
│                      MAIN COORDINATOR                           │
│              (TrialRegressionAlgorithm.cs)                      │
│                                                                  │
│  • Orchestrates entire analysis process                         │
│  • Delegates to specialized components                          │
│  • Returns RegressionResult                                     │
└─┬──────────────────┬──────────────────┬─────────────────────────┘
  │                  │                  │
  ↓                  ↓                  ↓
┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐
│ DATA LOADER  │  │  ML ENGINE   │  │  REPORT GENERATOR        │
│              │  │              │  │                          │
│ TrialData    │  │ Oxygen       │  │ TrialReport              │
│ Loader.cs    │  │ Predictor.cs │  │ Generator.cs             │
│              │  │              │  │                          │
│ • Load CSV   │  │ • Train      │  │ • Format summary         │
│ • Merge data │  │ • Predict    │  │ • Generate full report   │
│ • Validate   │  │ • Optimize   │  │ • Save to file           │
└──────────────┘  └──────┬───────┘  └──────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         │               │               │
         ↓               ↓               ↓
    ┌─────────┐    ┌──────────┐    ┌──────────┐
    │Feature  │    │Multiple  │    │Matrix    │
    │Normalizer    │Linear    │    │Helper    │
    │         │    │Regression│    │          │
    │• Scale  │    │• Ridge   │    │• X'X     │
    │• Stats  │    │• CV      │    │• Inverse │
    └─────────┘    └──────────┘    └──────────┘
                         │
                         └─────────────────────┐
                                               │
                    ┌──────────────────────────┴─────┐
                    │    SUPPORTING CLASSES          │
                    │                                │
                    │  • TrialDataModels.cs          │
                    │    (Data structures)           │
                    │                                │
                    │  • TrialDataCache.cs           │
                    │    (Runtime cache)             │
                    └────────────────────────────────┘
```

---

## 📝 **Summary**

### **Yes, ML/ folder is actively used!**

✅ **OxygenPredictor.cs** - Main ML interface  
✅ **MultipleLinearRegression.cs** - Core regression algorithm  
✅ **FeatureNormalizer.cs** - Data preprocessing  
✅ **MatrixHelper.cs** - Mathematical operations  

### **Everything is connected and coordinated:**

1. **TrialRegressionAlgorithm** = Main coordinator
2. **TrialDataLoader** = Data management
3. **TrialReportGenerator** = Output formatting
4. **ML/** = Machine learning core

### **Data flows smoothly:**
CSV → Loader → Algorithm → ML Engine → Reports → UI/File

---

*All components are integrated and working together as designed!* ✨


