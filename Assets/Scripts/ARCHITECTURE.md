# AquaGuardian Trial System - Architecture & Data Flow

## Overview
The Trial System is a sophisticated machine learning-based analysis tool that evaluates game parameters and predicts optimal settings for achieving a target oxygen level (5%).

---

## Project Structure

```
Assets/Scripts/
├── ML/                                  # Machine Learning Core
│   ├── OxygenPredictor.cs              # Main ML predictor (509 lines)
│   ├── MultipleLinearRegression.cs     # Ridge regression model (453 lines)
│   ├── FeatureNormalizer.cs            # Feature scaling (188 lines)
│   └── MatrixHelper.cs                 # Matrix operations (263 lines)
│
├── Trial/                              # Trial Management
│   ├── TrialUIController.cs           # UI management (450 lines)
│   ├── TrialParameterManager.cs       # Parameter loading/saving
│   └── TrialFishSpawner.cs           # In-game spawning logic
│
├── TrialRegressionAlgorithm.cs        # Main coordinator (258 lines)
├── TrialDataLoader.cs                 # CSV data loading (450 lines)
├── TrialReportGenerator.cs            # Report generation (210 lines)
├── TrialSystemManager.cs              # Overall system control (395 lines)
├── TrialRegressionUI.cs               # Regression UI (145 lines)
├── TrialDataCache.cs                  # Data caching (201 lines)
└── TrialDataModels.cs                 # Data structures (61 lines)
```

---

## Data Flow Architecture

### 1. **Trial Execution Flow**

```
User clicks "Start Trial"
        ↓
TrialSystemManager.StartTrials()
        ↓
TrialParameterManager.LoadAndApplyTrialParameters()
        ├─→ Random Mode? → Generate random parameters
        └─→ CSV Mode? → Load from Trial_5_runs_.csv
        ↓
Apply parameters to game objects
        ↓
Player completes trial
        ↓
TrialDataCache.SetOxygenValue()
        ↓
TrialParameterManager.SaveTrialResultToCSV()
        ├─→ Random Mode? → Save to Trial_Random_Parameters.csv
        └─→ CSV Mode? → Append to Trial_5_runs_.csv
```

### 2. **Regression Analysis Flow**

```
User clicks "Analyze"
        ↓
TrialRegressionUI.CalculateRegression()
        ↓
TrialDataLoader.LoadTrialDataFromCSV()
        ├─→ Load Trial_5_runs_.csv (regular trials)
        ├─→ Load Trial_Random_Parameters.csv (random trials)
        ├─→ Merge (random replaces regular with same ID)
        └─→ Take first 5 trials
        ↓
TrialRegressionAlgorithm.PerformRegressionAnalysis()
        ↓
┌─────────────────────────────────────────────────┐
│  ML PIPELINE (Assets/Scripts/ML/)              │
├─────────────────────────────────────────────────┤
│  1. OxygenPredictor.TrainModel()               │
│     ├─→ FeatureNormalizer.Fit()                │
│     ├─→ Feature Selection (if < 10 samples)    │
│     └─→ MultipleLinearRegression.Fit()         │
│                                                  │
│  2. MultipleLinearRegression                    │
│     ├─→ MatrixHelper (matrix operations)       │
│     ├─→ Ridge regularization (α = 0.1)         │
│     └─→ Normal equation: β = (X'X + αI)⁻¹X'y  │
│                                                  │
│  3. Model Validation                            │
│     └─→ KFoldCV (K=5 or adaptive)              │
│                                                  │
│  4. OxygenPredictor.FindOptimalParameters()    │
│     ├─→ Grid search over parameter ranges      │
│     ├─→ Predict oxygen for each combination    │
│     └─→ Return params closest to target (5%)   │
└─────────────────────────────────────────────────┘
        ↓
TrialReportGenerator.GenerateSummaryReport()
        ├─→ Calculate average error
        ├─→ Format compact summary for UI
        └─→ Include recommended parameters
        ↓
TrialReportGenerator.GenerateFullReport()
        ├─→ Include K-Fold CV metrics
        ├─→ Feature importance rankings
        ├─→ Trial-by-trial predictions
        └─→ Complete parameter recommendations
        ↓
TrialReportGenerator.SaveToFile()
        └─→ Save to Assets/Data/RegressionResults/
```

