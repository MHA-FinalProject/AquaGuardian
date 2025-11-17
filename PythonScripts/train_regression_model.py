"""Train regression models from trial CSV data and export them to JSON."""

import json
import os
import sys
import warnings
import traceback

import numpy as np
import pandas as pd
from sklearn.linear_model import Ridge, ElasticNet, HuberRegressor
from sklearn.cross_decomposition import PLSRegression
from sklearn.model_selection import cross_val_score
from sklearn.metrics import mean_absolute_error, r2_score, mean_squared_error

warnings.filterwarnings("ignore")

# Feature names definition (based on Unity TrialDataModels.cs)
FEATURE_NAMES = [
    "speed",
    "verticalSpeed",
    "idleUpwardSpeed",
    "lifeTime",
    "RemoveHealthEveryLifeTime",
    "removeHealthWithCollide",
    "timeBetweenCollides",
    "healHealthPoint",
    "factorForce",
    "EffectiveDrainRate",
]


def _extract_feature_value(dataframe: pd.DataFrame, feature_name: str) -> np.ndarray:
    """Extract a single feature column, handling missing columns."""
    if feature_name in dataframe.columns:
        return dataframe[feature_name].to_numpy()

    if feature_name == "factorForce":
        # Use factorForce only when Amadeo mode exists, otherwise 0
        if "IsAmadeoMode" in dataframe.columns:
            amadeo_mask = (dataframe["IsAmadeoMode"] > 0.5).astype(float).to_numpy()
            factor_force_series = dataframe.get("factorForce", 0)
            factor_force = amadeo_mask * factor_force_series.to_numpy()
            return factor_force
        print(f"   -> {feature_name}: Using 0 (no Amadeo mode detected)")
        return np.zeros(len(dataframe), dtype=float)

    if feature_name == "EffectiveDrainRate":
        # EffectiveDrainRate = RemoveHealthEveryLifeTime / lifeTime
        if (
            "RemoveHealthEveryLifeTime" in dataframe.columns
            and "lifeTime" in dataframe.columns
        ):
            num = dataframe["RemoveHealthEveryLifeTime"].to_numpy()
            den = np.maximum(dataframe["lifeTime"].to_numpy(), 0.1)
            edr = num / den
            print(
                f"   -> {feature_name}: "
                "Calculated from RemoveHealthEveryLifeTime / lifeTime"
            )
            return edr
        print(f"   -> {feature_name}: Using 0 (cannot calculate)")
        return np.zeros(len(dataframe), dtype=float)

    print(f"   -> {feature_name}: Using 0 (column missing)")
    return np.zeros(len(dataframe), dtype=float)


def _extract_features(dataframe: pd.DataFrame) -> np.ndarray:
    """Extract all features from dataframe into a 2D numpy array."""
    feature_data = [
        _extract_feature_value(dataframe, feature_name)
        for feature_name in FEATURE_NAMES
    ]
    return np.column_stack(feature_data)


def _find_target_column(dataframe: pd.DataFrame) -> str:
    """Find the target oxygen column in the dataframe."""
    # Try known column names first
    known_names = [
        "FinalOxygenRemaining",
        "finalOxygenRemaining",
        "FinalOxygen",
        "Oxygen",
        "o2_result",
    ]
    for possible_name in known_names:
        if possible_name in dataframe.columns:
            return possible_name

    # Look for O2 columns
    oxygen_columns = [
        col for col in dataframe.columns if col.lower().startswith("o2")
    ]
    if oxygen_columns:
        # Find the last column with at least one valid value
        for col in reversed(oxygen_columns):
            values = dataframe[col].to_numpy()
            if not np.isnan(values).all():
                print(f"   Found last O2 column with data: {col}")
                return col
        # Fallback to the last oxygen column
        target_col = oxygen_columns[-1]
        print(f"   Using last O2 column: {target_col} (may contain NaN)")
        return target_col

    raise ValueError(
        "Could not find oxygen target column in CSV. Expected: "
        "FinalOxygenRemaining, finalOxygenRemaining, FinalOxygen, Oxygen, "
        "o2_result, or o2_run*"
    )


