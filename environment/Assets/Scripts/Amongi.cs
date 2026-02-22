using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class Amongi : MonoBehaviour
{
    public string agentId = "Red";
    public string currentState = "Idle";

    [Header("Role")]
    public string role = "CREWMATE"; // "CREWMATE" or "IMPOSTER"

    [Header("Imposter Cooldowns")]
    public float killTimer = 30f;
    public float sabotageTimer = 30f;
    public float killCooldown = 30f;
    public float sabotageCooldown = 30f;

    public bool CanKill => role == "IMPOSTER" && killTimer >= killCooldown;
    public bool CanSabotage => role == "IMPOSTER" && sabotageTimer >= sabotageCooldown;

    [Header("References")]
    public TaskHandler tasks = new TaskHandler();
    public LocationManager locationManager;
    public AgentPathFollower myPathFollower;

    [Header("Runtime")]
    public Waypoint currentRoom;

    [Header("Proximity Radii")]
    public float near_radius = 4f;
    public float closest_radius = 1.5f;

    // -------------------------------------------------------------------------
    // Task execution state
    // -------------------------------------------------------------------------
    private Coroutine _taskCoroutine;
    public bool IsDoingTask { get; private set; } = false;

    // -------------------------------------------------------------------------
    // Venting state
    // -------------------------------------------------------------------------
    public bool IsVenting { get; private set; } = false;

    // -------------------------------------------------------------------------
    // Waypoint cache
    // -------------------------------------------------------------------------
    private Waypoint[] _waypointCache;

    private void Update()
    {
        if (myPathFollower != null)
            currentRoom = myPathFollower.CurrentNode;

        if (role == "IMPOSTER")
        {
            if (killTimer < killCooldown)
                killTimer = Mathf.Min(killTimer + Time.deltaTime, killCooldown);

            if (sabotageTimer < sabotageCooldown)
                sabotageTimer = Mathf.Min(sabotageTimer + Time.deltaTime, sabotageCooldown);
        }
    }

    // -------------------------------------------------------------------------
    // DO_TASK — begins the task at the current node.
    // short → 4s | common → 6s | long → 12s
    // -------------------------------------------------------------------------
    public void DO_TASK()
    {
        if (currentState == "DEAD")
        {
            Debug.LogWarning($"[{agentId}] DO_TASK: agent is dead.");
            return;
        }

        if (IsVenting)
        {
            Debug.LogWarning($"[{agentId}] DO_TASK: cannot do tasks while venting.");
            return;
        }

        if (currentRoom == null)
        {
            Debug.LogWarning($"[{agentId}] DO_TASK: currentRoom is null.");
            return;
        }

        float duration;
        if (tasks.shortTasks.Contains(currentRoom)) duration = 4f;
        else if (tasks.commonTasks.Contains(currentRoom)) duration = 6f;
        else if (tasks.longTasks.Contains(currentRoom)) duration = 12f;
        else
        {
            Debug.LogWarning($"[{agentId}] DO_TASK: '{currentRoom.name}' is not an assigned task.");
            return;
        }

        if (_taskCoroutine != null) StopTask();
        _taskCoroutine = StartCoroutine(TaskRoutine(currentRoom, duration));
    }

    private IEnumerator TaskRoutine(Waypoint task, float duration)
    {
        IsDoingTask = true;
        currentState = "DoingTask";
        Debug.Log($"[{agentId}] Doing task at '{task.name}' — {duration}s to complete.");

        yield return new WaitForSeconds(duration);

        tasks.CompleteTask(task);

        IsDoingTask = false;
        currentState = "Idle";
        _taskCoroutine = null;

        Debug.Log($"[{agentId}] Completed task at '{task.name}'.");
        // ← NEW
        sendInformation("completeTask", details: $"you have completed {task.name}");
    }

    // -------------------------------------------------------------------------
    // StopTask — cancels the active task without completing it.
    // -------------------------------------------------------------------------
    public void StopTask()
    {
        if (_taskCoroutine == null)
        {
            Debug.LogWarning($"[{agentId}] StopTask: no task is currently running.");
            return;
        }

        StopCoroutine(_taskCoroutine);
        _taskCoroutine = null;
        IsDoingTask = false;
        currentState = "Idle";

        Debug.Log($"[{agentId}] Task interrupted — not completed.");
    }

    // -------------------------------------------------------------------------
    // Proximity callbacks — called by LocationManager
    // -------------------------------------------------------------------------
    public void OnNearEnter(Amongi other)
    {
        if (other == null) return;

        if (role == "CREWMATE" &&
            SabotageManager.Instance != null &&
            SabotageManager.Instance.CurrentSabotage == SabotageType.Electrical)
        {
            return;
        }

        sendInformation($"near:{other.agentId}", details: $"you are near {other.agentId}");
    }

    public void OnClosestEnter(Amongi other)
    {
        if (other == null) return;

        if (role == "CREWMATE" &&
            SabotageManager.Instance != null &&
            SabotageManager.Instance.CurrentSabotage == SabotageType.Electrical)
        {
            return;
        }

        sendInformation($"closest:{other.agentId}", details: $"{other.agentId} is right next to you");
    }

    // -------------------------------------------------------------------------
    // Kill — marks target DEAD, stops their task, resets kill cooldown.
    // -------------------------------------------------------------------------
    private void KillInternal(Amongi target)
    {
        target.currentState = "DEAD";
        target.StopTask();

        killTimer = 0f;
        Debug.Log($"[{agentId}] (IMPOSTER) killed {target.agentId}!");
    }

    // -------------------------------------------------------------------------
    // VENT — teleports the imposter to a connected vent in the same network.
    // -------------------------------------------------------------------------
    public void VENT(Waypoint targetVent)
    {
        if (role != "IMPOSTER")
        {
            Debug.LogWarning($"[{agentId}] VENT: only IMPOSTERS can vent.");
            return;
        }

        if (currentState == "DEAD")
        {
            Debug.LogWarning($"[{agentId}] VENT: dead agents cannot vent.");
            return;
        }

        if (VentManager.Instance == null || !VentManager.Instance.IsVent(currentRoom))
        {
            Debug.LogWarning($"[{agentId}] VENT: '{currentRoom?.name}' is not a vent.");
            return;
        }

        if (targetVent == null)
        {
            Debug.LogWarning($"[{agentId}] VENT: targetVent is null.");
            return;
        }

        if (targetVent == currentRoom)
        {
            Debug.LogWarning($"[{agentId}] VENT: already at '{targetVent.name}'.");
            return;
        }

        if (!VentManager.Instance.AreConnected(currentRoom, targetVent))
        {
            Debug.LogWarning($"[{agentId}] VENT: '{currentRoom.name}' and '{targetVent.name}' are not connected.");
            return;
        }

        string origin = currentRoom.name;

        myPathFollower?.ClearPath();
        myPathFollower?.SnapToNode(targetVent);

        IsVenting = true;
        currentState = "Venting";

        Debug.Log($"[{agentId}] vented from '{origin}' → '{targetVent.name}'.");

        IsVenting = false;
        currentState = "Idle";

        sendInformation($"vent:{targetVent.name}", details: $"you have vented from {origin} to {targetVent.name}");
    }

    public List<Waypoint> GetAvailableVents()
    {
        if (role != "IMPOSTER") return null;
        if (VentManager.Instance == null) return null;
        if (!VentManager.Instance.IsVent(currentRoom)) return null;
        return VentManager.Instance.GetConnectedVents(currentRoom);
    }

    // -------------------------------------------------------------------------
    // Sabotage actions — IMPOSTER only
    // -------------------------------------------------------------------------
    public void ELECTRICAL_SABOTAGE()
    {
        if (!ValidateSabotage("ELECTRICAL")) return;
        SabotageManager.Instance.TriggerElectrical();
        sabotageTimer = 0f;

        BroadcastToAlive("sabotage:Electrical", "electrical sabotage has been triggered, lights are out");
    }

    public void REACTOR_SABOTAGE()
    {
        if (!ValidateSabotage("REACTOR")) return;
        SabotageManager.Instance.TriggerReactor();
        sabotageTimer = 0f;

        BroadcastToAlive("sabotage:Reactor", "reactor meltdown has been triggered, fix it before time runs out");
    }

    public void OXYGEN_SABOTAGE()
    {
        if (!ValidateSabotage("O2")) return;
        SabotageManager.Instance.TriggerOxygen();
        sabotageTimer = 0f;

        BroadcastToAlive("sabotage:O2", "oxygen sabotage has been triggered, fix it before time runs out");
    }

    private bool ValidateSabotage(string label)
    {
        if (role != "IMPOSTER")
        {
            Debug.LogWarning($"[{agentId}] {label}_SABOTAGE: only IMPOSTERS can sabotage.");
            return false;
        }

        if (!CanSabotage)
        {
            Debug.LogWarning($"[{agentId}] {label}_SABOTAGE: on cooldown ({sabotageTimer:F1}s / {sabotageCooldown}s).");
            return false;
        }

        if (SabotageManager.Instance == null)
        {
            Debug.LogWarning($"[{agentId}] {label}_SABOTAGE: SabotageManager.Instance is null.");
            return false;
        }

        if (SabotageManager.Instance.SabotageActive)
        {
            Debug.LogWarning($"[{agentId}] {label}_SABOTAGE: {SabotageManager.Instance.CurrentSabotage} already active.");
            return false;
        }

        return true;
    }

    // ← details parameter added so sabotage broadcasts carry context to every agent
    private void BroadcastToAlive(string reason, string details = null)
    {
        if (locationManager == null) return;
        foreach (Amongi a in locationManager.allAmongi)
        {
            if (a != null && a.currentState != "DEAD")
                a.sendInformation(reason, details: details);
        }
    }

    // -------------------------------------------------------------------------
    // REPORT — scans the current room for a dead body and calls a meeting.
    // -------------------------------------------------------------------------
    public void REPORT()
    {
        if (currentState == "DEAD")
        {
            Debug.LogWarning($"[{agentId}] REPORT: dead agents cannot report.");
            return;
        }

        if (MeetingManager.Instance == null) return;

        if (MeetingManager.Instance.MeetingActive)
        {
            Debug.LogWarning($"[{agentId}] REPORT: a meeting is already in progress.");
            return;
        }

        bool found = MeetingManager.Instance.TryReport(this);
        if (!found)
            Debug.LogWarning($"[{agentId}] REPORT: no dead body found in '{currentRoom?.name}'.");
    }

    // -------------------------------------------------------------------------
    // BUTTON — triggers an emergency meeting from the cafeteria.
    // -------------------------------------------------------------------------
    public void BUTTON()
    {
        if (currentState == "DEAD")
        {
            Debug.LogWarning($"[{agentId}] BUTTON: dead agents cannot press the button.");
            return;
        }

        if (MeetingManager.Instance == null) return;

        if (MeetingManager.Instance.MeetingActive)
        {
            Debug.LogWarning($"[{agentId}] BUTTON: a meeting is already in progress.");
            return;
        }

        bool pressed = MeetingManager.Instance.TryButton(this);
        if (!pressed)
            Debug.LogWarning($"[{agentId}] BUTTON: must be in Cafeteria to press the button.");
    }

    // -------------------------------------------------------------------------
    // Security & Admin — InformationManager queries
    // -------------------------------------------------------------------------
    public List<string> Security()
    {
        if (InformationManager.Instance == null) return null;
        List<string> visible = InformationManager.Instance.GetSecurityData(this);
        if (visible == null) return null;

        sendInformation("security", details: "you are viewing the security cameras");
        return visible;
    }

    public Dictionary<string, int> Admin()
    {
        if (InformationManager.Instance == null) return null;
        Dictionary<string, int> roomData = InformationManager.Instance.GetAdminData(this);
        if (roomData == null) return null;

        sendInformation("admin", details: "you are viewing the admin map");
        return roomData;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        Vector3 pos = transform.position;

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.12f);
        Gizmos.DrawSphere(pos, near_radius);
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.9f);
        Gizmos.DrawWireSphere(pos, near_radius);

        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.18f);
        Gizmos.DrawSphere(pos, closest_radius);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(pos, closest_radius);
    }

    // -------------------------------------------------------------------------
    // sendInformation — ENVELOPE MUST MATCH Python:
    // { "agent": "...", "event": {type,details,time}, "state": {location,sabotage,tasks,imposterInformation,availableActions} }
    // -------------------------------------------------------------------------
    // ← details param added; falls back to reason if omitted
    public string sendInformation(string reason, JObject extras = null, string details = null)
    {
        // -------- Event --------
        var eventObj = new JObject
        {
            ["type"] = reason,
            ["details"] = details ?? reason,   // ← uses custom details when provided
            ["time"] = locationManager != null ? (float)locationManager.time : 0f
        };

        // -------- State --------
        string loc = currentRoom != null ? currentRoom.name : "Unknown";

        // sabotage: dict[str,bool]
        var sabotageDict = new JObject();
        if (SabotageManager.Instance != null && SabotageManager.Instance.SabotageActive)
        {
            sabotageDict[SabotageManager.Instance.CurrentSabotage.ToString()] = true;
        }

        // tasks: list[{location,type,status}]
        var tasksList = new JArray();
        if (tasks != null)
        {
            foreach (var wp in tasks.commonTasks)
                if (wp != null) tasksList.Add(new JObject { ["location"] = wp.name, ["type"] = "common", ["status"] = "incomplete" });

            foreach (var wp in tasks.shortTasks)
                if (wp != null) tasksList.Add(new JObject { ["location"] = wp.name, ["type"] = "short", ["status"] = "incomplete" });

            foreach (var wp in tasks.longTasks)
                if (wp != null) tasksList.Add(new JObject { ["location"] = wp.name, ["type"] = "long", ["status"] = "incomplete" });
        }

        // imposterInformation: dict
        var imposterInformation = new JObject();
        if (role == "IMPOSTER" && locationManager != null)
        {
            int aliveCrewmates = 0;
            foreach (Amongi a in locationManager.allAmongi)
                if (a != null && a.role == "CREWMATE" && a.currentState != "DEAD") aliveCrewmates++;

            imposterInformation["aliveCrewmates"] = aliveCrewmates;
            imposterInformation["killCooldown"] = new JObject { ["current"] = killTimer, ["max"] = killCooldown, ["ready"] = CanKill };
            imposterInformation["sabotageCooldown"] = new JObject { ["current"] = sabotageTimer, ["max"] = sabotageCooldown, ["ready"] = CanSabotage };
            imposterInformation["isVenting"] = IsVenting;

            List<Waypoint> vents = GetAvailableVents();
            if (vents != null)
            {
                var ventNames = new JArray();
                foreach (var v in vents) if (v != null) ventNames.Add(v.name);
                imposterInformation["connectedVents"] = ventNames;
            }
        }

        // availableActions: list[str] (ActionType literals)
        var availableActions = new JArray();
        bool alive = currentState != "DEAD";
        bool noMeeting = (MeetingManager.Instance == null) || !MeetingManager.Instance.MeetingActive;

        if (alive && noMeeting && !IsVenting) availableActions.Add("Move");

        if (alive && noMeeting && !IsVenting && currentRoom != null &&
            (tasks.commonTasks.Contains(currentRoom) || tasks.shortTasks.Contains(currentRoom) || tasks.longTasks.Contains(currentRoom)))
            availableActions.Add("Task");

        if (alive && noMeeting && reason.StartsWith("found:", StringComparison.OrdinalIgnoreCase))
            availableActions.Add("Report");

        if (alive && noMeeting && MeetingManager.Instance != null && currentRoom == MeetingManager.Instance.cafeteriaNode)
            availableActions.Add("CallMeeting");

        if (InformationManager.Instance != null && currentRoom == InformationManager.Instance.securityNode)
            availableActions.Add("Security");

        if (InformationManager.Instance != null && currentRoom == InformationManager.Instance.adminNode)
            availableActions.Add("Admin");

        if (role == "IMPOSTER" && alive && CanKill && reason.StartsWith("near:", StringComparison.OrdinalIgnoreCase))
            availableActions.Add("Kill");

        if (role == "IMPOSTER" && alive)
            availableActions.Add("Vent");

        if (role == "IMPOSTER" && alive && CanSabotage && SabotageManager.Instance != null && !SabotageManager.Instance.SabotageActive)
            availableActions.Add("Sabotage");

        var stateObj = new JObject
        {
            ["location"] = loc,
            ["sabotage"] = sabotageDict,
            ["tasks"] = tasksList,
            ["imposterInformation"] = imposterInformation,
            ["availableActions"] = availableActions
        };

        if (extras != null)
        {
            foreach (var prop in extras.Properties())
                stateObj[prop.Name] = prop.Value;
        }

        var envelope = new JObject
        {
            ["agent"] = agentId,
            ["event"] = eventObj,
            ["state"] = stateObj
        };

        string json = envelope.ToString(Newtonsoft.Json.Formatting.None);
        Debug.Log($"[{agentId}] Briefing JSON:\n{json}");

        Connection.QueueEvent(json);
        return json;
    }

    // =========================================================================
    // ACTION DISPATCH — called by Connection.ApplyActionsCoroutine
    // =========================================================================
    public void DispatchAction(string type, string details)
    {
        string key = (type ?? "").Trim().ToLowerInvariant();

        switch (key)
        {
            case "move": Move(details); break;
            case "kill": Kill(); break;
            case "vent": Vent(details); break;
            case "report": Report(); break;
            case "callmeeting": CallMeeting(); break;
            case "security": Security(); break;
            case "admin": Admin(); break;
            case "sabotage": Sabotage(details); break;
            case "task": Task(); break;

            default:
                Debug.LogWarning($"[{agentId}] DispatchAction: unknown type '{type}'.");
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Action wrappers (names used by DispatchAction)
    // -------------------------------------------------------------------------
    public void Move(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning($"[{agentId}] Move: room name is null or empty.");
            return;
        }

        Waypoint target = FindWaypoint(roomName);
        if (target == null)
        {
            Debug.LogWarning($"[{agentId}] Move: waypoint '{roomName}' not found.");
            return;
        }

        if (IsDoingTask) StopTask();

        myPathFollower?.MoveTo(target);
        currentState = "Moving";
    }

    public void Kill()
    {
        if (role != "IMPOSTER")
        {
            Debug.LogWarning($"[{agentId}] Kill: only IMPOSTERS can kill.");
            return;
        }

        if (!CanKill)
        {
            Debug.LogWarning($"[{agentId}] Kill: on cooldown ({killTimer:F1}s / {killCooldown}s).");
            return;
        }

        if (locationManager == null)
        {
            Debug.LogWarning($"[{agentId}] Kill: locationManager is null.");
            return;
        }

        Amongi target = null;
        float closest = float.MaxValue;

        foreach (Amongi a in locationManager.allAmongi)
        {
            if (a == null || a == this) continue;
            if (a.role != "CREWMATE" || a.currentState == "DEAD") continue;

            float dist = Vector3.Distance(transform.position, a.transform.position);
            if (dist <= near_radius && dist < closest)
            {
                closest = dist;
                target = a;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[{agentId}] Kill: no crewmate within range ({near_radius}u).");
            return;
        }

        KillInternal(target);
    }

    public void Vent(string ventName)
    {
        if (string.IsNullOrEmpty(ventName))
        {
            Debug.LogWarning($"[{agentId}] Vent: vent name is null or empty.");
            return;
        }

        Waypoint target = FindWaypoint(ventName);
        if (target == null)
        {
            Debug.LogWarning($"[{agentId}] Vent: waypoint '{ventName}' not found.");
            return;
        }

        VENT(target);
    }

    public void Report() => REPORT();
    public void CallMeeting() => BUTTON();

    public void Sabotage(string type)
    {
        string key = (type ?? "").Trim().ToUpperInvariant();
        switch (key)
        {
            case "ELECTRICAL": ELECTRICAL_SABOTAGE(); break;
            case "REACTOR": REACTOR_SABOTAGE(); break;
            case "O2": OXYGEN_SABOTAGE(); break;
            default:
                Debug.LogWarning($"[{agentId}] Sabotage: unknown type '{type}'. Use Electrical / Reactor / O2.");
                break;
        }
    }

    public void Task() => DO_TASK();

    private Waypoint FindWaypoint(string roomName)
    {
        if (_waypointCache == null || _waypointCache.Length == 0)
            _waypointCache = FindObjectsOfType<Waypoint>();

        foreach (Waypoint wp in _waypointCache)
            if (wp != null && wp.name == roomName)
                return wp;

        return null;
    }
}
