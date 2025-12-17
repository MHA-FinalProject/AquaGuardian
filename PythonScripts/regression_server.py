from flask import Flask, request, jsonify, send_file
from flask_cors import CORS
import pandas as pd
import numpy as np
import json
import os
import tempfile
from sklearn.linear_model import Ridge, ElasticNet, HuberRegressor, Lasso
from sklearn.preprocessing import StandardScaler
from sklearn.cross_decomposition import PLSRegression
from sklearn.model_selection import cross_val_score, LeaveOneOut
from sklearn.metrics import mean_absolute_error, r2_score, mean_squared_error
import warnings
warnings.filterwarnings('ignore')

app = Flask(__name__)
CORS(app)  

# 10 features - synchronized with C# (TrialDataModels.FeatureNames)
FEATURE_NAMES = [
    "speed", "verticalSpeed", "idleUpwardSpeed", "lifeTime",
    "RemoveHealthEveryLifeTime", "removeHealthWithCollide",
    "timeBetweenCollides", "healHealthPoint", "factorForce", "EffectiveDrainRate"
]

# Store trained model in memory
current_model = None
model_info = None

def load_trial_data_from_csv(csv_path):
    """Load trial data from CSV."""
    df = pd.read_csv(csv_path)
    
    # Extract features and handle missing columns
    feature_data = []
    for feature_name in FEATURE_NAMES:
        if feature_name == "factorForce":
            if "factorForce" in df.columns:
                factor_force_values = df["factorForce"].values
                if "IsAmadeoMode" in df.columns:
                    amadeo_mask = (df["IsAmadeoMode"] > 0.5).astype(float).values
                    feature_data.append(factor_force_values * amadeo_mask)
                else:
                    feature_data.append(np.zeros(len(df)))
            elif "factor_force" in df.columns:
                feature_data.append(df["factor_force"].values)
            else:
                feature_data.append(np.zeros(len(df)))
        elif feature_name == "EffectiveDrainRate":
            if "EffectiveDrainRate" in df.columns:
                feature_data.append(df["EffectiveDrainRate"].values)
            elif "RemoveHealthEveryLifeTime" in df.columns and "lifeTime" in df.columns:
                numerator = df["RemoveHealthEveryLifeTime"].values
                denominator = np.maximum(df["lifeTime"].values, 0.1)
                effective_drain = numerator / denominator
                feature_data.append(effective_drain)
            else:
                feature_data.append(np.zeros(len(df)))
        elif feature_name in df.columns:
            feature_data.append(df[feature_name].values)
        else:
            feature_data.append(np.zeros(len(df)))
    
    X = np.column_stack(feature_data)
    
    # Find target column
    target_col = None
    for possible_name in ['FinalOxygenRemaining', 'finalOxygenRemaining', 'FinalOxygen', 'Oxygen', 'o2_result']:
        if possible_name in df.columns:
            target_col = possible_name
            break
    
    if target_col is None:
        o2_run_cols = [col for col in df.columns if col.lower().startswith('o2_run')]
        
        def extract_run_number(col_name):
            try:
                return int(col_name.lower().replace("o2_run", ""))
            except:
                return 0
        
        o2_run_cols = sorted(o2_run_cols, key=extract_run_number)
        
        if o2_run_cols:
            for col in reversed(o2_run_cols):
                values = df[col].values
                valid_count = np.sum(~np.isnan(values))
                if valid_count == len(values):
                    target_col = col
                    break
            
            if target_col is None:
                for col in reversed(o2_run_cols):
                    values = df[col].values
                    if not np.isnan(values).all():
                        target_col = col
                        break
            
            if target_col is None:
                target_col = o2_run_cols[-1]
    
    if target_col is None:
        raise ValueError("Could not find oxygen target column")
    
    y = df[target_col].values
    
    # Remove rows with NaN
    X_nan_mask = np.isnan(X).any(axis=1)
    y_nan_mask = np.isnan(y)
    valid_mask = ~(X_nan_mask | y_nan_mask)
    
    if not valid_mask.all():
        n_removed = (~valid_mask).sum()
        y = y[valid_mask]
        X = X[valid_mask]
    
    if len(y) == 0:
        raise ValueError("No valid samples found after removing NaN values")
    
    return X, y, target_col

