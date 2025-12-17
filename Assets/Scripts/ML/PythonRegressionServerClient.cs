using UnityEngine;
using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

/// <summary>
/// Client for Python Regression Server
/// Allows Unity to train models and get predictions from Python server
/// Uses UnityWebRequest (works on all platforms)
/// </summary>
public class PythonRegressionServerClient : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://localhost:5000";
    [SerializeField] private bool autoTrainOnStart = false;
    [SerializeField] private string csvPath = "Assets/Data/Trials/Trial_5_runs_.csv";
    [SerializeField] private string modelType = "ElasticNet";
    [SerializeField] private bool useSmallDatasetMethod = true;  // Use /train_small for small datasets (5-10 samples)

    private bool isServerAvailable = false;

    [System.Serializable]
    private class TrainRequest
    {
        public string csv_path;
        public string model_type;
    }

    [System.Serializable]
    private class TrainSmallRequest
    {
        public string csv_path;
        public string model_type;
        public int n_features;
    }

    [System.Serializable]
    private class PredictRequest
    {
        public float[] features;
    }

    [System.Serializable]
    private class PredictionResponse
    {
#pragma warning disable 0649 // Field is assigned by JSON deserialization
        public float prediction;
        public float prediction_clamped;
#pragma warning restore 0649
    }


    [System.Serializable]
    private class ModelResponse
    {
#pragma warning disable 0649 // Field is assigned by JSON deserialization
        public bool success;
        public ModelData model;
        public ModelInfo info;
#pragma warning restore 0649
    }

    [System.Serializable]
    private class ModelData
    {
#pragma warning disable 0649 // Field is assigned by JSON deserialization
        public string[] feature_names;
        public float intercept;
        public float[] betas;
        public float[] means;
        public float[] stds;
        public string model_type;
        public int n_samples;
        public int n_features;
        public float train_mae;
        public float train_r2;
        public float train_rmse;
        // Additional fields for /train_small endpoint
        public int n_features_selected;
        public string[] selected_features;
        public int[] selected_indices;
        public float cv_mae;
        public float cv_r2;
        public float cv_rmse;
#pragma warning restore 0649
    }

    [System.Serializable]
    private class ModelInfo
    {
#pragma warning disable 0649 // Field is assigned by JSON deserialization
        public string trained_at;
        public string csv_path;
        public string target_column;
#pragma warning restore 0649
    }

    void Start()
    {
        if (autoTrainOnStart)
        {
            StartCoroutine(CheckServerAndTrain());
        }
        else
        {
            StartCoroutine(CheckServerHealth());
        }
    }

   
    public IEnumerator CheckServerHealth()
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"{serverUrl}/health"))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                isServerAvailable = true;
            }
            else
            {
                isServerAvailable = false;
                Debug.LogWarning($"[PythonServer] Health check failed: {request.error}");
            }
        }
    }

    /// <summary>
    /// Train model on Python server
    /// </summary>
    public IEnumerator TrainModel(string csvPath, string modelType = "ElasticNet", Action<bool> onComplete = null)
    {
        string resolvedCsvPath = ResolveCsvPath(csvPath);

        if (!System.IO.File.Exists(resolvedCsvPath))
        {
            Debug.LogError($"[PythonServer] CSV file not found: {resolvedCsvPath}");
            onComplete?.Invoke(false);
            yield break;
        }

        var request = new TrainRequest
        {
            csv_path = resolvedCsvPath,
            model_type = string.IsNullOrWhiteSpace(modelType) ? "ElasticNet" : modelType.Trim()
        };

        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/train", "application/json"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var modelResponse = JsonUtility.FromJson<ModelResponse>(www.downloadHandler.text);
                    //Debug.Log($"[PythonServer] Model trained (samples: {modelResponse.model.n_samples}, MAE: {modelResponse.model.train_mae:F2}%, R^2: {modelResponse.model.train_r2:F3})");
                    
                    // Load model into Unity's regression system
                    LoadModelFromServer(modelResponse.model);
                    onComplete?.Invoke(true);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PythonServer] Failed to parse response: {e.Message}");
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError($"[PythonServer] Training failed: {www.error}\n{www.downloadHandler.text}");
                onComplete?.Invoke(false);
            }
        }
    }

  
    public IEnumerator Predict(float[] features, Action<float> onComplete)
    {
        var request = new PredictRequest { features = features };
        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/predict", "application/json"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var predictionData = JsonUtility.FromJson<PredictionResponse>(www.downloadHandler.text);
                    onComplete?.Invoke(predictionData.prediction_clamped);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PythonServer] Failed to parse prediction: {e.Message}");
                    onComplete?.Invoke(-1f);
                }
            }
            else
            {
                Debug.LogError($"[PythonServer] Prediction failed: {www.error}");
                onComplete?.Invoke(-1f);
            }
        }
    }

    
    public IEnumerator DownloadAndLoadModel(Action<bool> onComplete = null)
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{serverUrl}/model"))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var modelResponse = JsonUtility.FromJson<ModelResponse>(www.downloadHandler.text);
                    LoadModelFromServer(modelResponse.model);
                    onComplete?.Invoke(true);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PythonServer] Failed to parse model: {e.Message}");
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError($"[PythonServer] Download failed: {www.error}");
                onComplete?.Invoke(false);
            }
        }
    }


    private void LoadModelFromServer(ModelData modelData)
    {
        // Convert server model to JSON format that Unity expects
        var unityModel = new PythonRegressionModel.ModelData
        {
            feature_names = modelData.feature_names,
            intercept = modelData.intercept,
            betas = modelData.betas,
            means = modelData.means,
            stds = modelData.stds,
            model_type = modelData.model_type,
            n_samples = modelData.n_samples,
            n_features = modelData.n_features,
            train_mae = modelData.train_mae,
            train_r2 = modelData.train_r2,
            train_rmse = modelData.train_rmse
        };

        // Create temporary JSON file
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "server_model.json");
        string json = JsonUtility.ToJson(unityModel);
        System.IO.File.WriteAllText(tempPath, json);

        // Load into Unity regression system via PythonRegressionHandler
        if (!PythonRegressionHandler.LoadPythonModel(tempPath))
        {
            Debug.LogError("[PythonServer] Failed to load model from server");
        }
    }

    /// <summary>
    /// Train model optimized for small datasets (5-10 samples) using Lasso feature selection
    /// </summary>
    public IEnumerator TrainModelSmall(string csvPath, string modelType = "Ridge", int nFeatures = 3, Action<bool> onComplete = null)
    {
        string resolvedCsvPath = ResolveCsvPath(csvPath);

        if (!System.IO.File.Exists(resolvedCsvPath))
        {
            Debug.LogError($"[PythonServer] CSV file not found: {resolvedCsvPath}");
            onComplete?.Invoke(false);
            yield break;
        }

        var request = new TrainSmallRequest
        {
            csv_path = resolvedCsvPath,
            model_type = string.IsNullOrWhiteSpace(modelType) ? "Ridge" : modelType.Trim(),
            n_features = nFeatures
        };

        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/train_small", "application/json"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var modelResponse = JsonUtility.FromJson<ModelResponse>(www.downloadHandler.text);
                    
                    // Load model into Unity's regression system
                    LoadModelFromServer(modelResponse.model);
                    onComplete?.Invoke(true);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PythonServer] Failed to parse response: {e.Message}");
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError($"[PythonServer] Training failed: {www.error}\n{www.downloadHandler.text}");
                onComplete?.Invoke(false);
            }
        }
    }

    /// <summary>
    /// Train model on server using current trial data and perform analysis
    /// This is the main entry point for TrialRegressionUI
    /// Automatically uses /train_small for small datasets (5-10 samples)
    /// </summary>
    public IEnumerator TrainAndAnalyze(List<TrialDataModels.TrialData> allTrialData, float targetOxygen, Action<TrialDataModels.RegressionResult> onComplete)
    {
        if (!isServerAvailable)
        {
            Debug.LogError("[PythonServer] Server not available for training.");
            onComplete?.Invoke(new TrialDataModels.RegressionResult 
            { 
                summaryText = "ERROR: Python server not available.",
                fullDetailsText = "ERROR: Python server not available.",
                correlations = new Dictionary<string, float>()
            });
            yield break;
        }

        // 1. Save current trial data to a temporary CSV
        string tempCsvPath = System.IO.Path.Combine(Application.temporaryCachePath, "current_patient_trials.csv");
        bool saved = TrialDataService.SaveAllTrialsToCSV(allTrialData, tempCsvPath);

        if (!saved)
        {
            Debug.LogError("[PythonServer] Failed to save trial data for server training.");
            onComplete?.Invoke(new TrialDataModels.RegressionResult 
            { 
                summaryText = "ERROR: Failed to save trial data.",
                fullDetailsText = "ERROR: Failed to save trial data.",
                correlations = new Dictionary<string, float>()
            });
            yield break;
        }

        // 2. Decide which training method to use
        int nSamples = allTrialData.Count;
        bool shouldUseSmallMethod = useSmallDatasetMethod && nSamples >= 5 && nSamples <= 10;
        
        bool trainingSuccess = false;
        
        if (shouldUseSmallMethod)
        {
            int nFeatures = Mathf.Max(2, Mathf.Min(3, nSamples - 2)); // Auto-adjust features
            yield return StartCoroutine(TrainModelSmall(tempCsvPath, "Ridge", nFeatures, (success) =>
            {
                trainingSuccess = success;
            }));
        }
        else
        {
            string cleanModelType = string.IsNullOrWhiteSpace(modelType) ? "ElasticNet" : modelType.Trim();
            yield return StartCoroutine(TrainModel(tempCsvPath, cleanModelType, (success) =>
        {
            trainingSuccess = success;
        }));
        }

        if (!trainingSuccess)
        {
            Debug.LogError("[PythonServer] Server training failed.");
            onComplete?.Invoke(new TrialDataModels.RegressionResult 
            { 
                summaryText = "ERROR: Server training failed.",
                fullDetailsText = "ERROR: Server training failed.",
                correlations = new Dictionary<string, float>()
            });
            yield break;
        }

        // 3. Perform analysis with the newly loaded model
        var result = PythonRegressionHandler.PerformPythonRegressionAnalysis(allTrialData, targetOxygen);
        onComplete?.Invoke(result);
    }

    private IEnumerator CheckServerAndTrain()
    {
        yield return StartCoroutine(CheckServerHealth());
        
        if (isServerAvailable)
        {
            string cleanModelType = string.IsNullOrWhiteSpace(modelType) ? "ElasticNet" : modelType.Trim();
            yield return StartCoroutine(TrainModel(csvPath, cleanModelType));
        }
    }

    // Public API for UI or other scripts
    public void TrainModelFromUI()
    {
        string cleanModelType = string.IsNullOrWhiteSpace(modelType) ? "ElasticNet" : modelType.Trim();
        StartCoroutine(TrainModel(csvPath, cleanModelType));
    }


    public bool IsServerAvailable => isServerAvailable;

    private string ResolveCsvPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // Absolute path already?
        if (System.IO.Path.IsPathRooted(path))
            return path;

        string trimmed = path.TrimStart('/', '\\');

        // Check relative to project root
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        string candidate = System.IO.Path.Combine(projectRoot, trimmed);
        if (System.IO.File.Exists(candidate))
            return candidate;

        // Check inside Assets directory explicitly
        candidate = System.IO.Path.Combine(Application.dataPath, trimmed);
        if (System.IO.File.Exists(candidate))
            return candidate;

        return path;
    }
}