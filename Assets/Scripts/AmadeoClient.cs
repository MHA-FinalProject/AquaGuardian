using UnityEngine;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Enum to determine the input type: either emulation mode or actual Amadeo device.
public enum InputType
{
    EmulationMode,  // Reads the forces from the file Assets/AmadeoRecords/force_data.txt. The five right numbers in each row represent the forces in the five fingers (of the right hand).
    Amadeo,         // Reads the forces from Amadeo (if it is connected). If there are no forces from Amadeo, then reads from keyboard (Space = Down).
}

public class AmadeoClient : MonoBehaviour
{
    /* Singleton pattern to ensure only one instance of AmadeoClient exists.
    // public static AmadeoClient Instance { get; private set; }*/

    // Input type to determine if we're in EmulationMode or using the actual Amadeo device.
    [SerializeField] InputType inputType = InputType.Amadeo;

    // The port number used for the Amadeo connection, typically 4444.
    [SerializeField, Tooltip("Port should be 4444 for Amadeo connection"), Range(1024, 49151)]
    private int _port = 4444;

    // Number of data samples to be used for zeroing the forces.
    [SerializeField] private int _zeroFBuffer = 10;

    private CancellationTokenSource _cancellationTokenSource;  // See C# documentation to learn about CancellationTokenSource: https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource?view=net-8.0
    private bool _isReceiving = false;                         // Flag to check if data reception is active.
    private UdpClient _udpClient;
    private const string EmulationDataFile = "Assets/AmadeoRecords/force_data.txt"; // Path to file where each row represents a sample of 10 forces (one per finger).

    private IPEndPoint _remoteEndPoint; // End point for the UDP connection.

    private float[] _forces = new float[5]; // Array to store the force values for five fingers.
    private readonly float[] _zeroForces = new float[5]; // Array to store zeroed force values for five fingers.
    private bool _isLeftHand = false; // Flag to check if we're handling data for the left hand.

    // Event triggered whenever force values are updated.
    public event Action<float[]> OnForcesUpdated;

    public GameObject Panel;

    /* Singleton implementation; commented out because it did not work just before the experiment.
    // private void Awake()
    {
        Debug.Log("AmadeoClient Awake called");
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }*/


