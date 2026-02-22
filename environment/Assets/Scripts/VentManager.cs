// VentManager.cs
using System.Collections.Generic;
using UnityEngine;

public class VentManager : MonoBehaviour
{
    public static VentManager Instance { get; private set; }

    [Header("Vent Networks")]
    [Tooltip("Each entry is a group of connected vents. Any vent in a group connects to all others.")]
    public List<VentNetwork> ventNetworks = new List<VentNetwork>();

    // Fast lookup: waypoint → its network
    private Dictionary<Waypoint, VentNetwork> _ventMap = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        BuildVentMap();
    }

    private void BuildVentMap()
    {
        _ventMap.Clear();
        foreach (VentNetwork network in ventNetworks)
        {
            foreach (Waypoint vent in network.vents)
            {
                if (vent == null) continue;
                if (_ventMap.ContainsKey(vent))
                    Debug.LogWarning($"[VentManager] '{vent.name}' appears in multiple networks — using first.");
                else
                    _ventMap[vent] = network;
            }
        }
        Debug.Log($"[VentManager] Built vent map with {_ventMap.Count} registered vents.");
    }

    // -------------------------------------------------------------------------
    // Returns true if the given waypoint is a registered vent.
    // -------------------------------------------------------------------------
    public bool IsVent(Waypoint waypoint) => waypoint != null && _ventMap.ContainsKey(waypoint);

    // -------------------------------------------------------------------------
    // Returns all vents the given vent connects to (excludes itself).
    // Returns null if the waypoint is not a vent.
    // -------------------------------------------------------------------------
    public List<Waypoint> GetConnectedVents(Waypoint vent)
    {
        if (!_ventMap.TryGetValue(vent, out VentNetwork network)) return null;

        var connected = new List<Waypoint>();
        foreach (Waypoint v in network.vents)
            if (v != null && v != vent)
                connected.Add(v);
        return connected;
    }

    // -------------------------------------------------------------------------
    // Returns true if origin and target are in the same vent network.
    // -------------------------------------------------------------------------
    public bool AreConnected(Waypoint origin, Waypoint target)
    {
        if (!_ventMap.TryGetValue(origin, out VentNetwork originNetwork)) return false;
        if (!_ventMap.TryGetValue(target, out VentNetwork targetNetwork)) return false;
        return originNetwork == targetNetwork;
    }
}

// -------------------------------------------------------------------------
// VentNetwork — one group of connected vents, assignable in the Inspector.
// -------------------------------------------------------------------------
[System.Serializable]
public class VentNetwork
{
    public string networkName = "New Network";
    public List<Waypoint> vents = new List<Waypoint>();
}
