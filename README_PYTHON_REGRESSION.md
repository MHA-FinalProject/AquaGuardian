# Python Regression System for AquaGuardian

## Overview

AquaGuardian supports two regression modes:
- **C# Built-in** (default) - Ridge regression implemented in Unity
- **Python Server** (advanced) - External Python ML models via HTTP

---

## Installation

### Requirements

- Python 3.9+
- Unity 2021.3+ (or current version)

### Setup

```bash
# Install all dependencies (ML + Server)
pip install -r requirements.txt
```

**What gets installed:**
- `numpy`, `pandas`, `scikit-learn` - Core ML libraries
- `flask`, `flask-cors` - HTTP server (optional, only for server mode)

---

## Usage

### Mode 1: Training Models (Offline)

Train regression models from trial CSV data:

```bash
python PythonScripts/train_regression_model.py <csv_file> <model_type>
```

**Examples:**

```bash
# Train ElasticNet model (recommended)
python PythonScripts/train_regression_model.py Assets/Data/Trials/Trial_5_runs_.csv ElasticNet

# Train Ridge model
python PythonScripts/train_regression_model.py Assets/Data/Trials/Trial_5_runs_.csv Ridge

# Train Huber model (robust to outliers)
python PythonScripts/train_regression_model.py Assets/Data/Trials/Trial_5_runs_.csv Huber

# Train PLS model (Partial Least Squares)
python PythonScripts/train_regression_model.py Assets/Data/Trials/Trial_5_runs_.csv PLS
```

**Output:** Creates `regression_model_<type>.json` in project root

---

### Mode 2: Server Mode (Real-time from Unity)

Run a Python server that Unity can connect to:

```bash
python PythonScripts/regression_server.py
```

**Server starts on:** `http://localhost:5000`

**Endpoints:**
- `POST /train` - Train model from trial data
- `POST /predict` - Predict oxygen level
- `GET /health` - Check server status

---

## Unity Integration

### Enable Python Mode

1. **Open Scene:** `Scene_Ocean.unity`
2. **Find GameObject:** "python model" (in Hierarchy)
3. **Enable the GameObject** (checkbox in Inspector)
4. **Find Component:** "RegressionAnalyzer"
5. **Check:** ✅ **"Auto Load Python"**

### File Placement

Place trained model JSON in one of:
- `Assets/StreamingAssets/RegressionModels/regression_model_elasticnet.json`
- `Assets/Data/RegressionModels/regression_model_elasticnet.json`
- Project root: `regression_model_elasticnet.json`

Unity will auto-detect and load the model.

---

## Model Comparison

| Model | Best For | Pros | Cons |
|-------|----------|------|------|
| **ElasticNet** ⭐ | Most cases | Balanced L1+L2, handles collinearity | Requires tuning |
| **Ridge** | Small datasets | Simple, stable | May overfit with many features |
| **Huber** | Noisy data | Robust to outliers | Slower training |
| **PLS** | High collinearity | Reduces dimensionality | Complex interpretation |

**Recommended:** Start with **ElasticNet**

---

## Troubleshooting

### "Model not loaded" in Unity

✅ **Check:**
1. Is `regression_model_*.json` in correct location?
2. Is the file valid (not empty, not example file)?
3. Is "python model" GameObject **enabled**?
4. Is "Auto Load Python" **checked**?

### "Connection refused" (server mode)

✅ **Fix:**
1. Start Python server: `python PythonScripts/regression_server.py`
2. Verify server is running: Open `http://localhost:5000/health` in browser
3. Check firewall isn't blocking port 5000

### ImportError or ModuleNotFoundError

✅ **Fix:**
```bash
pip install -r requirements.txt
```

---

## Technical Details

### Model Format (JSON)

```json
{
  "model_type": "ElasticNet",
  "feature_names": ["speed", "verticalSpeed", ...],
  "intercept": 50.5,
  "betas": [0.5, -0.3, ...],
  "means": [1.5, 2.0, ...],
  "stds": [0.5, 0.8, ...],
  "n_samples": 5,
  "train_mae": 5.2,
  "train_r2": 0.85
}
```

### Features (10 parameters)

1. `speed` - Forward movement speed
2. `verticalSpeed` - Up/down movement speed
3. `idleUpwardSpeed` - Passive upward drift
4. `lifeTime` - Seconds between oxygen drain cycles
5. `RemoveHealthEveryLifeTime` - Oxygen lost per cycle
6. `removeHealthWithCollide` - Collision damage
7. `timeBetweenCollides` - Collision cooldown
8. `healHealthPoint` - Health pack restoration
9. `factorForce` - Amadeo device force multiplier
10. `EffectiveDrainRate` - Calculated drain rate (derived)

---

## Performance Tips

1. **Minimum 5 trials** for reliable training
2. **ElasticNet** works best with diverse parameter ranges
3. **Huber** if you have outlier trials (very low/high oxygen)
4. **Use C# mode** if Python server adds latency

---

## See Also

- [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) - Full system architecture
- [README.md](README.md) - Main game documentation
- `Assets/Data/HOW_REGRESSION_WORKS_NOW.md` - Internal docs

---

## Support

For issues or questions, refer to the full documentation or contact the development team.

