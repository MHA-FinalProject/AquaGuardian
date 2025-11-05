using UnityEngine;

/**
 * Regression evaluation metrics
 * Used for model evaluation and cross-validation results
 */
public struct RegressionMetrics
{
    public float RMSE;
    public float MAE;
    public float R2;
    public float AdjustedR2;
    
    public RegressionMetrics(float rmse, float mae, float r2, float adjR2 = float.NaN)
    {
        RMSE = rmse;
        MAE = mae;
        R2 = r2;
        AdjustedR2 = adjR2;
    }
    
    public override string ToString() => $"RMSE: {RMSE:F3}, MAE: {MAE:F3}, R2: {R2:F3}";
    
    public bool IsValid() => !float.IsNaN(RMSE) && !float.IsNaN(MAE) && !float.IsNaN(R2);
}

