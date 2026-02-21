using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class Connection : MonoBehaviour
{
    [Header("Socket")]
    private const string host = "127.0.0.1"; // localhost
    private int port = 12345;
    TcpClient client;
    NetworkStream stream;
    private Thread receiveThread;
    private bool isRunning = true;
    static Connection instance;
    public UnityMainThreadDispatcher umtd;

    [Header("Environment")]
    public GameManagerMultiTeam gm;
    public Text action_space_text;

    [Header("Agent Configuration")]
    [SerializeField] private int numberOfAgents = 4;

    // Property to get/set number of agents
    public int NumberOfAgents
    {
        get { return numberOfAgents; }
        set { numberOfAgents = Mathf.Max(1, value); } // Ensure at least 1 agent
    }

    // Start is called before the first frame update
    void Start()
    {
        resetEnv();
    }

    private void Awake()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-rlport" && int.TryParse(args[i + 1], out int p))
            {
                port = p;
                break;
            }
        }

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
            // Start the receive thread
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception: {e.Message}");
        }
    }

    void ReceiveData()
    {
        Debug.Log("Thread started!");
        byte[] data = new byte[1024];
        while (isRunning)
        {
            try
            {
                int bytesRead = stream.Read(data, 0, data.Length);
                if (bytesRead > 0)
                {
                    string message = Encoding.UTF8.GetString(data, 0, bytesRead);
                    if (message == "get_data_for")
                    {
                        // Enqueue the getItems call to be executed on the main thread
                        umtd.Enqueue(() => {
                            string toSend = "";
                            toSend = gm.envData();
                            action_space_text.text = toSend;
                            byte[] dataToSend = Encoding.UTF8.GetBytes(toSend);
                            stream.Write(dataToSend, 0, dataToSend.Length);
                        });
                    }
                    else if (message.Contains("play_state")) //receive: play_step:ACTION1|ACTION2|ACTION3|...
                                                            //send: REWARD:DONE:OBS_
                    {
                        string[] step = message.Split(':');
                        umtd.Enqueue(() => {
                            StartCoroutine(PlayStepCoroutine(step[1]));
                        });
                    }
                    if (message == "reset")
                    {
                        umtd.Enqueue(() =>
                        {
                            resetEnv();
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception: {e.Message}");
            }
        }
    }

    private IEnumerator PlayStepCoroutine(string action)
    {
        string[] actions = action.Split('|');

        // Validate that we have the expected number of actions
        if (actions.Length != numberOfAgents)
        {
            Debug.LogError($"Expected {numberOfAgents} actions, but received {actions.Length}");
            yield break;
        }


        List<int> team1actions = new List<int>();
        List<int> team2actions = new List<int>();

        int actionNum = 0;
        for (int i = 0; i < actions.Length; i++)
        {
            if (actionNum < gm.team1Agents.Count)
            {
                team1actions.Add(int.Parse(actions[i]));
            }
            else
            {
                team2actions.Add(int.Parse(actions[i]));
            }
            actionNum++;
        }


        yield return StartCoroutine(gm.playActions(team1actions, team2actions));

        string result = gm.result;
        string envData = gm.envData();
        string response = result + envData;
        action_space_text.text = envData;
        byte[] dataToSend = Encoding.UTF8.GetBytes(response);
        stream.Write(dataToSend, 0, dataToSend.Length);
        gm.result = "";
    }

    public void resetEnv()
    {
        gm.RESET();
    }

    // Helper method to set number of agents at runtime
    public void SetNumberOfAgents(int count)
    {
        NumberOfAgents = count;
        Debug.Log($"Number of agents set to: {numberOfAgents}");
    }

    // Helper method to get current agent count
    public int GetNumberOfAgents()
    {
        return numberOfAgents;
    }
}