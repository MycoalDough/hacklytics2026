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

    public string GetSecurityData(Amongi agent)
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
                visible.Add($"{a.agentId} in {a.currentRoom.name}");
        }

        if (visible.Count == 0)
            return "Security Data: No agents on camera. |";

        var sb = new System.Text.StringBuilder("Security Data: ");
        foreach (string entry in visible)
            sb.Append($"{entry} is on camera. ");

        sb.Append("|");
        return sb.ToString();
    }

    public string GetAdminData(Amongi agent)
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

        var sb = new System.Text.StringBuilder("Admin Data: ");
        foreach (var kvp in roomCounts)
            sb.Append($"There is {kvp.Value} in {kvp.Key}. ");

        sb.Append("|");
        return sb.ToString();
    }
}
