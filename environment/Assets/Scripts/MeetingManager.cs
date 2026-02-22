// MeetingManager.cs
using System.Collections.Generic;
using UnityEngine;

public class MeetingManager : MonoBehaviour
{
    public static MeetingManager Instance { get; private set; }

    [Header("Nodes")]
    public Waypoint cafeteriaNode;

    public bool MeetingActive { get; private set; } = false;
    public Amongi LastCaller { get; private set; } = null;
    public Amongi LastReportedBody { get; private set; } = null;

    private LocationManager _locationManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _locationManager = FindFirstObjectByType<LocationManager>();
        if (_locationManager == null)
            Debug.LogError("[MeetingManager] No LocationManager found in scene.");
    }

    // -------------------------------------------------------------------------
    // TryReport — scans the caller's current room for a dead body.
    // Returns false if no body was found.
    // -------------------------------------------------------------------------
    public bool TryReport(Amongi caller)
    {
        foreach (Amongi a in _locationManager.allAmongi)
        {
            if (a.currentState == "DEAD" && a.currentRoom == caller.currentRoom)
            {
                CallMeeting(caller, a);
                return true;
            }
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // TryButton — only works from the cafeteria node.
    // -------------------------------------------------------------------------
    public bool TryButton(Amongi caller)
    {
        if (caller.currentRoom != cafeteriaNode)
            return false;

        CallMeeting(caller, null);
        return true;
    }

    // -------------------------------------------------------------------------
    // CallMeeting — shared logic for both REPORT and BUTTON.
    // -------------------------------------------------------------------------
    private void CallMeeting(Amongi caller, Amongi reportedBody)
    {
        if (MeetingActive)
        {
            Debug.LogWarning("[MeetingManager] A meeting is already in progress.");
            return;
        }

        MeetingActive = true;
        LastCaller = caller;
        LastReportedBody = reportedBody;

        if (reportedBody != null)
            Debug.Log($"[MeetingManager] {caller.agentId} reported {reportedBody.agentId}'s body!");
        else
            Debug.Log($"[MeetingManager] {caller.agentId} hit the emergency button!");

        // TODO: Hook voting logic or GameManager here, e.g.:
        // GameManager.Instance?.BeginVote(caller, reportedBody);
    }

    // -------------------------------------------------------------------------
    // EndMeeting — call this when voting concludes.
    // -------------------------------------------------------------------------
    public void EndMeeting()
    {
        if (!MeetingActive)
        {
            Debug.LogWarning("[MeetingManager] No meeting is currently active.");
            return;
        }

        MeetingActive = false;
        LastCaller = null;
        LastReportedBody = null;
        Debug.Log("[MeetingManager] Meeting ended.");
    }
}