---

## Component Details

### **ML Core Components** (`Assets/Scripts/ML/`)

#### 1. **OxygenPredictor.cs** (Main ML Interface)
**Purpose**: High-level ML API for training and prediction

**Key Methods**:
- `TrainModel(trials, enableFeatureSelection)` - Trains regression model
- `PredictOxygen(trial)` - Predicts oxygen % for given parameters
- `FindOptimalParameters(targetOxygen)` - Finds best parameter combination
- `GetFeatureImportance()` - Returns ranked feature contributions

**Features**:
- Automatic feature selection for small datasets (< 10 samples)
- Uses top-K most important features (default: 4)
- Handles normalization via FeatureNormalizer
- Grid search optimization for parameter tuning

**Data Flow**:
```
Input: List<TrialData>
  ↓
Normalize features → Select top-K features → Train model
  ↓
Output: Trained model ready for predictions
```

---

#### 2. **MultipleLinearRegression.cs** (Regression Engine)
**Purpose**: Ridge regression with K-Fold cross-validation

**Algorithm**: Ridge Regression
```
Minimize: ||y - Xβ||² + α||β||²
Solution: β = (X'X + αI)⁻¹X'y
```

**Key Methods**:
- `Fit(X, y)` - Trains model using normal equation
- `Predict(X)` - Makes predictions
- `KFoldCV(X, y, k)` - Cross-validation
- `CalculateMetrics(y_true, y_pred)` - RMSE, MAE, R²

**Why Ridge Regression?**
- Prevents overfitting with small datasets
- Regularization parameter α = 0.1
- Stable with multicollinearity

**K-Fold Cross Validation**:
```
For k=5:
  Split data into 5 folds
  For each fold:
    Train on 4 folds → Test on 1 fold
  Average metrics across all folds
```

---

#### 3. **FeatureNormalizer.cs** (Data Preprocessing)
**Purpose**: Standardizes features to same scale

**Method**: Z-score normalization
```
x_normalized = (x - mean) / std_dev
```

**Key Methods**:
- `Fit(data)` - Calculate mean & std for each feature
- `Transform(data)` - Apply normalization
- `FitTransform(data)` - Fit and transform in one step
- `InverseTransform(data)` - Convert back to original scale

**Why Normalize?**
- Ensures all features contribute equally
- Speeds up optimization
- Improves numerical stability

---

#### 4. **MatrixHelper.cs** (Linear Algebra)
**Purpose**: Matrix operations for regression

**Key Operations**:
- `Transpose(A)` - A' for normal equation
- `Multiply(A, B)` - Matrix multiplication
- `Inverse(A)` - Matrix inversion (Gaussian elimination)
- `AddInterceptColumn(X)` - Add bias term

