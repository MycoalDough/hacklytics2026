using System.Collections.Generic;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    [Header("Task Pools — assign Waypoints in the Inspector")]
    public List<Waypoint> commonTasks = new List<Waypoint>();
    public List<Waypoint> shortTasks = new List<Waypoint>();
    public List<Waypoint> longTasks = new List<Waypoint>();

    private List<Waypoint> _commonPool;
    private List<Waypoint> _shortPool;
    private List<Waypoint> _longPool;

    private void Awake()
    {
        var seen = new HashSet<Waypoint>();

        _commonPool = Shuffle(Deduplicate(commonTasks, seen));
        _shortPool = Shuffle(Deduplicate(shortTasks, seen));
        _longPool = Shuffle(Deduplicate(longTasks, seen));

        AssignAllAgents();
    }

    private List<Waypoint> Deduplicate(List<Waypoint> source, HashSet<Waypoint> seen)
    {
        var result = new List<Waypoint>();
        foreach (Waypoint wp in source)
        {
            if (wp != null && seen.Add(wp))
                result.Add(wp);
        }
        return result;
    }

    private void AssignAllAgents()
    {
        Amongi[] agents = FindObjectsByType<Amongi>(FindObjectsSortMode.None);
        foreach (Amongi agent in agents)
            AssignTasks(agent);
    }

    public void AssignTasks(Amongi agent)
    {
        agent.tasks = new TaskHandler
        {
            commonTasks = Draw(_commonPool, 3),
            shortTasks = Draw(_shortPool, 3),
            longTasks = Draw(_longPool, 1)
        };

        Debug.Log($"[TaskManager] Assigned tasks to {agent.agentId}: " +
                  $"{agent.tasks.commonTasks.Count} common, " +
                  $"{agent.tasks.shortTasks.Count} short, " +
                  $"{agent.tasks.longTasks.Count} long");
    }

    private List<Waypoint> Draw(List<Waypoint> pool, int count)
    {
        var drawn = new List<Waypoint>();
        var available = new List<Waypoint>(pool);

        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0)
            {
                Debug.LogWarning("[TaskManager] Not enough unique tasks in pool!");
                break;
            }
            int idx = Random.Range(0, available.Count);
            drawn.Add(available[idx]);
            available.RemoveAt(idx); //only removes from the local copy (3:23am)
        }
        return drawn;
    }


    private List<Waypoint> Shuffle(List<Waypoint> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

[System.Serializable]
public class TaskHandler
{
    public List<Waypoint> commonTasks = new List<Waypoint>();
    public List<Waypoint> shortTasks = new List<Waypoint>();
    public List<Waypoint> longTasks = new List<Waypoint>();

    public bool HasTasksRemaining =>
        commonTasks.Count > 0 || shortTasks.Count > 0 || longTasks.Count > 0;

    public Waypoint GetNextTask()
    {
        if (commonTasks.Count > 0) return commonTasks[0];
        if (shortTasks.Count > 0) return shortTasks[0];
        if (longTasks.Count > 0) return longTasks[0];
        return null;
    }

    public void CompleteTask(Waypoint task)
    {
        commonTasks.Remove(task);
        shortTasks.Remove(task);
        longTasks.Remove(task);
    }
}