def load_trial_data(
    csv_path: str,
) -> tuple[np.ndarray, np.ndarray, pd.DataFrame]:
    """Load trial data from CSV and return (features, target, dataframe)."""
    dataframe = pd.read_csv(csv_path)

    print(f"Available columns in CSV: {list(dataframe.columns)}")

    # Verify that all required columns exist
    missing_cols = [col for col in FEATURE_NAMES if col not in dataframe.columns]
    if missing_cols:
        print(f"Warning: Missing columns in CSV: {missing_cols}")
        print("   Will calculate or use default values for missing columns")

    # Extract features
    features = _extract_features(dataframe)

    # Find target column
    target_col = _find_target_column(dataframe)
    target = dataframe[target_col].to_numpy()

    # Remove rows with NaN in target
    valid_mask = ~np.isnan(target)
    if not valid_mask.all():
        n_removed = (~valid_mask).sum()
        print(f"Warning: Removing {n_removed} rows with NaN in target column")
        target = target[valid_mask]
        features = features[valid_mask]
        dataframe = dataframe.loc[valid_mask].reset_index(drop=True)

    if len(target) == 0:
        raise ValueError(
            f"No valid samples found in target column {target_col} (all NaN)"
        )

    print(f"Using target column: {target_col}")
    print(
        f"   Valid samples: {len(target)}, "
        f"Target range: {target.min():.2f} - {target.max():.2f}%"
    )

    return features, target, dataframe


def train_and_export_model(
    csv_path: str,
    output_json: str = "regression_model.json",
    model_type: str = "ElasticNet",
) -> dict:
    """
    Train a regression model and export it to JSON.

    Args:
        csv_path: Path to the CSV file with trial data.
        output_json: Output JSON path.
        model_type: 'Ridge', 'ElasticNet', 'Huber', or 'PLS'.

    Returns:
        A dictionary representing the trained model data (also saved to JSON).
    """
    print(f"Loading data from {csv_path}...")
    features, target, _ = load_trial_data(csv_path)

    n_samples, n_features = features.shape
    print(f"Dataset: {n_samples} samples, {n_features} features")
    print(f"   Target range: {target.min():.2f} - {target.max():.2f}%")
    print(f"   Target mean: {target.mean():.2f}%")

    # Compute statistics for z-score normalization
    means = np.mean(features, axis=0)
    stds = np.std(features, axis=0, ddof=1)  # ddof=1 for sample std

    # Handle zero standard deviation (constant features)
    stds = np.where(stds < 1e-9, 1.0, stds)

    # Normalize
    features_normalized = (features - means) / stds

    # Select model
    print(f"Training {model_type} model...")

    if model_type == "Ridge":
        model = Ridge(alpha=0.5)
    elif model_type == "ElasticNet":
        model = ElasticNet(alpha=0.5, l1_ratio=0.3, max_iter=2000)
    elif model_type == "Huber":
        model = HuberRegressor(epsilon=1.35, alpha=0.5, max_iter=200)
    elif model_type == "PLS":
        n_components = min(3, n_features, n_samples - 1)
        model = PLSRegression(n_components=n_components)
    else:
        raise ValueError(f"Unknown model type: {model_type}")

    # Train
    model.fit(features_normalized, target)

    # Compute performance metrics
    predictions = model.predict(features_normalized)
    train_mae = mean_absolute_error(target, predictions)
    train_r2 = r2_score(target, predictions)
    train_rmse = np.sqrt(mean_squared_error(target, predictions))

    print("Training complete!")
    print(f"   MAE: {train_mae:.3f}%")
    print(f"   RMSE: {train_rmse:.3f}%")
    print(f"   R^2: {train_r2:.3f}")

    # Cross-validation (if enough samples)
    if n_samples >= 5:
        cv_folds = min(5, n_samples)
        cv_scores = cross_val_score(
            model,
            features_normalized,
            target,
            cv=cv_folds,
            scoring="neg_mean_absolute_error",
        )
        print(
            f"   CV MAE ({cv_folds}-fold): {-cv_scores.mean():.3f} "
            f"+/- {cv_scores.std():.3f}%"
        )

    # Extract coefficients
    if model_type == "PLS":
        # coef_ shape (n_features, n_targets) -> flatten to 1D
        betas = model.coef_.ravel()
        # Intercept in normalized space = prediction at zero-vector
        zero_input = np.zeros((1, n_features), dtype=float)
        intercept = float(model.predict(zero_input)[0])
    else:
        betas = model.coef_
        intercept = float(model.intercept_)

    # Prepare JSON payload
    model_data: dict[str, object] = {
        "feature_names": FEATURE_NAMES,
        "intercept": float(intercept),
        "betas": [float(beta) for beta in betas],
        "means": [float(mean_val) for mean_val in means],
        "stds": [float(std_val) for std_val in stds],
        "model_type": model_type,
        "n_samples": int(n_samples),
        "n_features": int(n_features),
        "train_mae": float(train_mae),
        "train_r2": float(train_r2),
        "train_rmse": float(train_rmse),
    }

    # Add model-specific parameters
    if model_type == "Ridge":
        model_data["alpha"] = float(model.alpha)
    elif model_type == "ElasticNet":
        model_data["alpha"] = float(model.alpha)
        model_data["l1_ratio"] = float(model.l1_ratio)
    elif model_type == "Huber":
        model_data["epsilon"] = float(model.epsilon)
        model_data["alpha"] = float(model.alpha)
    elif model_type == "PLS":
        model_data["n_components"] = int(model.n_components)

    # Save to JSON
    try:
        output_dir = os.path.dirname(output_json) or "."
        if output_dir and not os.path.exists(output_dir):
            print(f"Creating directory: {output_dir}")
            os.makedirs(output_dir, exist_ok=True)
            if not os.path.exists(output_dir):
                raise OSError(f"Failed to create directory: {output_dir}")

        # Convert to absolute path for clarity
        abs_output_path = os.path.abspath(output_json)
        print(f"Saving model to: {abs_output_path}")

        with open(abs_output_path, "w", encoding="utf-8") as json_file:
            json.dump(model_data, json_file, indent=2)

        # Verify file was created
        if not os.path.exists(abs_output_path):
            raise OSError(f"File was not created: {abs_output_path}")

        file_size = os.path.getsize(abs_output_path)
        print("Model exported successfully!")
        print(f"   Path: {abs_output_path}")
        print(f"   Size: {file_size} bytes")

    except OSError as exc:
        print(f"Error saving model to {output_json}: {exc}")
        traceback.print_exc()
        raise

    # Print feature importance
    print("\nFeature Importance (|beta|):")
    importance = [
        (name, abs(beta_val))
        for name, beta_val in zip(FEATURE_NAMES, betas)
    ]
    importance_sorted = sorted(importance, key=lambda item: item[1], reverse=True)

    for index, (name, importance_value) in enumerate(importance_sorted, start=1):
        print(f"   {index:2d}. {name:30s}: {importance_value:.4f}")

    return model_data


