"""
AquaGuardian Trial Regression System - Python Verification
===========================================================

This script verifies the Unity regression system works correctly.
It loads trial data from CSV, performs linear regression, calculates
coefficients, and finds optimal parameters using Gradient Descent.

Usage:
    Interactive mode (will ask which CSV to analyze):
        python test_regression_system.py
    
    Non-interactive mode:
        python test_regression_system.py --csv Trial_Random_Parameters.csv
        python test_regression_system.py --csv Trial_5_runs_.csv
"""

import numpy as np
import pandas as pd
from sklearn.linear_model import Ridge
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import r2_score, mean_squared_error, mean_absolute_error
import os
import sys
import argparse
import glob

# ============================================================================
# CONFIGURATION
# ============================================================================

# Data directory
DATA_DIR = "Assets/Data/Trials/"

# Target difficulty
TARGET_O2 = 10.0  # Want 10% oxygen remaining

# Parameter ranges (matching Unity - UPDATED with expanded ranges for difficulty)
PARAM_RANGES = {
    'speed': (10.0, 40.0),
    'verticalSpeed': (15.0, 45.0),
    'idleUpwardSpeed': (0.01, 3.0),          # Changed: 5→3 (more realistic)
    'lifeTime': (0.5, 4.0),
    'downHealthPairSec': (0.5, 7.0),         # Changed: 5→7 (more O2 drop)
    'removeHealthWithCollide': (5.0, 20.0),  # Changed: 15→20 (more damage)
    'timeBetweenCollides': (1.0, 5.0),
    'healHealthPoint': (3.0, 15.0),
    'factorForce': (0.5, 5.0),               # Matching Unity's 5.0
}

# Feature names (in order) - NOW INCLUDING factorForce!
FEATURE_NAMES = [
    'speed',
    'verticalSpeed', 
    'idleUpwardSpeed',
    'lifeTime',
    'downHealthPairSec',
    'removeHealthWithCollide',
    'timeBetweenCollides',
    'healHealthPoint',
    'factorForce'  # NEW: Factor multiplication
]

# ============================================================================
# CSV FILE SELECTION
# ============================================================================

def find_available_csvs():
    """
    Find all available CSV files in the data directory.
    
    Returns:
        List of tuples (filename, full_path)
    """
    if not os.path.exists(DATA_DIR):
        return []
    
    csv_files = glob.glob(os.path.join(DATA_DIR, "*.csv"))
    return [(os.path.basename(f), f) for f in csv_files]

def select_csv_interactive():
    """
    Interactively select a CSV file from available options.
    
    Returns:
        Selected CSV path or None if cancelled
    """
    available = find_available_csvs()
    
    if not available:
        print(f"\nERROR: No CSV files found in {DATA_DIR}")
        return None
    
    print("\n" + "="*70)
    print("AVAILABLE CSV FILES")
    print("="*70)
    
    for idx, (filename, _) in enumerate(available, 1):
        print(f"  {idx}. {filename}")
    
    print("\nEnter the number of the CSV file you want to analyze.")
    print("(Press Enter for default, or 'q' to quit)")
    
    while True:
        choice = input("\nYour choice: ").strip().lower()
        
        if choice == 'q':
            print("Cancelled.")
            return None
        
        if choice == '':
            # Default to first file
            selected_path = available[0][1]
            print(f"→ Using default: {available[0][0]}")
            return selected_path
        
        try:
            idx = int(choice) - 1
            if 0 <= idx < len(available):
                selected_path = available[idx][1]
                print(f"→ Selected: {available[idx][0]}")
                return selected_path
            else:
                print(f"Invalid choice. Please enter 1-{len(available)}")
        except ValueError:
            print("Invalid input. Please enter a number.")

# ============================================================================
# DATA LOADING
# ============================================================================

