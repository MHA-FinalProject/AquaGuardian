using UnityEngine;
using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
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

    private bool isServerAvailable = false;

    [System.Serializable]
    private class TrainRequest
    {
        public string csv_path;
        public string model_type;
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
                Debug.Log($"[PythonServer] Health check: {request.downloadHandler.text}");
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
            model_type = modelType
        };

        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm($"{serverUrl}/train", "application/json"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[PythonServer] Training model: {resolvedCsvPath}, type: {modelType}");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var modelResponse = JsonUtility.FromJson<ModelResponse>(www.downloadHandler.text);
                    Debug.Log($"[PythonServer] Model trained successfully!");
                    Debug.Log($"   Samples: {modelResponse.model.n_samples}");
                    Debug.Log($"   Train MAE: {modelResponse.model.train_mae:F2}%");
                    Debug.Log($"   Train R^2: {modelResponse.model.train_r2:F3}");
                    
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

        // Load into Unity regression system
        if (TrialRegressionAlgorithm.LoadPythonModel(tempPath))
        {
            Debug.Log("Model loaded from Python server into Unity");
        }
        else
        {
            Debug.LogError("Failed to load model from server");
        }
    }

    private IEnumerator CheckServerAndTrain()
    {
        yield return StartCoroutine(CheckServerHealth());
        
        if (isServerAvailable)
        {
            yield return StartCoroutine(TrainModel(csvPath, modelType, (success) =>
            {
                if (success)
                {
                    Debug.Log("Model trained and loaded from Python server");
                }
            }));
        }
    }

    // Public API for UI or other scripts
    public void TrainModelFromUI()
    {
        StartCoroutine(TrainModel(csvPath, modelType));
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