def train_model(X, y, model_type='ElasticNet'):
    """Train model."""
    if np.isnan(X).any():
        raise ValueError("Input X contains NaN after cleaning. This should not happen!")
    if np.isnan(y).any():
        raise ValueError("Input y contains NaN after cleaning. This should not happen!")
    
    means = np.mean(X, axis=0)
    stds = np.std(X, axis=0, ddof=1)
    stds = np.where(stds < 1e-9, 1.0, stds)
    
    X_normalized = (X - means) / stds
    
    if model_type == 'Ridge':
        model = Ridge(alpha=2.0)
    elif model_type == 'ElasticNet':
        model = ElasticNet(alpha=1.5, l1_ratio=0.5, max_iter=2000)
    elif model_type == 'Huber':
        model = HuberRegressor(epsilon=1.35, alpha=2.0, max_iter=200)
    elif model_type == 'PLS':
        n_components = min(3, X.shape[1], X.shape[0] - 1)
        model = PLSRegression(n_components=n_components)
    else:
        raise ValueError(f"Unknown model type: {model_type}")
    
    model.fit(X_normalized, y)
    
    y_pred = model.predict(X_normalized)
    train_mae = mean_absolute_error(y, y_pred)
    train_r2 = r2_score(y, y_pred)
    train_rmse = np.sqrt(mean_squared_error(y, y_pred))
    
    if model_type == 'PLS':
        betas = model.coef_.flatten() if len(model.coef_.shape) > 1 else model.coef_
        intercept = model._y_mean - np.dot(model._x_mean, model.coef_.ravel())
    else:
        betas = model.coef_
        intercept = model.intercept_
    
    return {
        'means': means.tolist(),
        'stds': stds.tolist(),
        'betas': betas.tolist(),
        'intercept': float(intercept),
        'train_mae': float(train_mae),
        'train_r2': float(train_r2),
        'train_rmse': float(train_rmse),
        'n_samples': int(len(y)),
        'n_features': int(X.shape[1])
    }


def select_top_features_lasso(X, y, n_features=3):
    """
    Select top N features using Lasso regression.
    Returns indices of selected features.
    """
    alpha_range = [10.0, 5.0, 2.0, 1.0, 0.5, 0.2, 0.1, 0.05]
    
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)
    
    best_alpha = None
    best_n_selected = 0
    best_model = None
    
    for alpha in alpha_range:
        lasso = Lasso(alpha=alpha, max_iter=5000, random_state=42)
        lasso.fit(X_scaled, y)
        
        n_selected = np.sum(np.abs(lasso.coef_) > 1e-6)
        
        if best_model is None or abs(n_selected - n_features) < abs(best_n_selected - n_features):
            best_alpha = alpha
            best_n_selected = n_selected
            best_model = lasso
        
        if n_selected == n_features:
            break
    
    coef_abs = np.abs(best_model.coef_)
    selected_indices = np.argsort(coef_abs)[::-1][:n_features]
    
    return selected_indices


@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    return jsonify({'status': 'ok', 'model_loaded': current_model is not None})

