using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LocationManager : MonoBehaviour
{
    public List<Amongi> allAmongi = new List<Amongi>();
    public float time;

    [Header("UI")]
    public Text tasksLeftText;

    private readonly HashSet<(int, int)> _sameRoomPairs = new();
    private readonly HashSet<(int, int)> _nearPairs = new();
    private readonly HashSet<(int, int)> _closestPairs = new();

    private void Update()
    {
        time += Time.deltaTime;
        CheckSameRoom();
        CheckProximity();
        UpdateTasksUI();
    }

    private void UpdateTasksUI()
    {
        if (tasksLeftText == null) return;
        int total = 0;
        foreach (Amongi a in allAmongi)
        {
            if (a.role != "CREWMATE" || a.currentState == "DEAD") continue;
            total += a.tasks.commonTasks.Count
                   + a.tasks.shortTasks.Count
                   + a.tasks.longTasks.Count;
        }
        tasksLeftText.text = $"TASKS LEFT: {total}";
    }

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
                if (_sameRoomPairs.Add(pair))
                    Debug.Log($"[LocationManager] {a.agentId} and {b.agentId} " +
                              $"are now in the same room: {a.currentRoom.name}");
            }

        _sameRoomPairs.IntersectWith(framePairs);
    }
    private void CheckProximity()
    {
        for (int i = 0; i < allAmongi.Count; i++)
            for (int j = i + 1; j < allAmongi.Count; j++)
            {
                Amongi a = allAmongi[i];
                Amongi b = allAmongi[j];
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                var pair = MakePair(a, b);

                float sharedNear = Mathf.Min(a.near_radius, b.near_radius);
                float sharedClosest = Mathf.Min(a.closest_radius, b.closest_radius);

                if (dist <= sharedNear)
                {
                    
                    if (_nearPairs.Add(pair))
                    {
                        a.OnNearEnter(b);
                        b.OnNearEnter(a);
                    }
                }
                else if (_nearPairs.Remove(pair))
                {
                    a.OnNearExit(b);
                    b.OnNearExit(a);
                }

                // --- Closest radius ---
                if (dist <= sharedClosest)
                {
                    if (_closestPairs.Add(pair))
                    {
                        a.OnClosestEnter(b);
                        b.OnClosestEnter(a);
                    }
                }
                else if (_closestPairs.Remove(pair))
                {
                    a.OnClosestExit(b);
                    b.OnClosestExit(a);
                }
            }
    }

    private static (int, int) MakePair(Amongi a, Amongi b)
    {
        int x = a.GetInstanceID();
        int y = b.GetInstanceID();
        return x < y ? (x, y) : (y, x);
    }
}
