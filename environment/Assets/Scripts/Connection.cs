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

    [Header("Batching")]
    public float batchWindow = 0.5f;

    private readonly object streamLock = new object();
    private readonly object queueLock = new object();

    private readonly List<JObject> eventQueue = new List<JObject>();

    private volatile bool _waitingForResponse = false;

    private float _batchTimer = -1f;

    private Dictionary<string, Amongi> agentRegistry = new Dictionary<string, Amongi>();

    private void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
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

    public static void QueueEvent(string eventJson)
    {
        if (instance == null) return;

        if (Volatile.Read(ref instance._waitingForResponse))
            return;

        JObject parsed;
        try
        {
            parsed = JObject.Parse(eventJson);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Connection] QueueEvent parse error: {e}");
            return;
        }

        lock (instance.queueLock)
        {
            if (instance._waitingForResponse)
                return;

            instance.eventQueue.Add(parsed);
        }
    }

    public static void FlushEvents()
    {
        if (instance == null || instance.stream == null) return;

        List<JObject> toSend;

        lock (instance.queueLock)
        {
            if (instance.eventQueue.Count == 0) return;

            instance._waitingForResponse = true;

            toSend = new List<JObject>(instance.eventQueue);
            instance.eventQueue.Clear();

            instance._batchTimer = -1f;
        }

        var payload = new JObject
        {
            ["type"] = "events",
            ["events"] = new JArray(toSend)
        };

        Time.timeScale = 0f;

        instance.SendLine(payload.ToString(Newtonsoft.Json.Formatting.None));
    }

    private void LateUpdate()
    {
        if (_waitingForResponse) return;

        int count;
        lock (queueLock) count = eventQueue.Count;

        if (count > 0)
        {
            if (_batchTimer < 0f)
                _batchTimer = 0f;

            _batchTimer += Time.unscaledDeltaTime;

            if (_batchTimer >= batchWindow)
                FlushEvents();
        }
        else
        {
            _batchTimer = -1f;
        }
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

                    JArray actionsArray;
                    JToken token = JToken.Parse(line);

                    if (token is JArray arr)
                        actionsArray = arr;
                    else if (token is JObject obj)
                        actionsArray = new JArray(obj);
                    else
                        continue;

                    if (umtd != null)
                        umtd.Enqueue(() => StartCoroutine(ApplyActionsCoroutine(actionsArray)));
                    else
                        Debug.LogError("[Connection] UnityMainThreadDispatcher not found.");
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
        _waitingForResponse = false;
        Time.timeScale = 1f;

        if (actionsArray == null) yield break;

        foreach (JObject entry in actionsArray)
        {
            string agentId = entry["agent"]?.ToString();
            string type = entry["type"]?.ToString();
            string details = entry["details"]?.ToString();

            if (string.IsNullOrEmpty(type)) continue;

            if (type == "Chat")
            {
                GameManager.instance.HandleChat(agentId, details);
                continue;
            }

            if (type == "Vote")
            {
                GameManager.instance.HandleVote(details);
                continue;
            }

            if (string.IsNullOrEmpty(agentId)) continue;

            if (!agentRegistry.TryGetValue(agentId, out Amongi agent) || agent == null)
            {
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

    public static void ClearQueue()
    {
        if (instance == null) return;

        lock (instance.queueLock)
        {
            instance.eventQueue.Clear();
        }

        instance._batchTimer = -1f;
    }

    private void OnApplicationQuit()
    {
        isRunning = false;

        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }
        try { receiveThread?.Join(200); } catch { }
    }

    public static void QueuePriorityEvent(string eventJson)
    {
        if (instance == null) return;

        JObject parsed;
        try { parsed = JObject.Parse(eventJson); }
        catch (Exception e) { Debug.LogError($"[Connection] QueuePriorityEvent parse error: {e}"); return; }

        lock (instance.queueLock)
        {
            instance.eventQueue.Add(parsed);
        }
    }

    public static void FlushIfNotWaiting()
    {
        if (instance == null) return;
        if (Volatile.Read(ref instance._waitingForResponse)) return; 
        FlushEvents();
    }

}
