// WaypointPath.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPath", menuName = "AmongUs/Waypoint Path")]
public class WaypointPath : ScriptableObject
{
    public List<Waypoint> waypoints = new List<Waypoint>();
}
