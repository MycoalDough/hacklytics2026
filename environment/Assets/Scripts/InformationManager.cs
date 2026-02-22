// InformationManager.cs
using System.Collections.Generic;
using UnityEngine;

public class InformationManager : MonoBehaviour
{
    public static InformationManager Instance { get; private set; }

    [Header("Special Rooms")]
    public Waypoint securityNode;
    public Waypoint adminNode;

    [Header("Camera Coverage")]
    public List<Waypoint> securityNodes = new List<Waypoint>();

    private LocationManager _locationManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _locationManager = FindFirstObjectByType<LocationManager>();
        if (_locationManager == null)
            Debug.LogError("[InformationManager] No LocationManager found in scene.");
    }

    // -------------------------------------------------------------------------
    // Security — returns the agentIds of all agents currently inside any
    // securityNode-covered room. Null if the querying agent isn't at security.
    // -------------------------------------------------------------------------
    public List<string> GetSecurityData(Amongi agent)
    {
        if (agent.currentRoom != securityNode)
        {
            Debug.LogWarning($"[InformationManager] {agent.agentId} tried Security but isn't in the Security room.");
            return null;
        }

        var visible = new List<string>();
        foreach (Amongi a in _locationManager.allAmongi)
        {
            if (a.currentRoom != null && securityNodes.Contains(a.currentRoom))
                visible.Add(a.agentId);
        }
        return visible;
    }

    // -------------------------------------------------------------------------
    // Admin — returns a room-name → agent-count map for every occupied room.
    // Null if the querying agent isn't at admin. No names, just numbers —
    // faithful to Among Us admin table behavior.
    // -------------------------------------------------------------------------
    public Dictionary<string, int> GetAdminData(Amongi agent)
    {
        if (agent.currentRoom != adminNode)
        {
            Debug.LogWarning($"[InformationManager] {agent.agentId} tried Admin but isn't in the Admin room.");
            return null;
        }

        var roomCounts = new Dictionary<string, int>();
        foreach (Amongi a in _locationManager.allAmongi)
        {
            if (a.currentRoom == null) continue;

            string roomName = a.currentRoom.name;
            if (!roomCounts.ContainsKey(roomName))
                roomCounts[roomName] = 0;

            roomCounts[roomName]++;
        }
        return roomCounts;
    }
}
