from flask import Flask, request, jsonify, send_file
from flask_cors import CORS
import pandas as pd
import numpy as np
import json
import os
import tempfile
from sklearn.linear_model import Ridge, ElasticNet, HuberRegressor
from sklearn.cross_decomposition import PLSRegression
from sklearn.model_selection import cross_val_score
from sklearn.metrics import mean_absolute_error, r2_score, mean_squared_error
import warnings
warnings.filterwarnings('ignore')

app = Flask(__name__)
CORS(app)  

# Feature names
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
    print(f"Loaded CSV: {len(df)} rows, {len(df.columns)} columns")
    
    # Extract features and handle missing columns
    feature_data = []
    for feature_name in FEATURE_NAMES:
        if feature_name in df.columns:
            feature_data.append(df[feature_name].values)
        else:
            if feature_name == "factorForce":
                if "IsAmadeoMode" in df.columns:
                    feature_data.append((df["IsAmadeoMode"] > 0.5).astype(float) * df.get("factorForce", 0))
                else:
                    feature_data.append(np.zeros(len(df)))
            elif feature_name == "EffectiveDrainRate":
                if "RemoveHealthEveryLifeTime" in df.columns and "lifeTime" in df.columns:
                    edr = df["RemoveHealthEveryLifeTime"] / np.maximum(df["lifeTime"], 0.1)
                    feature_data.append(edr.values)
                else:
                    feature_data.append(np.zeros(len(df)))
            else:
                feature_data.append(np.zeros(len(df)))
    
    X = np.column_stack(feature_data)
    
    # Find target column (last one with data)
    target_col = None
    for possible_name in ['FinalOxygenRemaining', 'finalOxygenRemaining', 'FinalOxygen', 'Oxygen', 'o2_result']:
        if possible_name in df.columns:
            target_col = possible_name
            break
    
    if target_col is None:
        o2_cols = [col for col in df.columns if col.lower().startswith('o2')]
        if o2_cols:
            for col in reversed(o2_cols):
                values = df[col].values
                if not np.isnan(values).all():
                    target_col = col
                    break
            if target_col is None:
                target_col = o2_cols[-1]
    
    if target_col is None:
        raise ValueError("Could not find oxygen target column")
    
    print(f"Using target column: {target_col}")
    y = df[target_col].values
    
    # Remove rows with NaN in X or y
    # First, check for NaN in any feature (X)
    X_nan_mask = np.isnan(X).any(axis=1)
    # Then, check for NaN in target (y)
    y_nan_mask = np.isnan(y)
    # Combine: keep only rows with no NaN in either X or y
    valid_mask = ~(X_nan_mask | y_nan_mask)
    
    if not valid_mask.all():
        n_removed = (~valid_mask).sum()
        print(f"⚠ Warning: Removed {n_removed} rows with NaN values")
        y = y[valid_mask]
        X = X[valid_mask]
        print(f"✓ Valid samples remaining: {len(y)}")
    else:
        print(f"✓ All {len(y)} samples are valid (no NaN values)")
    
    if len(y) == 0:
        raise ValueError("No valid samples found after removing NaN values")
    
    return X, y, target_col

def train_model(X, y, model_type='ElasticNet'):
    """Train model."""
    # Safety check: ensure no NaN values
    if np.isnan(X).any():
        raise ValueError("Input X contains NaN after cleaning. This should not happen!")
    if np.isnan(y).any():
        raise ValueError("Input y contains NaN after cleaning. This should not happen!")
    
    # Calculate statistics
    means = np.mean(X, axis=0)
    stds = np.std(X, axis=0, ddof=1)
    stds = np.where(stds < 1e-9, 1.0, stds)
    
    # Normalize
    X_normalized = (X - means) / stds
    
    # Train model
    if model_type == 'Ridge':
        model = Ridge(alpha=0.5)
    elif model_type == 'ElasticNet':
        model = ElasticNet(alpha=0.5, l1_ratio=0.3, max_iter=2000)
    elif model_type == 'Huber':
        model = HuberRegressor(epsilon=1.35, alpha=0.5, max_iter=200)
    elif model_type == 'PLS':
        n_components = min(3, X.shape[1], X.shape[0] - 1)
        model = PLSRegression(n_components=n_components)
    else:
        raise ValueError(f"Unknown model type: {model_type}")
    
    model.fit(X_normalized, y)
    
    # Calculate metrics
    y_pred = model.predict(X_normalized)
    train_mae = mean_absolute_error(y, y_pred)
    train_r2 = r2_score(y, y_pred)
    train_rmse = np.sqrt(mean_squared_error(y, y_pred))
    
    # Extract coefficients
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
        model_type = data.get('model_type', 'ElasticNet')
        
        if not csv_path or not os.path.exists(csv_path):
            return jsonify({'error': f'CSV file not found: {csv_path}'}), 400
        
        # Load and train
        X, y, target_col = load_trial_data_from_csv(csv_path)
        model_data = train_model(X, y, model_type)
        
        # Add metadata
        model_data['feature_names'] = FEATURE_NAMES
        model_data['model_type'] = model_type
        model_data['target_column'] = target_col
        
        if model_type == 'Ridge':
            model_data['alpha'] = 0.5
        elif model_type == 'ElasticNet':
            model_data['alpha'] = 0.5
            model_data['l1_ratio'] = 0.3
        
        # Store in memory
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
        
        # Normalize
        features = np.array(features)
        means = np.array(current_model['means'])
        stds = np.array(current_model['stds'])
        normalized = (features - means) / stds
        
        # Predict
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
    
    # Create temporary JSON file
    temp_file = tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False)
    json.dump(current_model, temp_file, indent=2)
    temp_file.close()
    
    return send_file(temp_file.name, mimetype='application/json', as_attachment=True, download_name='regression_model.json')

if __name__ == '__main__':
    print("Starting Python Regression Server...")
    print("   Endpoints:")
    print("   - POST /train - Train model from CSV")
    print("   - GET  /model - Get trained model")
    print("   - POST /predict - Predict oxygen from features")
    print("   - GET  /model/download - Download model as JSON")
    print("   - GET  /health - Health check")
    print("\n   Example:")
    print('   curl -X POST http://localhost:5000/train -H "Content-Type: application/json" -d \'{"csv_path": "Assets/Data/Trials/Trial_5_runs_.csv", "model_type": "ElasticNet"}\'')
    print("\n   Server running on http://localhost:5000")
    app.run(host='0.0.0.0', port=5000, debug=True)