@app.route('/train', methods=['POST'])
def train():
    """Train model from CSV."""
    global current_model, model_info
    
    try:
        data = request.json
        csv_path = data.get('csv_path')
        model_type = (data.get('model_type') or 'ElasticNet').strip()
        
        if not csv_path or not os.path.exists(csv_path):
            return jsonify({'error': f'CSV file not found: {csv_path}'}), 400
        
        X, y, target_col = load_trial_data_from_csv(csv_path)
        model_data = train_model(X, y, model_type)
        
        model_data['feature_names'] = FEATURE_NAMES
        model_data['model_type'] = model_type
        model_data['target_column'] = target_col
        
        if model_type == 'Ridge':
            model_data['alpha'] = 2.0
        elif model_type == 'ElasticNet':
            model_data['alpha'] = 1.5
            model_data['l1_ratio'] = 0.5
        
        current_model = model_data
        model_info = {
            'trained_at': pd.Timestamp.now().isoformat(),
            'csv_path': csv_path,
            'target_column': target_col
        }
        
        return jsonify({
            'success': True,
            'model': model_data,
            'info': model_info
        })
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/train_small', methods=['POST'])
def train_small():
    """
    Train model optimized for small datasets (5-10 samples).
    Uses Lasso feature selection + Ridge regression.
    """
    global current_model, model_info
    
    try:
        data = request.json
        csv_path = data.get('csv_path')
        model_type = (data.get('model_type') or 'Ridge').strip()
        n_features = data.get('n_features', 3)
        
        if not csv_path or not os.path.exists(csv_path):
            return jsonify({'error': f'CSV file not found: {csv_path}'}), 400
        
        X, y, target_col = load_trial_data_from_csv(csv_path)
        n_samples = len(y)
        
        # Auto-adjust n_features if needed
        recommended_n = max(2, min(n_features, n_samples - 2))
        if recommended_n != n_features:
            n_features = recommended_n
        
        # Step 1: Feature Selection
        selected_indices = select_top_features_lasso(X, y, n_features=n_features)
        
        # Step 2: Train on selected features
        X_selected = X[:, selected_indices]
        
        means_selected = np.mean(X_selected, axis=0)
        stds_selected = np.std(X_selected, axis=0, ddof=1)
        stds_selected = np.where(stds_selected < 1e-9, 1.0, stds_selected)
        
        X_normalized = (X_selected - means_selected) / stds_selected
        
        if model_type == 'Ridge':
            model = Ridge(alpha=1.0)
        elif model_type == 'ElasticNet':
            model = ElasticNet(alpha=0.5, l1_ratio=0.5, max_iter=5000)
        else:
            model = Ridge(alpha=1.0)
        
        model.fit(X_normalized, y)
        
        # Step 3: Leave-One-Out CV
      
        
        loo = LeaveOneOut()
        predictions = []
        actuals = []
        
        for train_idx, test_idx in loo.split(X_normalized):
            X_train, X_test = X_normalized[train_idx], X_normalized[test_idx]
            y_train, y_test = y[train_idx], y[test_idx]
            
            temp_model = Ridge(alpha=1.0) if model_type == 'Ridge' else ElasticNet(alpha=0.5, l1_ratio=0.5)
            temp_model.fit(X_train, y_train)
            pred = temp_model.predict(X_test)[0]
            
            predictions.append(pred)
            actuals.append(y_test[0])
        
        cv_mae = mean_absolute_error(actuals, predictions)
        cv_rmse = np.sqrt(mean_squared_error(actuals, predictions))
        cv_r2 = r2_score(actuals, predictions)
        
        # Training metrics
        y_pred_train = model.predict(X_normalized)
        train_mae = mean_absolute_error(y, y_pred_train)
        train_rmse = np.sqrt(mean_squared_error(y, y_pred_train))
        train_r2 = r2_score(y, y_pred_train)
        
        print(f"Results: Train MAE={train_mae:.2f}% R^2={train_r2:.3f} | CV MAE={cv_mae:.2f}% R^2={cv_r2:.3f}")
        
        # Step 4: Create full arrays for Unity
        full_means = np.zeros(len(FEATURE_NAMES))
        full_stds = np.ones(len(FEATURE_NAMES))
        full_betas = np.zeros(len(FEATURE_NAMES))
        
        for i, idx in enumerate(selected_indices):
            full_means[idx] = means_selected[i]
            full_stds[idx] = stds_selected[i]
            full_betas[idx] = model.coef_[i]
        
        model_data = {
            'feature_names': FEATURE_NAMES,
            'intercept': float(model.intercept_),
            'betas': [float(b) for b in full_betas],
            'means': [float(m) for m in full_means],
            'stds': [float(s) for s in full_stds],
            'model_type': f"{model_type}_FeatureSelection",
            'n_samples': int(n_samples),
            'n_features': len(FEATURE_NAMES),
            'n_features_selected': len(selected_indices),
            'selected_features': [FEATURE_NAMES[i] for i in selected_indices],
            'selected_indices': [int(i) for i in selected_indices],
            'train_mae': float(train_mae),
            'train_r2': float(train_r2),
            'train_rmse': float(train_rmse),
            'cv_mae': float(cv_mae),
            'cv_r2': float(cv_r2),
            'cv_rmse': float(cv_rmse)
        }
        
        if model_type == 'Ridge':
            model_data['alpha'] = 1.0
        elif model_type == 'ElasticNet':
            model_data['alpha'] = 0.5
            model_data['l1_ratio'] = 0.5
        
        current_model = model_data
        model_info = {
            'trained_at': pd.Timestamp.now().isoformat(),
            'csv_path': csv_path,
            'target_column': target_col,
            'method': 'small_sample_feature_selection'
        }
        
        return jsonify({
            'success': True,
            'model': model_data,
            'info': model_info
        })
        
    except Exception as e:
        import traceback
        return jsonify({'error': str(e), 'traceback': traceback.format_exc()}), 500


@app.route('/model', methods=['GET'])
def get_model():
    """Return trained model."""
    if current_model is None:
        return jsonify({'error': 'No model trained yet. Call /train first.'}), 404
    
    return jsonify({
        'model': current_model,
        'info': model_info
    })

@app.route('/predict', methods=['POST'])
def predict():
    """Predict from features."""
    if current_model is None:
        return jsonify({'error': 'No model trained yet. Call /train first.'}), 404
    
    try:
        data = request.json
        features = data.get('features')
        
        if len(features) != len(FEATURE_NAMES):
            return jsonify({'error': f'Expected {len(FEATURE_NAMES)} features, got {len(features)}'}), 400
        
        features = np.array(features)
        means = np.array(current_model['means'])
        stds = np.array(current_model['stds'])
        normalized = (features - means) / stds
        
        prediction = current_model['intercept']
        for i, beta in enumerate(current_model['betas']):
            prediction += beta * normalized[i]
        
        return jsonify({
            'prediction': float(prediction),
            'prediction_clamped': float(np.clip(prediction, 0, 100))
        })
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/model/download', methods=['GET'])
def download_model():
    """Download the trained model as JSON."""
    if current_model is None:
        return jsonify({'error': 'No model trained yet'}), 404
    
    temp_file = tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False)
    json.dump(current_model, temp_file, indent=2)
    temp_file.close()
    
    return send_file(temp_file.name, mimetype='application/json', as_attachment=True, download_name='regression_model.json')


if __name__ == '__main__':
    print("Python Regression Server running on http://localhost:5000")
    print("Endpoints:")
    print("  /health - Health check")
    print("  /train - Train model (full dataset)")
    print("  /train_small - Train model (small dataset, 5-10 samples)")
    print("  /model - Get trained model")
    print("  /predict - Predict oxygen from features")
    app.run(host='0.0.0.0', port=5000, debug=True)
