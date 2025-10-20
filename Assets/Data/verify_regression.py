#!/usr/bin/env python3
"""
Script to verify Ridge Regression results from Unity C# implementation
Validates the ML predictions against ground truth data

Author: Verification Script
Date: 2025-10-19
"""

import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.linear_model import Ridge
from sklearn.preprocessing import StandardScaler
from sklearn.model_selection import KFold
from sklearn.metrics import mean_squared_error, mean_absolute_error, r2_score
import sys

# Feature names (order must match C# code)
FEATURE_NAMES = [
    'speed',
    'verticalSpeed', 
    'idleUpwardSpeed',
    'lifeTime',
    'downHealthPairSec',
    'removeHealthWithCollide',
    'timeBetweenCollides',
    'healHealthPoint'
]

# Ridge regularization strength (must match C# lambda)
RIDGE_LAMBDA = 0.5

def load_trial_data(csv_path='Trials/Trial_5_runs_.csv'):
    """
    Load trial data from CSV
    Returns X (features), Y (oxygen), trial_params (full data)
    """
    print(f"\n{'='*60}")
    print(f"LOADING DATA FROM: {csv_path}")
    print(f"{'='*60}")
    
    df = pd.read_csv(csv_path)
    print(f"Loaded {len(df)} trials")
    print(f"Columns: {list(df.columns)}")
    
    # Extract features (X)
    X = df[FEATURE_NAMES].values
    print(f"\nFeatures shape: {X.shape}")
    print(f"Features:\n{pd.DataFrame(X, columns=FEATURE_NAMES)}")
    
    # Extract target (Y) - calculate median/mean oxygen from all runs
    o2_columns = [col for col in df.columns if col.startswith('o2_run')]
    print(f"\nFound {len(o2_columns)} oxygen measurement columns")
    
    Y = []
    for idx, row in df.iterrows():
        o2_values = []
        for col in o2_columns:
            val = row[col]
            if pd.notna(val) and val != '':
                try:
                    o2_values.append(float(val))
                except (ValueError, TypeError):
                    pass
        
        if o2_values:
            # Use last valid value (matches C# behavior)
            final_o2 = o2_values[-1]
            Y.append(final_o2)
            print(f"Trial {idx+1}: {len(o2_values)} runs, final O2 = {final_o2}%")
        else:
            print(f"Trial {idx+1}: NO VALID DATA - skipping")
            Y.append(np.nan)
    
    Y = np.array(Y)
    
    # Remove trials with NaN oxygen values
    valid_mask = ~np.isnan(Y)
    X = X[valid_mask]
    Y = Y[valid_mask]
    trial_params = df[valid_mask].copy()
    
    print(f"\nValid trials: {len(Y)}/{len(df)}")
    print(f"Target (Oxygen): {Y}")
    print(f"Average Oxygen: {Y.mean():.1f}%")
    
    return X, Y, trial_params

def train_ridge_model(X, Y, normalize=True, lambda_val=RIDGE_LAMBDA):
    """
    Train Ridge Regression model (matches C# implementation)
    Returns: model, scaler, predictions, metrics
    """
    print(f"\n{'='*60}")
    print(f"TRAINING RIDGE REGRESSION MODEL")
    print(f"{'='*60}")
    print(f"Samples: {len(X)}")
    print(f"Features: {X.shape[1]}")
    print(f"Lambda (regularization): {lambda_val}")
    print(f"Normalization: {normalize}")
    
    # Normalize features (z-score, matches C# FeatureNormalizer)
    scaler = None
    X_processed = X.copy()
    
    if normalize:
        scaler = StandardScaler()
        X_processed = scaler.fit_transform(X)
        print("\nFeatures normalized (StandardScaler)")
        print(f"Mean: {scaler.mean_}")
        print(f"Std: {scaler.scale_}")
    
    # Train Ridge model
    # Note: sklearn uses alpha = lambda (same terminology)
    model = Ridge(alpha=lambda_val, fit_intercept=True, solver='cholesky')
    model.fit(X_processed, Y)
    
    print("\n=== MODEL COEFFICIENTS ===")
    print(f"Intercept (β₀): {model.intercept_:.4f}")
    for i, (name, coef) in enumerate(zip(FEATURE_NAMES, model.coef_)):
        print(f"{name:25s} (β{i+1}): {coef:+.4f}")
    
    # Make predictions
    Y_pred = model.predict(X_processed)
    
    # Calculate metrics
    rmse = np.sqrt(mean_squared_error(Y, Y_pred))
    mae = mean_absolute_error(Y, Y_pred)
    r2 = r2_score(Y, Y_pred)
    
    # Adjusted R2
    n = len(Y)
    k = X.shape[1]
    adj_r2 = 1 - ((1 - r2) * (n - 1) / (n - k - 1)) if (n - k - 1) > 0 else np.nan
    
    print("\n=== MODEL METRICS ===")
    print(f"R2 Score: {r2:.4f} ({r2*100:.1f}% variance explained)")
    if not np.isnan(adj_r2):
        print(f"Adjusted R2: {adj_r2:.4f}")
    print(f"RMSE: {rmse:.3f}%")
    print(f"MAE: {mae:.3f}%")
    print(f"MSE: {rmse**2:.3f}")
    
    return model, scaler, Y_pred, {
        'rmse': rmse,
        'mae': mae,
        'r2': r2,
        'adj_r2': adj_r2
    }