    private void Start()
    {
        try
        {
            // Set up the UDP client for receiving data.
            _udpClient = new UdpClient(_port);
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize UdpClient: {ex.Message}");
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        Debug.Log("AmadeoClient started");
        // NOTE: Barak called StartReceiveData() here, but we call it from PanelOpenUp.ClosePanel().
    }

    // Method to start zeroing the forces (calibration step).
    public void StartZeroF()
    {
        SetZeroF(_cancellationTokenSource.Token);
        Debug.Log("StartReceiveData :: Starting zeroing forces.");
        return;
    }

    // Method to start receiving data from either Amadeo device or emulation file.
    // We call it after the PanelOpenUp is closed.
    public void StartReceiveData()
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }
        _isReceiving = true;
        if (inputType == InputType.EmulationMode)
        {
            Debug.Log("StartReceiveData :: Emulation mode is true. Starting emulation data.");
            HandleIncomingDataEmu(_cancellationTokenSource.Token);
        }
        else
        {
            Debug.Log("StartReceiveData :: Amadeo mode is true. Starting Amadeo data.");
            ReceiveDataAmadeo(_cancellationTokenSource.Token);
        }
    }

    // Method to stop receiving data.
    public void StopReceiveData()
    {
        _isReceiving = false;
    }

    // Asynchronous method to receive data from the Amadeo device.
    private async void ReceiveDataAmadeo(CancellationToken cancellationToken)
    {
        while (_isReceiving && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Receiving data from UDP and converting it to a string.
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string receivedData = Encoding.ASCII.GetString(result.Buffer);
                // Parsing and processing the received data.
                HandleReceivedData(ParseDataFromAmadeo(receivedData));
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Data reception was canceled.");
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in ReceiveData: {ex.Message}");
                break;
            }
        }
    }

    // Asynchronous method to handle incoming data from the emulation file.
    private async void HandleIncomingDataEmu(CancellationToken cancellationToken)
    {
        try
        {
            // Reading all lines from the emulation data file.
            string[] lines = await File.ReadAllLinesAsync(EmulationDataFile, cancellationToken);

            int index = 0;

            while (_isReceiving)
            {
                string line = lines[index];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    HandleReceivedData(ParseDataFromAmadeo(line));
                }

                index = (index + 1) % lines.Length;
                await Task.Delay(10, cancellationToken); // Delay between data readings. Emulates the delay between samples.
            }

            Debug.Log("HandleIncomingDataEmu: Stopped receiving data.");
        }
        catch (TaskCanceledException e)
        {
            Debug.Log($"Task was canceled: {e.Message}");
            throw;
        }
    }

    // Method to process the received data string.
    private void HandleReceivedData(string data)
    {
        string[] strForces = data.Split('\t');
        if (strForces.Length != 11)
        {
            Debug.Log("Received data does not contain exactly 11 values. Ignoring...");
            return;
        }

        // Parsing the last 5 force values and applying the zeroing forces.
        strForces.Select(str =>
                float.Parse(str.Replace(",", "."), CultureInfo.InvariantCulture))
            .Skip(strForces.Length - 5)
            .ToArray()
            .CopyTo(_forces, 0);

        _forces = _forces.Select((force, i) => force - _zeroForces[i]).ToArray();

        // If the hand is not the left hand, reverse the forces array.
        if (!_isLeftHand)
        {
            _forces = _forces.Reverse().ToArray();
        }

        Debug.Log("Forces after processing: " + string.Join(", ", _forces));
        OnForcesUpdated?.Invoke(_forces); // Trigger the event to update forces.
    }

    // Method to parse and clean the data received from the Amadeo device.
    private static string ParseDataFromAmadeo(string data)
    {
        return data.Replace("<Amadeo>", "").Replace("</Amadeo>", "");
    }

    // Called when the object is destroyed, to stop receiving data and clean up resources.
    private void OnDestroy()
    {
        Debug.Log("Called OnDestroy() in AmadeoClient...");
        StopClientConnection();
    }

    // Called when the application quits, to stop receiving data and clean up resources.
    private void OnApplicationQuit()
    {
        StopClientConnection();
    }

    // Method to stop the client connection and clean up resources.
    private void StopClientConnection()
    {
        _isReceiving = false;
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient.Dispose();
            _udpClient = null;
        }

        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    // Asynchronous method to calculate and set the zeroing forces.
    private async void SetZeroF(CancellationToken cancellationToken)
    {
        var index = 0;
        int numOfLinesToRead = _zeroFBuffer;
        string[] lines = new string[numOfLinesToRead];
        try
        {
            if (inputType is InputType.EmulationMode)
            {
                using (StreamReader reader = new StreamReader(EmulationDataFile))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null && index < numOfLinesToRead)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lines[index] = line;
                            index++;
                        }
                    }
                }

                if (lines.Length == 0)
                {
                    Debug.LogError("No data found in the emulation file.");
                    return;
                }
            }
            else
            {
                int i = 0;
                while (i < numOfLinesToRead && !cancellationToken.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    string receivedData = Encoding.ASCII.GetString(result.Buffer);
                    Debug.Log(receivedData);

                    HandleReceivedData(ParseDataFromAmadeo(receivedData));

                    lines[i] = receivedData;
                    i++;
                }

                if (i < numOfLinesToRead)
                {
                    Debug.LogError("Not enough data received from Amadeo device.");
                }
            }

            // Calculate the average zeroing forces based on the received data.
            string[] parsedData = lines.Select(ParseDataFromAmadeo).ToArray();
            CalculateZeroingForces(parsedData);

        }
        catch (OperationCanceledException)
        {
            Debug.Log("Data reception was canceled.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in ReceiveData: {ex.Message}");
        }
    }

    // Method to calculate the zeroing forces by averaging the received data.
    private void CalculateZeroingForces(string[] lines)
    {
        float[] sums = new float[5];
        int count = 0;

        // Parsing and processing the data to calculate the sums of force values.
        String[][] allLines = lines
            .Select(line => line.Replace(",", ".").Split('\t'))
            .ToArray();
        string[][] relevantForces = allLines.Select(line => line.Skip(line.Length - 5).ToArray()).ToArray();

        foreach (var line in relevantForces)
        {
            if (line.Length == 5)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (!float.TryParse(line[i], NumberStyles.Float, CultureInfo.InvariantCulture,
                            out float value)) continue;
                    sums[i] += value;
                }

                count++;
            }
        }

        // Calculating the average forces for zeroing.
        for (int i = 0; i < sums.Length; i++)
        {
            _zeroForces[i] = sums[i] / count;
        }
    }
}
