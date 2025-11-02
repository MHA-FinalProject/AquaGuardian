#!/usr/bin/env python3
"""
Solve regression equation for target difficulty level
Trains Ridge regression and finds optimal parameters for target O2
"""
import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.linear_model import Ridge

FEATURES = [
    "speed","verticalSpeed","idleUpwardSpeed","lifeTime",
    "downHealthPairSec","removeHealthWithCollide","timeBetweenCollides","healHealthPoint"
]

def strip_num(x):
    """Remove % signs and convert to float"""
    if pd.isna(x): return np.nan
    try: return float(str(x).replace("%","").strip())
    except: return np.nan

def sample_standardize(X):
    """Z-score normalization (sample std with ddof=1)"""
    X = np.asarray(X, dtype=float)
    mean = np.nanmean(X, axis=0)
    std = np.nanstd(X, axis=0, ddof=1 if len(X)>1 else 0)
    std[~np.isfinite(std) | (std < 1e-10)] = 1.0
    return (X - mean) / std, mean, std

def main():
    # ========== LOAD DATA ==========
    csv_path = Path(__file__).parent / "Trials" / "Trial_Random_Parameters.csv"
    if not csv_path.exists():
        raise FileNotFoundError(f"CSV not found: {csv_path}")
    
    df = pd.read_csv(csv_path).head(5)
    X = df[FEATURES].applymap(strip_num).to_numpy(float)
    y = df['o2_result'].apply(strip_num).to_numpy(float)
    
    # ========== TRAIN MODEL ==========
    Xz, mean, std = sample_standardize(X)
    model = Ridge(alpha=0.5, fit_intercept=True, solver="cholesky")
    model.fit(Xz, y)
    
    # ========== PRINT COEFFICIENTS ==========
    print("\n" + "="*70)
    print("REGRESSION COEFFICIENTS")
    print("="*70)
    print(f"\nβ0 (Intercept) = {model.intercept_:.4f}")
    for i, (name, coef) in enumerate(zip(FEATURES, model.coef_), 1):
        print(f"β{i} ({name:25s}) = {coef:+.4f}")
    
    # ========== PRINT EQUATION ==========
    print("\n" + "-"*70)
    print("REGRESSION EQUATION:")
    print("-"*70)
    print(f"\nO2 = {model.intercept_:.4f}")
    for name, coef in zip(FEATURES, model.coef_):
        print(f"     {coef:+.4f} * {name}")
    
    # ========== FIND OPTIMAL PARAMETERS ==========
    target_o2 = 10.0
    
    # Find most important features
    importance = [(name, abs(coef)) for name, coef in zip(FEATURES, model.coef_)]
    importance.sort(key=lambda x: x[1], reverse=True)
    top3_features = [f[0] for f in importance[:3]]
    
    # Base parameters (average values from dataset)
    base_params = {
        'speed': 17.5, 'verticalSpeed': 30.0, 'idleUpwardSpeed': 1.0,
        'lifeTime': 1.5, 'downHealthPairSec': 2.5, 'removeHealthWithCollide': 10.0,
        'timeBetweenCollides': 3.0, 'healHealthPoint': 10.0
    }
    
    # Grid search: vary top 3 features
    grid_values = {}
    for feat in top3_features:
        if feat == 'healHealthPoint':
            grid_values[feat] = np.linspace(1, 20, 20)
        elif feat == 'downHealthPairSec':
            grid_values[feat] = np.linspace(0.5, 5.0, 20)
        elif feat == 'lifeTime':
            grid_values[feat] = np.linspace(0.5, 3.0, 20)
        elif feat == 'speed':
            grid_values[feat] = np.linspace(5, 40, 20)
        else:
            grid_values[feat] = np.linspace(0.5, 2.0, 20)
    
    best_params = None
    best_error = float('inf')
    
    for v1 in grid_values[top3_features[0]]:
        for v2 in grid_values[top3_features[1]]:
            for v3 in grid_values[top3_features[2]]:
                test_params = base_params.copy()
                test_params[top3_features[0]] = v1
                test_params[top3_features[1]] = v2
                test_params[top3_features[2]] = v3
                
                # Build and normalize feature vector
                x_test = np.array([test_params[f] for f in FEATURES])
                x_test_norm = (x_test - mean) / std
                
                # Predict
                pred_o2 = model.predict(x_test_norm.reshape(1, -1))[0]
                error = abs(pred_o2 - target_o2)
                
                if error < best_error:
                    best_error = error
                    best_params = test_params.copy()
    
    # ========== PRINT RESULTS ==========
    x_best = np.array([best_params[f] for f in FEATURES])
    x_best_norm = (x_best - mean) / std
    predicted_o2 = model.predict(x_best_norm.reshape(1, -1))[0]
    
    print("\n" + "="*70)
    print(f"SOLUTION FOR TARGET O2 = {target_o2:.1f}%")
    print("="*70)
    
    print("\nOPTIMAL PARAMETERS:")
    print("-"*70)
    print(f"Speed              = {best_params['speed']:.2f}")
    print(f"VerticalSpeed      = {best_params['verticalSpeed']:.2f}")
    print(f"IdleUpwardSpeed    = {best_params['idleUpwardSpeed']:.2f}")
    print(f"LifeTime           = {best_params['lifeTime']:.2f}")
    print(f"O2DropPerSec       = {best_params['downHealthPairSec']:.2f}")
    print(f"CollisionDamage    = {best_params['removeHealthWithCollide']:.2f}")
    print(f"TimeBetweenCollide = {best_params['timeBetweenCollides']:.2f}")
    print(f"HealPoints         = {best_params['healHealthPoint']:.2f}")
    
    print("\n" + "-"*70)
    print(f"PREDICTED O2: {predicted_o2:.2f}%")
    print(f"TARGET O2:    {target_o2:.1f}%")
    print(f"ERROR:        {abs(predicted_o2 - target_o2):.2f}%")
    print("="*70 + "\n")

if __name__ == "__main__":
    main()
