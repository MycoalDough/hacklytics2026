using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class Connection : MonoBehaviour
{
    [Header("Socket")]
    private const string host = "127.0.0.1";
    private int port = 12345;
    TcpClient client;
    NetworkStream stream;
    private Thread receiveThread;
    private bool isRunning = true;
    static Connection instance;
    public UnityMainThreadDispatcher umtd;

    // Lock to prevent multiple agents writing to the stream simultaneously
    private readonly object streamLock = new object();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            ConnectToServer();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ConnectToServer()
    {
        try
        {
            client = new TcpClient(host, port);
            stream = client.GetStream();
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log("Connected to Python server.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection Exception: {e.Message}");
        }
    }

    /// <summary>
    /// Called by each agent. agentAndInfo format: "RED:obs1|obs2|obs3"
    /// Sends "get_state_agent:RED:obs1|obs2|obs3" to Python.
    /// </summary>
    public static void SendData(string agentAndInfo)
    {
        if (instance == null || instance.stream == null)
        {
            Debug.LogError("SendData: No active connection.");
            return;
        }

        try
        {
            string message = $"get_state_agent:{agentAndInfo}";
            byte[] dataToSend = Encoding.UTF8.GetBytes(message);

            // Lock so multiple agents don't write at the same time
            lock (instance.streamLock)
            {
                instance.stream.Write(dataToSend, 0, dataToSend.Length);
            }

            Debug.Log($"Sent to Python: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"SendData Exception: {e.Message}");
        }
    }

    void ReceiveData()
    {
        Debug.Log("Receive thread started.");
        byte[] data = new byte[4096];

        while (isRunning)
        {
            try
            {
                int bytesRead = stream.Read(data, 0, data.Length);
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(data, 0, bytesRead).Trim();
                    Debug.Log($"Received from Python: {message}");

                    // Expected format: "play_state:ACTION1|ACTION2|ACTION3"
                    if (message.StartsWith("play_state:"))
                    {
                        string[] parts = message.Split(':');
                        string actions = parts.Length > 1 ? parts[1] : "";

                        umtd.Enqueue(() => {
                            StartCoroutine(PlayStepCoroutine(actions));
                        });
                    }
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogError($"ReceiveData Exception: {e.Message}");
            }
        }
    }

    private IEnumerator PlayStepCoroutine(string actions)
    {
        // actions = "ACTION1|ACTION2|ACTION3"
        string[] actionList = actions.Split('|');
        // TODO: apply each action to the corresponding agent
        foreach (string action in actionList)
        {
            Debug.Log($"Applying action: {action}");
        }
        yield return null;
    }

    private void OnApplicationQuit()
    {
        isRunning = false;
        receiveThread?.Join(500);
        stream?.Close();
        client?.Close();
    }
}
