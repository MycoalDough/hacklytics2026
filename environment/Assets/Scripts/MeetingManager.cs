using System.Collections.Generic;
using UnityEngine;

public class MeetingManager : MonoBehaviour
{
    public static MeetingManager Instance { get; private set; }

    [Header("Nodes")]
    public Waypoint cafeteriaNode;

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


    public bool TryButton(Amongi caller)
    {
        if (caller.currentRoom != cafeteriaNode)
            return false;

        CallMeeting(caller, null);
        return true;
    }


    private void CallMeeting(Amongi caller, Amongi reportedBody)
    {

        if (reportedBody != null)
            Debug.Log($"[MeetingManager] {caller.agentId} reported {reportedBody.agentId}'s body!");
        else
            Debug.Log($"[MeetingManager] {caller.agentId} hit the emergency button!");

    }

    public void EndMeeting()
    {
        Debug.Log("[MeetingManager] Meeting ended.");
    }
}
