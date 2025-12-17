using UnityEngine;
using System;
using System.Linq;

/**
 * Multiple Linear Regression Model for Oxygen Prediction
 * 
 * Implements Ridge regression with normalization and cross-validation.
 * Model: y = b0 + b1*x1 + b2*x2 + ... + bk*xk
 * Solves: (Phi^T * Phi + lambda * I) * b = Phi^T * y
 */
public class MultipleLinearRegression
{

    private const float MIN_LAMBDA = 1e-9f;


    #region Public API - Model Parameters and Metrics

    /// <summary>Regression coefficients: [b0, b1, b2, ..., bk] where b0 is intercept.</summary>
    public float[] coefficients;

    /// <summary>R^2 (R SQUARE) - Proportion of variance explained (0-1, higher is better).</summary>
    public float rSquared;

    /// <summary>Adjusted R^2 - R^2 adjusted for model complexity (penalizes extra features).</summary>
    public float adjustedRSquared;

    /// <summary>Mean Squared Error - Average of squared prediction errors.</summary>
    public float meanSquaredError;

    /// <summary>Root Mean Squared Error - Square root of MSE, same units as target variable.</summary>
    public float rootMeanSquaredError;

    #endregion

    #region Feature Information

    /// <summary>Number of input features (excluding intercept).</summary>
    public int numFeatures;

    /// <summary>Number of training samples.</summary>
    public int numSamples;

    /// <summary>Names of features for interpretability (optional).</summary>
    public string[] featureNames;

    #endregion

    #region Normalization

    /// <summary>Feature normalizer for standardization (mean=0, std=1). Public for optimizer access.</summary>
    public FeatureNormalizer normalizer;

    /// <summary>Whether to use feature normalization during training and prediction.</summary>
    private bool useNormalization;

    /// <summary>Mean values used for normalization (one per feature).</summary>
    public float[] Means => normalizer?.means;

    /// <summary>Standard deviation values used for normalization (one per feature).</summary>
    public float[] Stds => normalizer?.stdDevs;

    #endregion

    #region Regularization

    /// <summary>Ridge regularization strength (lambda). Higher = stronger regularization. Recommended: 0.1-1.0 for small datasets. Intercept is not penalized.</summary>
    public float ridgeLambda = 0.5f;

    #endregion

    /// <summary>Constructor - Initializes a new Multiple Linear Regression model.</summary>
    /// <param name="normalize">Whether to normalize features (default: true). Improves numerical stability.</param>
    public MultipleLinearRegression(bool normalize = true)
    {
        useNormalization = normalize;
        if (normalize)
        {
            normalizer = new FeatureNormalizer();
        }
    }


    #region Model Training

    /// <summary>Trains the regression model on the provided data using Ridge regression with Cholesky decomposition.</summary>
    /// <param name="X">Feature matrix: [numSamples][numFeatures]</param>
    /// <param name="Y">Target vector: [numSamples]</param>
    /// <param name="featureNames">Optional feature names for interpretability.</param>
    public void Fit(float[][] X, float[] Y, string[] featureNames = null)
    {
        if (!RegressionMath.ValidateInputs(X, Y, out string error))
        {
            Debug.LogError($"[MultipleLinearRegression] Invalid input: {error}");
            return;
        }

        RegressionMath.CleanMatrix(X);
        RegressionMath.CleanVector(Y);

        numSamples = X.Length;
        numFeatures = X[0].Length;
        this.featureNames = featureNames;

        // Normalize features if enabled
        float[][] Xprocessed = X;
        if (useNormalization)
        {
            normalizer.Fit(X);
            Xprocessed = normalizer.Transform(X);
        }

        // Build design matrix Phi with intercept: [1, x1, x2, ..., xk]
        int m = numSamples;
        int d = numFeatures + 1;
        double[,] Phi = new double[m, d];

        for (int i = 0; i < m; i++)
        {
            Phi[i, 0] = 1.0;  // Intercept
            for (int j = 0; j < numFeatures; j++)
            {
                Phi[i, j + 1] = Xprocessed[i][j];
            }
        }

        // Build normal equations: A = Phi^T * Phi, b = Phi^T * y
        double[,] A = new double[d, d];
        double[] b = new double[d];

        for (int i = 0; i < m; i++)
        {
            double yi = Y[i];
            for (int a = 0; a < d; a++)
            {
                double va = Phi[i, a];
                b[a] += va * yi;
                for (int c = 0; c < d; c++)
                {
                    A[a, c] += va * Phi[i, c];
                }
            }
        }

        // Add Ridge regularization (skip intercept)
        double lambdaEff = Math.Max(MIN_LAMBDA, ridgeLambda);
        for (int a = 1; a < d; a++)
        {
            A[a, a] += lambdaEff;
        }

        // Solve using Cholesky decomposition
        double[] coefficientsDouble = RegressionMath.SolveSPDByCholesky(A, b);

        if (coefficientsDouble == null)
        {
            Debug.LogError("Cholesky decomposition failed (matrix not positive definite)");
            return;
        }

        coefficients = coefficientsDouble.Select(x => (float)x).ToArray();
        CalculateMetrics(X, Y);
    }

