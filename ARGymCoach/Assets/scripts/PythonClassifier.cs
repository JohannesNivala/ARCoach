using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Threading.Tasks; // Required for async
using System.Collections.Generic;

public class PythonClassifier : MonoBehaviour
{
    [Header("References")]
    public GameObject cube;
    public Material[] materials;

    [Header("Settings")]
    [SerializeField] private string serverUrl = "ws://192.168.1.117:8765";

    private WebSocket ws;
    public List<int> previous_classifiers;

    async void Start()
    {
        await ConnectWebSocket();
        previous_classifiers = new List<int>();
    }

    void Update()
    {
        // This is crucial: it processes the events that occurred on background threads 
        // and executes them on Unity's main thread.
        if (ws != null && ws.State == WebSocketState.Open)
        {
            ws.DispatchMessageQueue();
        }
    }

    private async void OnDestroy()
    {
        // Clean up the connection when the scene/game ends
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.Close();
        }
    }

    // --- Connection & Communication Methods ---

    private async Task ConnectWebSocket()
    {
        ws = new WebSocket(serverUrl);

        // 1. Connection Opened Event
        ws.OnOpen += () =>
        {
            Debug.Log("WebSocket Connection Established!");
            //SendStringMessage(messageToSend);
            ws.SendText("Unity has connected!");
        };

        // 2. Message Received Event (handles binary and text)
        ws.OnMessage += (bytes) =>
        {
            // Convert the received byte array back into a string
            string receivedMessage = Encoding.UTF8.GetString(bytes);
            HandleReceivedMessage(receivedMessage);
        };

        // 3. Connection Closed Event
        ws.OnClose += (e) =>
        {
            Debug.Log("WebSocket Closed. Code: " + e);
            //SendStringMessage("Unity has disconnected!");
            ws.SendText("Unity has disconnected!");
        };

        // 4. Error Event
        ws.OnError += (e) =>
        {
            Debug.LogError("WebSocket Error! " + e);
        };

        // Connect asynchronously
        await ws.Connect();
    }

    // Call this method to send a string message
    public void SendStringMessage(string message)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            // Convert the string to a byte array before sending
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            ws.Send(bytes);
        }
        else
        {
            Debug.LogWarning("WebSocket not open. Cannot send message.");
        }
    }

    public void reset_classifiers()
    {
        previous_classifiers = new List<int>();
    }

    private void HandleReceivedMessage(string message)
    {
        // Example: Send another message after getting a response
        if (message == "0")
        {
            previous_classifiers.Add(0);
            cube.GetComponent<Renderer>().material = materials[0];
            Debug.Log("LOWER GOOD");
        }
        else if (message == "1")
        {
            previous_classifiers.Add(1);
            cube.GetComponent<Renderer>().material = materials[1];
            Debug.Log("MIDDLE GOOD");
        }
        else if (message == "2")
        {
            previous_classifiers.Add(2);
            cube.GetComponent<Renderer>().material = materials[2];
            Debug.Log("UPPER GOOD");
        }
        else if (message == "3")
        {
            previous_classifiers.Add(3);
            cube.GetComponent<Renderer>().material = materials[3];
            Debug.Log("LEFT BAD");
        }
        else if (message == "4")
        {
            previous_classifiers.Add(4);
            cube.GetComponent<Renderer>().material = materials[4];
            Debug.Log("RIGHT BAD");
        }
        else if (message == "5")
        {
            previous_classifiers.Add(5);
            cube.GetComponent<Renderer>().material = materials[5];
            Debug.Log("WIDE BAD");
        }
        else
        {
            Debug.Log(message);
        }
    }
}
