using System;
using System.Collections.Generic;
using UnityEngine;

public class AgentPathFollower : MonoBehaviour
{
    public float speed = 2f;
    public float waypointTolerance = 0.1f;

    [Header("Crowd Offset")]
    [SerializeField] private float offsetRadius = 0.35f;
    [SerializeField] private float offsetLerpSpeed = 4f;

    public Action<Waypoint> onWaypointReached;
    private Action _onPathComplete;

    private List<Waypoint> _currentPath = new List<Waypoint>();
    private int _targetIndex = 0;

    public Waypoint CurrentNode;
    private Amongi _agent;

    private Vector2 _currentOffset;
    private Vector2 _targetOffset;

    private void Awake()
    {
        _agent = GetComponent<Amongi>();

        _currentOffset = UnityEngine.Random.insideUnitCircle * offsetRadius;
        _targetOffset = _currentOffset;
    }

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

    public void SnapToNode(Waypoint node)
    {
        CurrentNode = node;
        transform.position = (Vector2)node.transform.position + _currentOffset;
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
        PickNewOffset();
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


    private void Update()
    {
        _currentOffset = Vector2.Lerp(_currentOffset, _targetOffset, offsetLerpSpeed * Time.deltaTime);

        if (!IsMoving) return;

        Vector2 targetPos = (Vector2)_currentPath[_targetIndex].transform.position + _currentOffset;
        transform.position = Vector2.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPos) <= waypointTolerance)
        {
            Waypoint reached = _currentPath[_targetIndex];
            CurrentNode = reached;
            onWaypointReached?.Invoke(reached);

            _targetIndex++;
            PickNewOffset(); 

            if (_targetIndex >= _currentPath.Count)
            {
                string locationName = CurrentNode != null ? CurrentNode.name : "Unknown";
                _currentPath.Clear();
                _agent.currentRoom = CurrentNode;
                _agent.sendInformation("reachLocation", details: $"you have reached {locationName}");
                _onPathComplete?.Invoke();
            }
        }
    }

    private void PickNewOffset()
    {
        _targetOffset = UnityEngine.Random.insideUnitCircle * offsetRadius;
    }

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