    #endregion

    #region Prediction

    /// <summary>Predicts target value for a single feature vector. Formula: y = b0 + b1*x1 + ... + bk*xk</summary>
    /// <param name="features">Feature vector [x1, x2, ..., xk]. Must match numFeatures.</param>
    /// <returns>Predicted target value. Returns 0 if model not fitted or on error.</returns>
    public float Predict(float[] features)
    {
        if (coefficients == null)
        {
            Debug.LogError("Model not fitted! Call Fit() first.");
            return 0f;
        }

        if (features == null || features.Length != numFeatures)
        {
            Debug.LogError($"Feature count mismatch: expected {numFeatures}, got {features?.Length ?? 0}");
            return 0f;
        }

        RegressionMath.CleanVector(features);
        float[] x = useNormalization ? normalizer.TransformSample(features) : features;

        double yhat = coefficients[0];  // Intercept
        for (int j = 0; j < x.Length; j++)
        {
            yhat += coefficients[j + 1] * x[j];
        }

        if (double.IsNaN(yhat) || double.IsInfinity(yhat))
        {
            Debug.LogWarning("Prediction resulted in NaN/Inf, returning 0");
            return 0f;
        }

        return (float)yhat;
    }

    /// <summary>Predicts target values for multiple feature vectors (batch prediction).</summary>
    /// <param name="X">Feature matrix: [numSamples][numFeatures]</param>
    /// <returns>Array of predicted values. Returns null if input is null or empty.</returns>
    public float[] PredictBatch(float[][] X)
    {
        if (X == null || X.Length == 0) return null;

        var predictions = new float[X.Length];
        for (int i = 0; i < X.Length; i++)
        {
            predictions[i] = Predict(X[i]);
        }

        return predictions;
    }

    #endregion

    #region Cross-Validation

    /// <summary>Performs K-fold cross-validation: splits data into k folds, trains on k-1, tests on 1, repeats k times, averages metrics.</summary>
    /// <param name="X">Feature matrix: [numSamples][numFeatures]</param>
    /// <param name="Y">Target vector: [numSamples]</param>
    /// <param name="kFolds">Number of folds (default: 5). Range: 2 to min(10, numSamples).</param>
    /// <param name="seed">Optional random seed for reproducible shuffling.</param>
    /// <returns>Average RMSE, MAE, and R^2 across all folds. Returns NaN if validation fails.</returns>
    public RegressionMetrics KFoldCV(float[][] X, float[] Y, int kFolds = 5, int? seed = null)
    {
        if (!RegressionMath.ValidateInputs(X, Y, out string error))
        {
            Debug.LogError($"[MultipleLinearRegression] K-Fold CV failed: {error}");
            return new RegressionMetrics(float.NaN, float.NaN, float.NaN);
        }

        // Clean NaN/Inf values before CV to prevent numerical errors
        RegressionMath.CleanMatrix(X);
        RegressionMath.CleanVector(Y);

        int n = X.Length;  // Number of samples
        // Clamp k to valid range: at least 2 folds, at most 10 folds or n samples
        int k = Mathf.Clamp(kFolds, 2, Mathf.Min(10, n));

        // Shuffle indices randomly to ensure folds are representative
        // This prevents bias from data ordering (e.g., if data is sorted by target value)
        var idx = Enumerable.Range(0, n).ToArray();
        var rand = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

        // Fisher-Yates shuffle algorithm
        for (int i = n - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);  // Swap
        }

        // Build fold sizes that cover ALL n samples
        // Distribute remainder samples evenly across first few folds
        // Example: n=23, k=5 -> folds: [5, 5, 5, 4, 4] (total = 23)
        int baseSize = n / k;  // Base samples per fold
        int remainder = n % k;  // Extra samples to distribute

        int start = 0;
        var folds = new (int start, int len)[k];
        for (int f = 0; f < k; f++)
        {
            // First 'remainder' folds get one extra sample
            int len = baseSize + (f < remainder ? 1 : 0);
            folds[f] = (start, len);
            start += len;
        }

