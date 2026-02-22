// Connection.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class Connection : MonoBehaviour
{
    [Header("Socket")]
    private const string host = "127.0.0.1";
    private const int port = 12345;
    private const char Delimiter = '\n';

    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private volatile bool isRunning = true;

    private static Connection instance;

    [Header("Dispatcher (optional)")]
    public UnityMainThreadDispatcher umtd;

    private readonly object streamLock = new object();
    private readonly object queueLock = new object();

    private readonly List<JObject> eventQueue = new List<JObject>();
    private bool hasPendingEvents = false;

    private Dictionary<string, Amongi> agentRegistry = new Dictionary<string, Amongi>();

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (umtd == null)
            umtd = FindFirstObjectByType<UnityMainThreadDispatcher>();

        RebuildRegistry();
        ConnectToServer();
    }

    public void RebuildRegistry()
    {
        agentRegistry.Clear();
        foreach (Amongi a in FindObjectsByType<Amongi>(FindObjectsSortMode.None))
        {
            if (a != null && !string.IsNullOrEmpty(a.agentId))
                agentRegistry[a.agentId] = a;
        }
    }

    public void ConnectToServer()
    {
        try
        {
            client = new TcpClient(host, port);
            client.NoDelay = true;

            stream = client.GetStream();

            receiveThread = new Thread(ReceiveData) { IsBackground = true };
            receiveThread.Start();

            Debug.Log("[Connection] Connected to Python server.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Connection] ConnectToServer: {e}");
        }
    }

    // -------------------- SEND --------------------

    public static void QueueEvent(string eventJson)
    {
        if (instance == null) return;

        try
        {
            JObject parsed = JObject.Parse(eventJson);
            lock (instance.queueLock)
            {
                instance.eventQueue.Add(parsed);
                instance.hasPendingEvents = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Connection] QueueEvent parse error: {e}");
        }
    }

    public static void FlushEvents()
    {
        if (instance == null || instance.stream == null) return;

        List<JObject> toSend;
        lock (instance.queueLock)
        {
            if (instance.eventQueue.Count == 0) return;
            toSend = new List<JObject>(instance.eventQueue);
            instance.eventQueue.Clear();
            instance.hasPendingEvents = false;
        }

        var payload = new JObject
        {
            ["type"] = "events",
            ["events"] = new JArray(toSend)
        };

        instance.SendLine(payload.ToString(Newtonsoft.Json.Formatting.None));
    }

    private void LateUpdate()
    {
        if (hasPendingEvents) FlushEvents();
    }

    private void SendLine(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json + Delimiter);
        try
        {
            lock (streamLock)
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Connection] SendLine write error: {e}");
        }
    }

    // -------------------- RECEIVE --------------------

    private void ReceiveData()
    {
        Debug.Log("[Connection] Receive thread started.");

        byte[] buffer = new byte[500000];
        var sb = new StringBuilder();

        while (isRunning)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                // 0 bytes means the remote closed the connection.
                if (bytesRead <= 0) break;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                while (true)
                {
                    string all = sb.ToString();
                    int newlineIndex = all.IndexOf(Delimiter);
                    if (newlineIndex < 0) break;

                    string line = all.Substring(0, newlineIndex).Trim();
                    sb.Remove(0, newlineIndex + 1);

                    if (string.IsNullOrEmpty(line)) continue;

                    // Python sends a raw JArray: [{agent,type,details,...}, ...]
                    JArray actionsArray = JArray.Parse(line);

                    if (umtd != null)
                        umtd.Enqueue(() => StartCoroutine(ApplyActionsCoroutine(actionsArray)));
                    else
                        Debug.LogError("[Connection] UnityMainThreadDispatcher not found; cannot apply actions on main thread.");
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogError($"[Connection] ReceiveData: {e}");
            }
        }

        Debug.Log("[Connection] Receive thread exiting.");
    }

    private IEnumerator ApplyActionsCoroutine(JArray actionsArray)
    {
        if (actionsArray == null) yield break;

        foreach (JObject entry in actionsArray)
        {
            string agentId = entry["agent"]?.ToString();
            string type = entry["type"]?.ToString();
            string details = entry["details"]?.ToString();

            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(type)) continue;

            if (!agentRegistry.TryGetValue(agentId, out Amongi agent) || agent == null)
            {
                // Agents may have spawned after Awake
                RebuildRegistry();
                agentRegistry.TryGetValue(agentId, out agent);
            }

            if (agent == null)
            {
                Debug.LogWarning($"[Connection] No agent found for id '{agentId}'.");
                continue;
            }

            agent.DispatchAction(type, details);
        }

        yield return null;
    }

    private void OnApplicationQuit()
    {
        isRunning = false;

        // Close first to unblock Read()
        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }

        try { receiveThread?.Join(200); } catch { }
    }
}