def load_trial_data(csv_path, use_random=False):
    """
    Load trial data from CSV file.
    
    Args:
        csv_path: Path to CSV file
        use_random: If True, load from random parameters CSV
        
    Returns:
        DataFrame with trial data
    """
    if not os.path.exists(csv_path):
        print(f"ERROR: File not found: {csv_path}")
        return None
    
    print(f"\n{'='*70}")
    print(f"LOADING DATA FROM: {csv_path}")
    print(f"Mode: {'RANDOM Parameters' if use_random else 'REGULAR Parameters'}")
    print(f"{'='*70}")
    
    df = pd.read_csv(csv_path)
    
    # Find oxygen columns
    o2_cols = [col for col in df.columns if col.lower().startswith('o2_run') or col.lower() == 'o2_result']
    
    if not o2_cols:
        print("ERROR: No oxygen columns found!")
        return None
    
    print(f"\nFound {len(o2_cols)} oxygen columns: {o2_cols}")
    
    # Use last oxygen column (most recent run)
    o2_col = o2_cols[-1]
    print(f"Using oxygen column: {o2_col}")
    
    # Extract data (skip rows with NaN oxygen values)
    trials = []
    for idx, row in df.iterrows():
        o2_value = row[o2_col]
        
        # Skip if oxygen value is NaN or empty
        if pd.isna(o2_value):
            print(f"  Skipping trial {idx + 1}: No oxygen data")
            continue
        
        try:
            trial = {
                'trialId': int(row.get('trialId', row.get('trial_id', idx + 1))),
                'speed': float(row['speed']),
                'verticalSpeed': float(row['verticalSpeed']),
                'idleUpwardSpeed': float(row['idleUpwardSpeed']),
                'lifeTime': float(row['lifeTime']),
                'downHealthPairSec': float(row['downHealthPairSec']),
                'removeHealthWithCollide': float(row['removeHealthWithCollide']),
                'timeBetweenCollides': float(row['timeBetweenCollides']),
                'healHealthPoint': float(row['healHealthPoint']),
                'factorForce': float(row.get('factorForce', 1.0)),  # Default to 1.0 if missing
                'finalO2': float(o2_value)
            }
            trials.append(trial)
        except (ValueError, KeyError) as e:
            print(f"  Skipping trial {idx + 1}: Invalid data ({e})")
            continue
    
    df_trials = pd.DataFrame(trials)
    
    # Take only first 5 trials (to match Unity behavior)
    if len(df_trials) > 5:
        print(f"\nUsing first 5 trials (out of {len(df_trials)} available)")
        df_trials = df_trials.head(5)
    
    print(f"\nLoaded {len(df_trials)} trials")
    print(f"O2 Range: {df_trials['finalO2'].min():.1f}% - {df_trials['finalO2'].max():.1f}%")
    print(f"O2 Average: {df_trials['finalO2'].mean():.1f}%")
    
    return df_trials

# ============================================================================
# REGRESSION MODEL
# ============================================================================

def train_regression_model(df_trials):
    """
    Train Ridge regression model on trial data.
    
    Args:
        df_trials: DataFrame with trial data
        
    Returns:
        Tuple of (model, scaler, X, y, feature_names)
    """
    print(f"\n{'='*70}")
    print("TRAINING REGRESSION MODEL")
    print(f"{'='*70}")
    
    # Prepare features (X) and target (y)
    X = df_trials[FEATURE_NAMES].values
    y = df_trials['finalO2'].values
    
    print(f"\nSamples: {len(X)}")
    print(f"Features: {len(FEATURE_NAMES)}")
    
    # Normalize features
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)
    
    # Train Ridge regression (regularization prevents overfitting)
    model = Ridge(alpha=0.5)  # lambda = 0.5 (matching Unity)
    model.fit(X_scaled, y)
    
    # Calculate metrics
    y_pred = model.predict(X_scaled)
    r2 = r2_score(y, y_pred)
    rmse = np.sqrt(mean_squared_error(y, y_pred))
    mae = mean_absolute_error(y, y_pred)
    
    print(f"\nModel Quality:")
    print(f"  R² Score:  {r2:.4f} ({r2*100:.2f}% variance explained)")
    print(f"  RMSE:      {rmse:.3f}%")
    print(f"  MAE:       {mae:.3f}%")
    
    if r2 > 0.9:
        print("  Quality:   EXCELLENT")
    elif r2 > 0.7:
        print("  Quality:   GOOD")
    else:
        print("  Quality:   MODERATE")
    
    # Print predictions vs actual
    print(f"\n{'Trial':<8} {'Actual':<10} {'Predicted':<10} {'Error':<10}")
    print(f"{'-'*40}")
    for i, (actual, pred) in enumerate(zip(y, y_pred)):
        error = abs(actual - pred)
        print(f"{i+1:<8} {actual:<10.1f} {pred:<10.1f} {error:<10.1f}")
    
    return model, scaler, X, y, FEATURE_NAMES

