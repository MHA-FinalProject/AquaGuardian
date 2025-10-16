using UnityEngine;
using System;

/// <summary>
/// Matrix operations helper for linear regression
/// Pure math - no Unity dependencies
/// </summary>
public static class MatrixHelper
{
    /// <summary>
    /// Transpose matrix: [m x n] to [n x m]
    /// </summary>
    public static float[][] Transpose(float[][] matrix)
    {
        if (matrix == null || matrix.Length == 0) return null;
        
        int rows = matrix.Length;
        int cols = matrix[0].Length;
        
        float[][] result = new float[cols][];
        for (int i = 0; i < cols; i++)
        {
            result[i] = new float[rows];
            for (int j = 0; j < rows; j++)
            {
                result[i][j] = matrix[j][i];
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Multiply two matrices: [m x n] x [n x p] to [m x p]
    /// </summary>
    public static float[][] Multiply(float[][] A, float[][] B)
    {
        if (A == null || B == null) return null;
        
        int m = A.Length;
        int n = A[0].Length;
        int p = B[0].Length;
        
        if (B.Length != n)
        {
            Debug.LogError($"Matrix dimensions don't match: [{m}x{n}] x [{B.Length}x{p}]");
            return null;
        }
        
        float[][] result = new float[m][];
        for (int i = 0; i < m; i++)
        {
            result[i] = new float[p];
            for (int j = 0; j < p; j++)
            {
                float sum = 0f;
                for (int k = 0; k < n; k++)
                {
                    sum += A[i][k] * B[k][j];
                }
                result[i][j] = sum;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Multiply matrix by vector: [m x n] x [n x 1] to [m x 1]
    /// </summary>
    public static float[] MultiplyVector(float[][] matrix, float[] vector)
    {
        if (matrix == null || vector == null) return null;
        
        int m = matrix.Length;
        int n = matrix[0].Length;
        
        if (vector.Length != n)
        {
            Debug.LogError($"Dimensions don't match: [{m}x{n}] x [{vector.Length}]");
            return null;
        }
        
        float[] result = new float[m];
        for (int i = 0; i < m; i++)
        {
            float sum = 0f;
            for (int j = 0; j < n; j++)
            {
                sum += matrix[i][j] * vector[j];
            }
            result[i] = sum;
        }
        
        return result;
    }
    
    /// <summary>
    /// Invert matrix using Gaussian elimination
    /// For small matrices (up to 10x10)
    /// </summary>
    public static float[][] Inverse(float[][] matrix)
    {
        if (matrix == null || matrix.Length == 0) return null;
        
        int n = matrix.Length;
        if (matrix[0].Length != n)
        {
            Debug.LogError("Matrix must be square for inversion");
            return null;
        }
        
        // Create augmented matrix [A | I]
        float[][] augmented = new float[n][];
        for (int i = 0; i < n; i++)
        {
            augmented[i] = new float[2 * n];
            for (int j = 0; j < n; j++)
            {
                augmented[i][j] = matrix[i][j];
                augmented[i][j + n] = (i == j) ? 1f : 0f;
            }
        }
        
        // Gaussian elimination with partial pivoting
        for (int i = 0; i < n; i++)
        {
            // Find pivot
            int maxRow = i;
            float maxVal = Mathf.Abs(augmented[i][i]);
            for (int k = i + 1; k < n; k++)
            {
                float val = Mathf.Abs(augmented[k][i]);
                if (val > maxVal)
                {
                    maxVal = val;
                    maxRow = k;
                }
            }
            
            // Swap rows
            if (maxRow != i)
            {
                float[] temp = augmented[i];
                augmented[i] = augmented[maxRow];
                augmented[maxRow] = temp;
            }
            
            // Check for singular matrix
            if (Mathf.Abs(augmented[i][i]) < 1e-10f)
            {
                Debug.LogError("Matrix is singular (not invertible)");
                return null;
            }
            
            // Scale pivot row
            float pivot = augmented[i][i];
            for (int j = 0; j < 2 * n; j++)
            {
                augmented[i][j] /= pivot;
            }
            
            // Eliminate column
            for (int k = 0; k < n; k++)
            {
                if (k != i)
                {
                    float factor = augmented[k][i];
                    for (int j = 0; j < 2 * n; j++)
                    {
                        augmented[k][j] -= factor * augmented[i][j];
                    }
                }
            }
        }
        
        // Extract inverse from augmented matrix
        float[][] inverse = new float[n][];
        for (int i = 0; i < n; i++)
        {
            inverse[i] = new float[n];
            for (int j = 0; j < n; j++)
            {
                inverse[i][j] = augmented[i][j + n];
            }
        }
        
        return inverse;
    }
    
    /// <summary>
    /// Add intercept column (all 1s) to feature matrix
    /// [m x n] to [m x (n+1)]
    /// </summary>
    public static float[][] AddInterceptColumn(float[][] X)
    {
        if (X == null || X.Length == 0) return null;
        
        int m = X.Length;
        int n = X[0].Length;
        
        float[][] result = new float[m][];
        for (int i = 0; i < m; i++)
        {
            result[i] = new float[n + 1];
            result[i][0] = 1f; // Intercept
            for (int j = 0; j < n; j++)
            {
                result[i][j + 1] = X[i][j];
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Print matrix for debugging
    /// </summary>
    public static void PrintMatrix(float[][] matrix, string name = "Matrix")
    {
        if (matrix == null)
        {
            Debug.Log($"{name}: null");
            return;
        }
        
        string output = $"{name} [{matrix.Length}x{matrix[0].Length}]:\n";
        for (int i = 0; i < matrix.Length; i++)
        {
            output += "[";
            for (int j = 0; j < matrix[i].Length; j++)
            {
                output += $"{matrix[i][j]:F3}";
                if (j < matrix[i].Length - 1) output += ", ";
            }
            output += "]\n";
        }
        Debug.Log(output);
    }
    
    /// <summary>
    /// Print vector for debugging
    /// </summary>
    public static void PrintVector(float[] vector, string name = "Vector")
    {
        if (vector == null)
        {
            Debug.Log($"{name}: null");
            return;
        }
        
        string output = $"{name} [{vector.Length}]: [";
        for (int i = 0; i < vector.Length; i++)
        {
            output += $"{vector[i]:F3}";
            if (i < vector.Length - 1) output += ", ";
        }
        output += "]";
        Debug.Log(output);
    }
}

