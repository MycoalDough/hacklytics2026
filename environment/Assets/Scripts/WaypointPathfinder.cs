using System.Collections.Generic;
using UnityEngine;

public static class WaypointPathfinder
{
    //THIS IS A* ALGORITHM!!!! im so goate.d.
    public static List<Waypoint> FindPath(Waypoint start, Waypoint goal)
    {
        if (start == goal) return new List<Waypoint> { start };

        var openSet = new List<Waypoint> { start };

        var cameFrom = new Dictionary<Waypoint, Waypoint>();

        var gCost = new Dictionary<Waypoint, float>();
        gCost[start] = 0f;

        var fCost = new Dictionary<Waypoint, float>();
        fCost[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            Waypoint current = GetLowestF(openSet, fCost);

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);

            foreach (Waypoint neighbor in current.neighbors)
            {
                if (neighbor == null) continue;

                float tentativeG = gCost[current] +
                    Vector2.Distance(current.transform.position, neighbor.transform.position);

                if (!gCost.ContainsKey(neighbor) || tentativeG < gCost[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gCost[neighbor] = tentativeG;
                    fCost[neighbor] = tentativeG + Heuristic(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        Debug.LogWarning($"No path found from {start.name} to {goal.name}");
        return null;
    }

    private static float Heuristic(Waypoint a, Waypoint b)
        => Vector2.Distance(a.transform.position, b.transform.position);

    private static Waypoint GetLowestF(List<Waypoint> openSet, Dictionary<Waypoint, float> fCost)
    {
        Waypoint best = openSet[0];
        foreach (var node in openSet)
            if (fCost.TryGetValue(node, out float f) && f < fCost.GetValueOrDefault(best, float.MaxValue))
                best = node;
        return best;
    }

    private static List<Waypoint> ReconstructPath(Dictionary<Waypoint, Waypoint> cameFrom, Waypoint current)
    {
        var path = new List<Waypoint> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }
}