# ============================================================================
# COEFFICIENT ANALYSIS
# ============================================================================

def analyze_coefficients(model, scaler, feature_names):
    """
    Analyze and display regression coefficients.
    
    Args:
        model: Trained Ridge model
        scaler: Feature scaler
        feature_names: List of feature names
    """
    print(f"\n{'='*70}")
    print("REGRESSION COEFFICIENTS (β)")
    print(f"{'='*70}")
    
    # Get coefficients (need to denormalize)
    coefficients = model.coef_
    intercept = model.intercept_
    
    print(f"\nβ0 (Intercept) = {intercept:.4f}")
    print(f"\nFeature Coefficients:")
    
    # Calculate feature importance (absolute coefficient values)
    importance = []
    for i, (name, coef) in enumerate(zip(feature_names, coefficients)):
        abs_coef = abs(coef)
        importance.append((name, abs_coef, coef))
        sign = '+' if coef >= 0 else ''
        print(f"  β{i+1} ({name:<25}) = {sign}{coef:>8.4f}")
    
    # Sort by importance
    importance.sort(key=lambda x: x[1], reverse=True)
    
    print(f"\n{'='*70}")
    print("FEATURE IMPORTANCE (sorted by impact)")
    print(f"{'='*70}")
    
    for i, (name, abs_coef, coef) in enumerate(importance):
        bar_len = int(abs_coef * 10)
        bar = '#' * min(bar_len, 50)
        direction = "↑ positive" if coef > 0 else "↓ negative"
        print(f"{i+1}. {name:<25} {abs_coef:>6.4f}  {bar}")
        print(f"   {'':27} → {direction} effect on O2")
    
    return importance

# ============================================================================
# GRADIENT DESCENT OPTIMIZATION
# ============================================================================

