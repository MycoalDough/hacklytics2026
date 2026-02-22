// LocationManager.cs
using UnityEngine;
using System.Collections.Generic;

public class LocationManager : MonoBehaviour
{
    public List<Amongi> allAmongi = new List<Amongi>();
    public float time;

    // Active-pair sets — a pair is inserted on enter, removed on exit
    // This means logs fire ONCE on enter and ONCE on exit, not every frame
    private readonly HashSet<(int, int)> _sameRoomPairs = new();
    private readonly HashSet<(int, int)> _nearPairs = new();
    private readonly HashSet<(int, int)> _closestPairs = new();

    private void Update()
    {
        time += Time.deltaTime;
        CheckSameRoom();
        CheckProximity();
    }

    // -------------------------------------------------------------------------
    // Same-room detection
    // -------------------------------------------------------------------------
    private void CheckSameRoom()
    {
        var framePairs = new HashSet<(int, int)>();

        for (int i = 0; i < allAmongi.Count; i++)
            for (int j = i + 1; j < allAmongi.Count; j++)
            {
                Amongi a = allAmongi[i];
                Amongi b = allAmongi[j];

                if (a.currentRoom == null || b.currentRoom == null) continue;
                if (a.currentRoom != b.currentRoom) continue;

                var pair = MakePair(a, b);
                framePairs.Add(pair);

                if (_sameRoomPairs.Add(pair))   // true = was newly added (enter)
                    Debug.Log($"[LocationManager] {a.agentId} and {b.agentId} " +
                              $"are now in the same room: {a.currentRoom.name}");
            }

        // Detect exits — pairs in the old set but not this frame
        _sameRoomPairs.IntersectWith(framePairs);
    }

    // -------------------------------------------------------------------------
    // Radius proximity detection
    // -------------------------------------------------------------------------
    private void CheckProximity()
    {
        var frameNear = new HashSet<(int, int)>();
        var frameClosest = new HashSet<(int, int)>();

        for (int i = 0; i < allAmongi.Count; i++)
            for (int j = i + 1; j < allAmongi.Count; j++)
            {
                Amongi a = allAmongi[i];
                Amongi b = allAmongi[j];
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                var pair = MakePair(a, b);

                // "In each other's near_radius" — inside BOTH agents' spheres
                float sharedNear = Mathf.Min(a.near_radius, b.near_radius);
                float sharedClosest = Mathf.Min(a.closest_radius, b.closest_radius);

                if (dist <= sharedNear)
                {
                    frameNear.Add(pair);
                    if (_nearPairs.Add(pair))       // enter
                    {
                        a.OnNearEnter(b);
                        b.OnNearEnter(a);
                    }
                }

                if (dist <= sharedClosest)
                {
                    frameClosest.Add(pair);
                    if (_closestPairs.Add(pair))    // enter
                    {
                        a.OnClosestEnter(b);
                        b.OnClosestEnter(a);
                    }
                }
            }

        _nearPairs.IntersectWith(frameNear);
        _closestPairs.IntersectWith(frameClosest);
    }

    // -------------------------------------------------------------------------
    // Canonical pair key — uses InstanceID so order doesn't matter
    // -------------------------------------------------------------------------
    private static (int, int) MakePair(Amongi a, Amongi b)
    {
        int x = a.GetInstanceID();
        int y = b.GetInstanceID();
        return x < y ? (x, y) : (y, x);
    }
}
