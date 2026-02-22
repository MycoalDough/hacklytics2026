using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Meeting Animation")]
    public Animator anim;

    [Header("Meeting UI")]
    public GameObject meetingScreen;
    public TextMeshProUGUI meetingText;

    [Header("Vote Animation")]
    public Animator killAnim;

    [Header("Vote Result UI")]
    public Image voteResultImage;
    public TextMeshProUGUI voteResultText;

    [Header("Agent Color Sprites")]
    public List<AgentColorEntry> agentColors = new List<AgentColorEntry>();

    private Dictionary<string, Sprite> _spriteLookup;

    private static readonly Dictionary<string, string> _agentHexColors =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Red",    "#FF4444" },
            { "Blue",   "#4499FF" },
            { "Green",  "#44DD44" },
            { "Yellow", "#FFEE44" },
            { "Purple", "#CC66FF" },
            { "Pink",   "#FF88CC" },
        };

    private readonly List<string> _chatLines = new List<string>();

    public Waypoint deadNode;

    [System.Serializable]
    public class AgentColorEntry
    {
        public string agentId;
        public Sprite sprite;
    }

    private void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;

        _spriteLookup = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in agentColors)
        {
            if (!string.IsNullOrEmpty(entry.agentId) && entry.sprite != null)
                _spriteLookup[entry.agentId] = entry.sprite;
        }
    }


    public void HandleReport() => StartCoroutine(ReportSequence());
    public void HandleMeeting() => StartCoroutine(MeetingSequence());

    private IEnumerator ReportSequence()
    {
        anim.Play("BodyReport", -1, 0f);
        yield return new WaitForSecondsRealtime(2f);
        _chatLines.Clear();
        meetingText.text = "";
        meetingScreen.SetActive(true);
    }

    private IEnumerator MeetingSequence()
    {
        anim.Play("MeetingReport", -1, 0f);
        yield return new WaitForSecondsRealtime(2f);
        _chatLines.Clear();
        meetingText.text = "";
        meetingScreen.SetActive(true);
    }


    public void HandleChat(string agentId, string message)
    {
        string hex = _agentHexColors.TryGetValue(agentId, out string h) ? h : "#FFFFFF";
        _chatLines.Add($"<color={hex}>[{agentId}]</color>: {message}");
        RefreshChatText();
    }

    private void RefreshChatText()
    {
        meetingText.text = string.Join("\n", _chatLines);
        meetingText.ForceMeshUpdate();

        while (_chatLines.Count > 1 && meetingText.isTextOverflowing)
        {
            _chatLines.RemoveAt(0);
            meetingText.text = string.Join("\n", _chatLines);
            meetingText.ForceMeshUpdate();
        }
    }


    public void HandleVote(string votedOutAgentId)
    {
        StartCoroutine(VoteOutSequence(votedOutAgentId));
    }

    private IEnumerator VoteOutSequence(string votedOutAgentId)
    {
        meetingScreen.SetActive(false);
        killAnim.Play("VoteOut", -1, 0f);

        bool isSkip = string.IsNullOrEmpty(votedOutAgentId) ||
                      votedOutAgentId.Equals("skip", System.StringComparison.OrdinalIgnoreCase) ||
                      votedOutAgentId.Contains("skip") ||
                      votedOutAgentId.Contains("Skip");

        string id = votedOutAgentId.ToLower();
        if (id.Contains("red")) votedOutAgentId = "Red";
        else if (id.Contains("blue")) votedOutAgentId = "Blue";
        else if (id.Contains("green")) votedOutAgentId = "Green";
        else if (id.Contains("yellow")) votedOutAgentId = "Yellow";
        else if (id.Contains("purple")) votedOutAgentId = "Purple";
        else if (id.Contains("pink")) votedOutAgentId = "Pink";

        if (isSkip)
        {
            voteResultImage.sprite = null;
            voteResultText.text = "Skipped";
        }
        else
        {
            if (_spriteLookup.TryGetValue(votedOutAgentId, out Sprite sprite))
            {
                voteResultImage.gameObject.SetActive(true);
                voteResultImage.sprite = sprite;
            }
            else
            {
                voteResultImage.gameObject.SetActive(false);
            }

            bool wasImposter = false;
            Amongi[] allAgents = FindObjectsByType<Amongi>(FindObjectsSortMode.None);
            foreach (Amongi agent in allAgents)
            {
                if (agent.agentId == votedOutAgentId)
                {
                    agent.currentState = "DEAD";
                    wasImposter = agent.role == "IMPOSTER";
                    break;
                }
            }

            voteResultText.text = wasImposter
                ? $"{votedOutAgentId} was the Imposter."
                : $"{votedOutAgentId} was not the Imposter.";
        }

        Amongi[] agents = FindObjectsByType<Amongi>(FindObjectsSortMode.None);
        MeetingManager.Instance.EndMeeting();
        foreach (Amongi agent in agents)
        {
            if (agent.currentState == "DEAD")
            {
                agent.myPathFollower?.ClearPath();
                agent.myPathFollower?.SnapToNode(deadNode);
                agent.currentRoom = deadNode;
            }
            else
            {
                agent.myPathFollower?.ClearPath();
                agent.myPathFollower?.SnapToNode(MeetingManager.Instance.cafeteriaNode);
                agent.currentRoom = MeetingManager.Instance.cafeteriaNode;
                agent.sendInformation("meetingEnd", details: "the meeting has ended with " + votedOutAgentId + " being voted.");
            }

            if(agent.currentState == "IMPOSTER")
            {
                agent.killTimer = 15;
                agent.sabotageTimer = 0;
            }
        }

        yield return new WaitForSecondsRealtime(1.5f);

        _chatLines.Clear();
        meetingText.text = "";
        meetingScreen.SetActive(false);




        Time.timeScale = 1f;
    }
}