def gradient_descent_optimize(model, scaler, target_o2, feature_names, 
                               param_ranges, max_iterations=100):
    """
    Find optimal parameters using Gradient Descent.
    
    Args:
        model: Trained regression model
        scaler: Feature scaler
        target_o2: Target oxygen level
        feature_names: List of feature names
        param_ranges: Dictionary of parameter ranges
        max_iterations: Maximum iterations
        
    Returns:
        Dictionary with optimal parameters and metrics
    """
    print(f"\n{'='*70}")
    print(f"GRADIENT DESCENT OPTIMIZATION")
    print(f"{'='*70}")
    print(f"\nTarget O2: {target_o2:.2f}%")
    print(f"Max Iterations: {max_iterations}")
    
    # Start with default parameters (midpoint of ranges)
    params = np.array([
        (param_ranges[name][0] + param_ranges[name][1]) / 2
        for name in feature_names
    ])
    
    # Initial prediction
    params_scaled = scaler.transform([params])
    initial_o2 = model.predict(params_scaled)[0]
    initial_error = abs(initial_o2 - target_o2)
    
    print(f"\nInitial State:")
    print(f"  O2:    {initial_o2:.2f}%")
    print(f"  Error: {initial_error:.2f}%")
    
    # Gradient Descent parameters (OPTIMIZED to match Unity)
    learning_rate = 0.2  # Matching Unity's 0.2 learning rate
    convergence_threshold = 0.5  # More realistic threshold
    momentum = 0.9  # Add momentum for smoother convergence
    
    best_params = params.copy()
    best_error = initial_error
    
    # Momentum tracking
    velocity = np.zeros_like(params)
    
    # Track for oscillation detection
    error_history = []
    o2_history = []
    
    # Dynamic learning rate adjustment
    lr_reduction_count = 0
    max_lr_reductions = 3
    
    print(f"\nOptimizing...")
    
    for iteration in range(max_iterations):
        # Current prediction
        params_scaled = scaler.transform([params])
        current_o2 = model.predict(params_scaled)[0]
        error = current_o2 - target_o2
        
        error_history.append(abs(error))
        o2_history.append(current_o2)
        
        # Check convergence
        if abs(error) < convergence_threshold:
            print(f"\nConverged at iteration {iteration + 1}!")
            break
        
        # Detect oscillation and reduce learning rate
        if len(o2_history) >= 6:
            # Check if O2 is oscillating (going up and down repeatedly)
            last_6 = o2_history[-6:]
            # If we see pattern like: up, down, up, down...
            direction_changes = 0
            for i in range(1, len(last_6) - 1):
                if (last_6[i] > last_6[i-1] and last_6[i] > last_6[i+1]) or \
                   (last_6[i] < last_6[i-1] and last_6[i] < last_6[i+1]):
                    direction_changes += 1
            
            # If oscillating (2+ direction changes in 6 iterations)
            if direction_changes >= 2 and lr_reduction_count < max_lr_reductions:
                learning_rate *= 0.5  # Reduce by half
                lr_reduction_count += 1
                print(f"\n  WARNING: Oscillation detected! Reducing learning rate to {learning_rate:.4f}")
                velocity *= 0  # Reset momentum
        
        # Stop if error not improving after reducing learning rate multiple times
        if len(error_history) > 15:
            recent_15 = error_history[-15:]
            if max(recent_15) - min(recent_15) < 0.5 and abs(error) > convergence_threshold:
                print(f"\nWARNING: Stopped at iteration {iteration + 1}: Error stabilized but not converged")
                break
        
        # Adaptive learning rate (more conservative)
        error_magnitude = abs(error)
        adaptive_lr = learning_rate * min(1.0, error_magnitude / 20.0)
        
        # Log progress
        if iteration < 5 or (iteration + 1) % 10 == 0:
            print(f"  Iter {iteration+1:3d}: O2={current_o2:6.2f}% | Error={abs(error):6.3f}% | LR={adaptive_lr:.4f}")
        
        # Calculate gradients for all parameters
        gradients = np.zeros_like(params)
        
        for i, (name, coef) in enumerate(zip(feature_names, model.coef_)):
            if abs(coef) < 0.0001:
                continue
            
            # Normalized gradient (direction to move to reduce error)
            # If error > 0 (O2 too high), and coef > 0, need to decrease param
            # If error < 0 (O2 too low), and coef > 0, need to increase param
            gradient = -error / coef
            
            # Normalize by coefficient magnitude to prevent large jumps
            normalized_gradient = gradient / (abs(coef) + 1.0)
            gradients[i] = normalized_gradient
        
        # Update velocity with momentum
        velocity = momentum * velocity + (1 - momentum) * gradients
        
        # Update parameters
        any_adjustment = False
        
        for i, name in enumerate(feature_names):
            adjustment = velocity[i] * adaptive_lr
            
            old_value = params[i]
            new_value = old_value + adjustment
            
            # Clamp to valid range
            min_val, max_val = param_ranges[name]
            clamped_value = np.clip(new_value, min_val, max_val)
            
            if abs(clamped_value - old_value) > 0.001:
                params[i] = clamped_value
                any_adjustment = True
        
        # Update best if improved
        if abs(error) < best_error:
            best_error = abs(error)
            best_params = params.copy()
        
        # Stop if no adjustments possible
        if not any_adjustment:
            print(f"\nWARNING: Stopped at iteration {iteration + 1}: All parameters at bounds")
            break
    
    # Final prediction
    final_params_scaled = scaler.transform([best_params])
    final_o2 = model.predict(final_params_scaled)[0]
    final_error = abs(final_o2 - target_o2)
    
    print(f"\n{'='*70}")
    print("OPTIMIZED PARAMETERS")
    print(f"{'='*70}")
    
    for name, value in zip(feature_names, best_params):
        min_val, max_val = param_ranges[name]
        at_bound = ""
        if abs(value - min_val) < 0.01:
            at_bound = " [AT MINIMUM]"
        elif abs(value - max_val) < 0.01:
            at_bound = " [AT MAXIMUM]"
        print(f"  {name:<25} = {value:>7.2f}{at_bound}")
    
    print(f"\n{'='*70}")
    print("RESULT")
    print(f"{'='*70}")
    print(f"  Predicted O2: {final_o2:.2f}%")
    print(f"  Target O2:    {target_o2:.2f}%")
    print(f"  Error:        {final_error:.3f}%")
    
    if final_error < 0.1:
        quality = "EXCELLENT"
    elif final_error < 0.5:
        quality = "VERY GOOD"
    elif final_error < 1.0:
        quality = "GOOD"
    elif final_error < 5.0:
        quality = "FAIR"
    else:
        quality = "POOR - Check parameter ranges"
    
    print(f"  Quality:      {quality}")
    print(f"{'='*70}")
    
    return {
        'params': dict(zip(feature_names, best_params)),
        'predicted_o2': final_o2,
        'error': final_error,
        'quality': quality
    }