**Used in**: Normal equation solution (X'X)⁻¹X'y

---

### **Data Management**

#### **TrialDataLoader.cs** (CSV Management)
**Purpose**: Loads and merges trial data from multiple sources

**Key Features**:
- Reads `Trial_5_runs_.csv` (regular trials with multiple runs)
- Reads `Trial_Random_Parameters.csv` (random parameter trials)
- Automatically detects oxygen columns (o2_run1, o2_run2, o2_result, etc.)
- Uses latest run data
- Merges random trials (replace regular trials with same ID)

**Data Merging Logic**:
```
Regular Trials: {1, 2, 3, 4, 5}
Random Trials:  {3, 5}
Result:         {1(R), 2(R), 3(Rand), 4(R), 5(Rand)}
```

---

#### **TrialDataCache.cs** (Runtime Cache)
**Purpose**: Caches trial results during gameplay

**Features**:
- Stores oxygen values for up to 10 trials
- Provides fallback when CSV is unavailable
- Thread-safe singleton pattern

---

### **Reporting**

#### **TrialReportGenerator.cs** (Report Creation)
**Purpose**: Generates analysis reports

**Report Types**:

1. **Summary Report** (for UI display):
   ```
   === REGRESSION ANALYSIS ===
   Trials: 5 (Regular: 4, Random: 1)
   Average Error: 2.3%
   Target: 5.0% oxygen remaining
   === RECOMMENDED PARAMETERS ===
   Predicted Result: 4.8%
   Speed: 17.5
   Vertical Speed: 30.0
   ...
   ```

2. **Full Report** (saved to file):
   - K-Fold CV metrics (RMSE, MAE, R²)
   - Trial-by-trial predictions
   - Feature importance rankings
   - Complete parameter recommendations
   - Raw trial data

**File Output**: `RegressionAnalysis_YYYY-MM-DD_HH-MM-SS.txt`

---

## Machine Learning Pipeline Details

### **Training Process**

```python
# Step 1: Feature Extraction
Features = [speed, verticalSpeed, idleUpwardSpeed, lifeTime, 
            downHealthPairSec, removeHealthWithCollide,
            timeBetweenCollides, healHealthPoint]
Target = finalOxygenRemaining

# Step 2: Normalization
X_normalized = (X - mean) / std_dev

# Step 3: Feature Selection (if samples < 10)
# Train with all features → Calculate importance → Select top-4

# Step 4: Model Training (Ridge Regression)
β = (X'X + 0.1*I)⁻¹ * X'y

# Step 5: Validation (K-Fold CV)
# Adaptive K: min(5, max(2, num_samples))

# Step 6: Optimization
# Grid search to find parameters that predict ~5% oxygen
```

### **Prediction Process**

```python
# Input: New trial parameters
trial = {speed: 17, verticalSpeed: 30, ...}

# Step 1: Normalize
trial_norm = normalizer.transform(trial)

# Step 2: Select features (if feature selection was used)
trial_selected = trial_norm[selected_features]

# Step 3: Predict
oxygen_pred = model.predict(trial_selected)

# Output: Predicted oxygen percentage
return oxygen_pred  # e.g., 4.8%
```

---

## Key Algorithms

### **Ridge Regression**
```
Objective: min ||y - Xβ||² + α||β||²
Solution:  β = (X'X + αI)⁻¹X'y

Where:
- X: Feature matrix [n_samples × n_features]
- y: Target vector [n_samples]
- β: Coefficients [n_features]
- α: Regularization strength (0.1)
- I: Identity matrix
```

### **Feature Selection**
```
1. Train model with all 8 features
2. Calculate importance = |coefficient_i|
3. Rank features by importance
4. Select top-K features (K=4)
5. Retrain with selected features only
```

### **Grid Search Optimization**
```
For each parameter in parameter_ranges:
  For each value in value_range:
    trial = create_trial_with_params(values)
    predicted = model.predict(trial)
    error = |predicted - target|
    if error < best_error:
      best_params = values
Return best_params
```

---

## Performance Characteristics

### **Model Requirements**
- **Minimum samples**: 3 (but recommends 5+)
- **Optimal samples**: 10+ for full 8-feature model
- **Feature selection trigger**: < 10 samples → use 4 features

### **Computational Complexity**
- **Training**: O(n³) for matrix inversion (n = features)
- **Prediction**: O(n) per sample
- **K-Fold CV**: O(k × n³) where k = folds
- **Grid Search**: O(m × n) where m = combinations

### **Model Validation**
- **K-Fold CV**: Adaptive (2-5 folds)
- **Metrics**: RMSE, MAE, R²
- **Quality Thresholds**:
  - R² > 0.7: Excellent
  - R² > 0.5: Good
  - R² > 0.3: Fair
  - R² ≤ 0.3: Poor

---

## CSV File Formats

### **Trial_5_runs_.csv** (Regular Trials)
```csv
trial_id,speed,verticalSpeed,...,o2_run1,o2_run2,o2_run3,o2_run4
1,15,30,0.01,...,70.4,80.0,80.0,78.0
2,12,35,0.5,...,40.8,44.0,54.5,46.5
```
- Multiple runs per trial (o2_run1, o2_run2, etc.)
- System uses **latest run** with data

### **Trial_Random_Parameters.csv** (Random Mode)
```csv
trial_id,speed,verticalSpeed,...,o2_result
1,22.62,18.41,1.22,...,92.04
2,22.02,22.16,1.13,...,54.00
```
- One result per trial (o2_result)
- **Replaces** regular trials with same ID

---

## Error Handling

### **Data Loading**
- Missing CSV → Use TrialDataCache fallback
- Invalid data → Skip trial with warning
- Insufficient trials → Show error message

### **Model Training**
- < 3 samples → Error: "Need at least 3 trials"
- No variance → Error: "Not enough variance in data"
- Singular matrix → Error: "Matrix not invertible"

### **Feature Selection**
- Ensures selected features have variance
- Validates feature count matches model expectations
- Handles edge cases gracefully

---

## Configuration Parameters

### **ML Parameters**
```csharp
targetOxygen = 5.0f              // Target oxygen percentage
topKFeatures = 4                 // Features for small datasets
ridgeAlpha = 0.1f                // Regularization strength
kFolds = 5                       // Cross-validation folds (adaptive)
```

### **Parameter Ranges** (for optimization)
```csharp
speed:                [10.0, 25.0]
verticalSpeed:        [15.0, 45.0]
idleUpwardSpeed:      [0.01, 2.0]
lifeTime:             [0.8, 3.0]
downHealthPairSec:    [0.5, 3.5]
removeHealthWithCollide: [5.0, 15.0]
timeBetweenCollides:  [1.0, 5.0]
healHealthPoint:      [3.0, 15.0]
```

---

## Usage Examples

### **Running Analysis**
```csharp
// Load data
var trials = TrialDataLoader.LoadTrialDataFromCSV();

// Perform analysis
var result = TrialRegressionAlgorithm.PerformRegressionAnalysis(trials);

// Display summary
Debug.Log(result.summaryText);

// Save full report
TrialRegressionAlgorithm.SaveRegressionResultsToFile(result);
```

### **Making Predictions**
```csharp
var predictor = new OxygenPredictor();
predictor.TrainModel(trials);

var testTrial = new TrialData { 
    speed = 17.5f, 
    verticalSpeed = 30.0f,
    // ... other parameters
};

float predicted = predictor.PredictOxygen(testTrial);
// Returns: 4.8% (example)
```

---

## Future Improvements

### **Potential Enhancements**
1. **Neural Networks**: Replace linear regression with deep learning
2. **Real-time Prediction**: Predict outcome mid-trial
3. **Adaptive Learning**: Update model as more trials complete
4. **Multi-objective Optimization**: Balance multiple goals (oxygen + time)
5. **Uncertainty Quantification**: Provide confidence intervals

### **Data Collection**
- Store more trial metadata
- Track player performance metrics
- Record environmental conditions

---

## Troubleshooting

### **Common Issues**

**Q: "NaN in K-Fold CV"**  
A: Not enough samples for K folds. System auto-adjusts K or uses training metrics.

**Q: "Model quality: Poor (R*R < 0.3)"**  
A: Need more diverse trial data. Run additional trials with varied parameters.

**Q: "Feature count mismatch"**  
A: Feature selection flag set incorrectly. Check `useFeatureSelection` consistency.

**Q: "No oxygen columns found"**  
A: CSV missing oxygen data. Check file format and column headers.