if __name__ == "__main__":
    # Usage: python train_regression_model.py <csv_path> [model_type] [output_path]
    if len(sys.argv) < 2:
        print(
            "Usage: python train_regression_model.py "
            "<csv_path> [model_type] [output_path]"
        )
        print("Example: python train_regression_model.py trial_data.csv ElasticNet")
        print("Example: python train_regression_model.py trial_data.csv all")
        sys.exit(1)

    csv_path_arg = sys.argv[1]
    model_type_arg = sys.argv[2] if len(sys.argv) > 2 else "ElasticNet"
    output_path_arg = sys.argv[3] if len(sys.argv) > 3 else None

    if not os.path.exists(csv_path_arg):
        print(f"Error: CSV file not found: {csv_path_arg}")
        sys.exit(1)

    # Try one or multiple models
    models_to_try = (
        [model_type_arg] if model_type_arg != "all" else ["ElasticNet", "Ridge", "Huber"]
    )

    for model_name in models_to_try:
        print(f"\n{'=' * 60}")
        if output_path_arg:
            output_file = output_path_arg
        else:
            output_file = f"regression_model_{model_name.lower()}.json"
        try:
            train_and_export_model(csv_path_arg, output_file, model_name)
        except Exception as exc:  # pylint: disable=broad-exception-caught
            print(f"Failed to train {model_name}: {exc}")
            traceback.print_exc()