# ============================================================================
# MAIN
# ============================================================================

def main():
    """
    Main function - runs the complete verification.
    """
    # Parse command line arguments
    parser = argparse.ArgumentParser(
        description="Verify AquaGuardian ML regression system",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  Interactive mode:
    python test_regression_system.py
  
  Non-interactive mode:
    python test_regression_system.py --csv Trial_Random_Parameters.csv
    python test_regression_system.py --csv Trial_5_runs_.csv
        """
    )
    
    parser.add_argument(
        '--csv',
        type=str,
        help='CSV filename to analyze (from Assets/Data/Trials/)',
        default=None
    )
    
    args = parser.parse_args()
    
    print("\n" + "="*70)
    print("AQUAGUARDIAN REGRESSION SYSTEM VERIFICATION")
    print("="*70)
    
    # Determine which CSV to use
    if args.csv:
        # Non-interactive mode: use specified CSV
        csv_path = os.path.join(DATA_DIR, args.csv)
        if not os.path.exists(csv_path):
            print(f"\nERROR: File not found: {csv_path}")
            print(f"Make sure the file exists in {DATA_DIR}")
            return
        print(f"\n→ Using specified file: {args.csv}")
    else:
        # Interactive mode: let user choose
        csv_path = select_csv_interactive()
        if csv_path is None:
            return
    
    # Determine if this is random parameters CSV
    use_random = 'random' in os.path.basename(csv_path).lower()
    
    # Step 1: Load data
    df_trials = load_trial_data(csv_path, use_random)
    if df_trials is None:
        print("\nERROR: Could not load data!")
        return
    
    # Step 2: Train regression model
    model, scaler, X, y, feature_names = train_regression_model(df_trials)
    
    # Step 3: Analyze coefficients
    importance = analyze_coefficients(model, scaler, feature_names)
    
    # Step 4: Find optimal parameters
    result = gradient_descent_optimize(
        model=model,
        scaler=scaler,
        target_o2=TARGET_O2,
        feature_names=feature_names,
        param_ranges=PARAM_RANGES,
        max_iterations=100
    )
    
    # Summary
    print(f"\n{'='*70}")
    print("VERIFICATION SUMMARY")
    print(f"{'='*70}")
    print(f"\nData loaded:           {len(df_trials)} trials")
    print(f"Model trained:         R² = {r2_score(y, model.predict(scaler.transform(X))):.4f}")
    print(f"Coefficients analyzed: {len(importance)} features")
    print(f"Optimization complete: Error = {result['error']:.3f}%")
    
    if result['error'] < 1.0:
        print(f"\nSUCCESS! System can reach target difficulty!")
    elif result['error'] < 5.0:
        print(f"\nGOOD! System reaches close to target (within 5%)")
    else:
        print(f"\nWARNING! Large error - may need wider parameter ranges")
    
    print(f"\n{'='*70}")

if __name__ == "__main__":
    main()