        // Accumulate metrics across all folds
        double rmseSum = 0;
        double maeSum = 0;
        double r2Sum = 0;
        int foldsCounted = 0;

        // Process each fold
        for (int f = 0; f < k; f++)
        {
            var (s, len) = folds[f];
            if (len <= 0) continue;  // Skip empty folds

            // Split into test and training indices
            var testIdx = idx.Skip(s).Take(len).ToArray();  // Current fold = test set
            var trainIdx = idx.Where(ii => ii < s || ii >= s + len).ToArray();  // Rest = training set

            int foldNumFeatures = (X != null && X.Length > 0) ? X[0].Length : numFeatures;

            // Validate: need enough training samples for regression
            // At least (numFeatures + 1) samples needed to fit the model
            // (one sample per feature + one for the intercept)
            if (trainIdx.Length < Math.Max(2, foldNumFeatures + 1))
            {
                Debug.LogWarning($"Fold {f + 1}: Not enough training samples ({trainIdx.Length}), skipping");
                continue;
            }

            var Xtr = RegressionMath.SubsetX(X, trainIdx);
            var Ytr = RegressionMath.SubsetY(Y, trainIdx);
            var Xte = RegressionMath.SubsetX(X, testIdx);
            var Yte = RegressionMath.SubsetY(Y, testIdx);

            // Train model on this fold's training set
            // Use same normalization and regularization settings as main model
            var model = new MultipleLinearRegression(normalize: useNormalization)
            {
                ridgeLambda = this.ridgeLambda
            };
            model.Fit(Xtr, Ytr, featureNames);

            // Predict on test fold
            var preds = model.PredictBatch(Xte);

            // Calculate metrics for this fold
            var metrics = RegressionMath.ComputeMetrics(Yte, preds);

            // Validate metrics: skip folds with invalid results
            if (float.IsNaN(metrics.RMSE) || float.IsNaN(metrics.R2) || float.IsInfinity(metrics.RMSE) || float.IsInfinity(metrics.R2))
            {
                Debug.LogWarning($"Fold {f + 1}: NaN/Inf metrics detected, skipping");
                continue;
            }

            // Accumulate metrics
            rmseSum += metrics.RMSE;
            maeSum += metrics.MAE;
            r2Sum += metrics.R2;
            foldsCounted++;
        }

        if (foldsCounted == 0)
        {
            Debug.LogWarning("K-Fold CV: No valid folds");
            return new RegressionMetrics(float.NaN, float.NaN, float.NaN);
        }

        float avgRMSE = (float)(rmseSum / foldsCounted);
        float avgMAE = (float)(maeSum / foldsCounted);
        float avgR2 = (float)(r2Sum / foldsCounted);

        return new RegressionMetrics(avgRMSE, avgMAE, avgR2);
    }

    #endregion

    #region Feature Importance

    /// <summary>Calculates feature importance based on absolute coefficient values. Intercept excluded. Returns sorted array (descending).</summary>
    /// <returns>Array of (feature name, importance) tuples. Returns null if model not fitted.</returns>
    public (string feature, float importance)[] GetFeatureImportance()
    {
        // Validate model is fitted
        if (coefficients == null || coefficients.Length < 2) return null;

        var importance = new (string, float)[numFeatures];

        // Calculate importance for each feature
        for (int i = 0; i < numFeatures; i++)
        {
            // Use feature name if available, otherwise use generic name
            string name = (featureNames != null && i < featureNames.Length)
                ? featureNames[i]
                : $"Feature_{i}";

            // Importance = absolute value of coefficient
            // Skip intercept at index 0, feature coefficients start at index 1
            float value = Mathf.Abs(coefficients[i + 1]);
            importance[i] = (name, value);
        }

        return importance.OrderByDescending(x => x.Item2).ToArray();
    }

    #endregion

    #region Metrics Calculation

    /// <summary>Calculates performance metrics (R^2, Adjusted R^2, MSE, RMSE) on training data.</summary>
    /// <param name="X">Feature matrix used for training</param>
    /// <param name="Y">Target vector used for training</param>
    private void CalculateMetrics(float[][] X, float[] Y)
    {
        var predictions = PredictBatch(X);
        var metrics = RegressionMath.ComputeMetrics(Y, predictions);

        rSquared = metrics.R2;

        int n = Y.Length;
        int k = numFeatures;

        adjustedRSquared = (n - k - 1 > 0)
            ? (1f - ((1f - rSquared) * (n - 1) / (n - k - 1)))
            : float.NaN;

        meanSquaredError = metrics.RMSE * metrics.RMSE;
        rootMeanSquaredError = metrics.RMSE;
    }

    #endregion
}