def kfold_cross_validation(X, Y, normalize=True, lambda_val=RIDGE_LAMBDA, n_folds=5):
    """
    Perform K-Fold Cross Validation (matches C# implementation)
    """
    print(f"\n{'='*60}")
    print(f"K-FOLD CROSS VALIDATION ({n_folds} folds)")
    print(f"{'='*60}")
    
    # Adjust folds if we have too few samples
    n_folds = min(n_folds, max(2, len(X)))
    
    kf = KFold(n_splits=n_folds, shuffle=True, random_state=42)
    
    fold_rmse = []
    fold_mae = []
    fold_r2 = []
    
    for fold_idx, (train_idx, test_idx) in enumerate(kf.split(X)):
        X_train, X_test = X[train_idx], X[test_idx]
        Y_train, Y_test = Y[train_idx], Y[test_idx]
        
        # Need enough training samples
        if len(X_train) < X.shape[1] + 1:
            print(f"Fold {fold_idx+1}: Not enough training samples, skipping")
            continue
        
        # Normalize
        X_train_proc = X_train
        X_test_proc = X_test
        if normalize:
            scaler = StandardScaler()
            X_train_proc = scaler.fit_transform(X_train)
            X_test_proc = scaler.transform(X_test)
        
        # Train model on this fold
        model = Ridge(alpha=lambda_val, fit_intercept=True, solver='cholesky')
        model.fit(X_train_proc, Y_train)
        
        # Predict on test fold
        Y_pred = model.predict(X_test_proc)
        
        # Calculate metrics
        rmse = np.sqrt(mean_squared_error(Y_test, Y_pred))
        mae = mean_absolute_error(Y_test, Y_pred)
        r2 = r2_score(Y_test, Y_pred)
        
        fold_rmse.append(rmse)
        fold_mae.append(mae)
        fold_r2.append(r2)
        
        print(f"Fold {fold_idx+1}: RMSE={rmse:.3f}, MAE={mae:.3f}, R2={r2:.3f}")
    
    if len(fold_rmse) == 0:
        print("WARNING: No valid folds!")
        return np.nan, np.nan, np.nan
    
    avg_rmse = np.mean(fold_rmse)
    avg_mae = np.mean(fold_mae)
    avg_r2 = np.mean(fold_r2)
    
    print(f"\n=== CV AVERAGE ===")
    print(f"RMSE: {avg_rmse:.3f}%, MAE: {avg_mae:.3f}%, R2: {avg_r2:.3f}")
    
    # Assess quality
    quality = "Excellent" if avg_r2 > 0.9 else "Good" if avg_r2 > 0.7 else "Fair" if avg_r2 > 0.5 else "Poor"
    print(f"Model Quality: {quality}")
    
    return avg_rmse, avg_mae, avg_r2

def calculate_feature_importance(model, scaler=None):
    """
    Calculate feature importance (absolute coefficient values)
    """
    print(f"\n{'='*60}")
    print(f"FEATURE IMPORTANCE")
    print(f"{'='*60}")
    
    # Get absolute coefficients
    importance = np.abs(model.coef_)
    
    # Sort by importance
    sorted_idx = np.argsort(importance)[::-1]
    
    print("(Impact on oxygen level)\n")
    
    max_importance = importance[sorted_idx[0]]
    for idx in sorted_idx[:5]:  # Top 5 features
        name = FEATURE_NAMES[idx]
        value = importance[idx]
        bar_length = int((value / max_importance) * 200) if max_importance > 0 else 0
        bar = '#' * bar_length
        print(f"{name}:")
        print(f"  {value:.4f} {bar}")
    
    return importance

