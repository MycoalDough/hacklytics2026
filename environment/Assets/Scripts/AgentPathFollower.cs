// AgentPathFollower.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class AgentPathFollower : MonoBehaviour
{
    public float speed = 2f;
    public float waypointTolerance = 0.1f;

    public Action<Waypoint> onWaypointReached;
    private Action _onPathComplete;

    private List<Waypoint> _currentPath = new List<Waypoint>();
    private int _targetIndex = 0;

    public Waypoint CurrentNode { get; private set; }

    // ─── Public API ───────────────────────────────────────────────

    /// <summary>Pass a single target Waypoint — A* figures out the route.</summary>
    public void MoveTo(Waypoint target, Action onComplete = null)
    {
        if (CurrentNode == null)
        {
            Debug.LogWarning($"{name}: CurrentNode is null. Snap the agent to a starting waypoint first.");
            return;
        }

        if (CurrentNode == target)
        {
            onComplete?.Invoke();
            return;
        }

        List<Waypoint> path = WaypointPathfinder.FindPath(CurrentNode, target);
        SetPath(path, onComplete);
    }

    /// <summary>Manually set CurrentNode when the agent spawns.</summary>
    public void SnapToNode(Waypoint node)
    {
        CurrentNode = node;
        transform.position = node.transform.position;
    }

    public void SetPath(List<Waypoint> path, Action onComplete = null)
    {
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"{name}: SetPath called with empty or null path.");
            onComplete?.Invoke();
            return;
        }

        _currentPath = path;
        _targetIndex = 0;
        _onPathComplete = onComplete;
    }

    public void SetPath(WaypointPath path, Action onComplete = null)
        => SetPath(new List<Waypoint>(path.waypoints), onComplete);

    public void ClearPath()
    {
        _currentPath.Clear();
        _targetIndex = 0;
        _onPathComplete = null;
    }

    public bool IsMoving => _currentPath != null && _currentPath.Count > 0;

    // ─── Movement ─────────────────────────────────────────────────

    private void Update()
    {
        if (!IsMoving) return;

        Vector2 targetPos = _currentPath[_targetIndex].transform.position;
        transform.position = Vector2.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPos) <= waypointTolerance)
        {
            Waypoint reached = _currentPath[_targetIndex];
            CurrentNode = reached;
            onWaypointReached?.Invoke(reached);

            _targetIndex++;

            if (_targetIndex >= _currentPath.Count)
            {
                _currentPath.Clear();
                _onPathComplete?.Invoke();
            }
        }
    }

    // ─── Debug ────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (_currentPath == null || _currentPath.Count == 0) return;

        Gizmos.color = Color.yellow;
        for (int i = _targetIndex; i < _currentPath.Count - 1; i++)
        {
            if (_currentPath[i] != null && _currentPath[i + 1] != null)
                Gizmos.DrawLine(_currentPath[i].transform.position, _currentPath[i + 1].transform.position);
        }

        if (_targetIndex < _currentPath.Count && _currentPath[_targetIndex] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_currentPath[_targetIndex].transform.position, 0.2f);
        }
    }
}