def validate_predictions(Y_true, Y_pred, trial_params):
    """
    Print detailed prediction validation
    """
    print(f"\n{'='*60}")
    print(f"MODEL PREDICTIONS (Actual vs Predicted)")
    print(f"{'='*60}\n")
    
    errors = []
    for i in range(len(Y_true)):
        actual = Y_true[i]
        predicted = Y_pred[i]
        error = abs(predicted - actual)
        errors.append(error)
        
        print(f"Trial {i+1}:")
        print(f"  Actual: {actual:.1f}%  Predicted: {predicted:.1f}%")
        print(f"  Error: {error:.1f}%\n")
    
    avg_error = np.mean(errors)
    print(f"Average Prediction Error: {avg_error:.2f}%")
    
    return avg_error

def compare_with_csharp_results(regression_file='RegressionResults/RegressionAnalysis_2025-10-19_15-07-01.txt'):
    """
    Load C# regression results and compare
    """

    
    if not Path(regression_file).exists():
        print(f"C# results file not found: {regression_file}")
        return
    
    with open(regression_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    print(f"Loaded C# results from: {regression_file}")
    
    # Extract key metrics from C# output
    lines = content.split('\n')
    for line in lines:
        if 'Average Oxygen:' in line:
            print(f"C# {line.strip()}")
        elif 'Average Prediction Error:' in line:
            print(f"C# {line.strip()}")
        elif 'Cross-Val RMSE:' in line:
            print(f"C# {line.strip()}")
        elif 'Cross-Val R2:' in line:
            print(f"C# {line.strip()}")
    
    print("\nIf Python and C# results match closely, the implementation is correct!")

def main():
    """Main verification workflow"""
    print("="*60)
    print("RIDGE REGRESSION VERIFICATION SCRIPT")
    print("Validates Unity C# implementation against Python/scikit-learn")
    print("="*60)
    
    # Change to Data directory
    script_dir = Path(__file__).parent
    csv_path = script_dir / 'Trials' / 'Trial_5_runs_.csv'
    
    if not csv_path.exists():
        print(f"ERROR: {csv_path} not found!")
        sys.exit(1)
    
    # 1. Load data
    X, Y, trial_params = load_trial_data(csv_path)
    
    if len(Y) < 3:
        print(f"ERROR: Need at least 3 valid trials, got {len(Y)}")
        sys.exit(1)
    
    # 2. Train model
    model, scaler, Y_pred, metrics = train_ridge_model(X, Y, normalize=True)
    
    # 3. Validate predictions
    avg_error = validate_predictions(Y, Y_pred, trial_params)
    
    # 4. Calculate feature importance
    importance = calculate_feature_importance(model, scaler)
    
    # 5. K-Fold Cross Validation
    cv_rmse, cv_mae, cv_r2 = kfold_cross_validation(X, Y, normalize=True, n_folds=5)
    
    # 6. Compare with C# results
    compare_with_csharp_results()
    
    # 7. Summary
    print(f"\n{'='*60}")
    print("VERIFICATION SUMMARY")
    print(f"{'='*60}")
    print(f" Data loaded: {len(Y)} valid trials")
    print(f" Model trained: Ridge (λ={RIDGE_LAMBDA})")
    print(f" R2 Score: {metrics['r2']:.4f}")
    print(f" RMSE: {metrics['rmse']:.2f}%")
    print(f" Avg Prediction Error: {avg_error:.2f}%")
    print(f" CV RMSE: {cv_rmse:.2f}%")
    print(f" CV R2: {cv_r2:.3f}")
    
    # Quality assessment
    if metrics['r2'] > 0.8 and avg_error < 5.0:
        print("\n EXCELLENT: Model predictions are highly accurate!")
    elif metrics['r2'] > 0.6 and avg_error < 10.0:
        print("\n GOOD: Model predictions are reasonably accurate")
    else:
        print("\n  WARNING: Model may need more training data or feature engineering")
    
    print(f"\n{'='*60}")
    print("Verification complete!")
    print(f"{'='*60}\n")

if __name__ == '__main__':
    main()

