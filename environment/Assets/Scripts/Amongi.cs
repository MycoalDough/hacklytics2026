using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public class Amongi : MonoBehaviour
{
    public string agentId = "Red";
    public string currentState = "Idle";

    [Header("Role")]
    public string role = "CREWMATE"; // "CREWMATE" or "IMPOSTER"
    public Sprite dead;

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

    private Coroutine _taskCoroutine;
    public bool IsDoingTask { get; private set; } = false;
    public bool IsVenting { get; private set; } = false;

    private Waypoint[] _waypointCache;

    private bool _killCooldownReady = false;
    private bool _sabotageCooldownReady = false;

    private void Update()
    {
        if (myPathFollower != null)
            currentRoom = myPathFollower.CurrentNode;

        if (role == "IMPOSTER")
        {
            tasks = null;

            bool prevKillReady = _killCooldownReady;
            bool prevSabReady = _sabotageCooldownReady;

            if (killTimer < killCooldown)
                killTimer = Mathf.Min(killTimer + Time.deltaTime, killCooldown);

            if (sabotageTimer < sabotageCooldown)
                sabotageTimer = Mathf.Min(sabotageTimer + Time.deltaTime, sabotageCooldown);

            _killCooldownReady = CanKill;
            if (_killCooldownReady && !prevKillReady)
                sendInformation("killCooldownEnd", details: "your kill cooldown has ended — you can kill again");

            _sabotageCooldownReady = CanSabotage;
            if (_sabotageCooldownReady && !prevSabReady)
                sendInformation("sabotageCooldownEnd", details: "your sabotage cooldown has ended — you can sabotage again");
        }

        if (currentState == "DEAD")
        {
            GetComponent<SpriteRenderer>().sprite = dead;
            myPathFollower.speed = 0f;
        }
    }


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
        sendInformation("completeTask", details: $"you have completed {task.name}");
    }

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

    public void OnNearEnter(Amongi other)
    {
        if (other == null) return;
        if (currentState == "DEAD") return;
        if (role == "CREWMATE" &&
            SabotageManager.Instance != null &&
            SabotageManager.Instance.CurrentSabotage == SabotageType.Electrical)
            return;

        if (other.currentState == "DEAD")
        {
            sendInformation($"seeBody:{other.agentId}", details: $"you see the dead body of {other.agentId}");
            return;
        }

        sendInformation($"seePlayer:{other.agentId}", details: $"you are near {other.agentId}");

        if (role == "IMPOSTER" && other.role == "CREWMATE" && CanKill)
            sendInformation($"killRange:{other.agentId}", details: $"{other.agentId} is within your kill range");
    }

    public void OnClosestEnter(Amongi other)
    {
        if (other == null) return;
        if (currentState == "DEAD") return;

        if (role == "CREWMATE" &&
            SabotageManager.Instance != null &&
            SabotageManager.Instance.CurrentSabotage == SabotageType.Electrical)
            return;

        if (other.currentState == "DEAD")
        {
            sendInformation($"seeBody:{other.agentId}", details: $"the dead body of {other.agentId} is right next to you");
            return;
        }

        sendInformation($"seePlayer:{other.agentId}", details: $"{other.agentId} is right next to you");

        if (role == "IMPOSTER" && other.role == "CREWMATE" && CanKill)
            sendInformation($"killRange:{other.agentId}", details: $"{other.agentId} is right next to you and within kill range");
    }

    public void OnNearExit(Amongi other)
    {
        if (other == null) return;
        if (currentState == "DEAD") return;

        if (role == "CREWMATE" &&
            SabotageManager.Instance != null &&
            SabotageManager.Instance.CurrentSabotage == SabotageType.Electrical)
            return;

        if (other.currentState == "DEAD")
        {
            sendInformation($"seeBody:{other.agentId}", details: $"the dead body of {other.agentId} is right next to you");
            return;
        }
        sendInformation($"seePlayerEnd:{other.agentId}", details: $"{other.agentId} is no longer near you");

        if (role == "IMPOSTER" && other.role == "CREWMATE")
            sendInformation($"killRangeEnd:{other.agentId}", details: $"{other.agentId} has left your kill range");
    }

    public void OnClosestExit(Amongi other) { }

    private void KillInternal(Amongi target)
    {
        target.currentState = "DEAD";
        target.StopTask();

        NotifyKillWitnesses(target);

        killTimer = 0f;
        _killCooldownReady = false;
        Debug.Log($"[{agentId}] (IMPOSTER) killed {target.agentId}!");
        sendInformation("completeKill", details: $"you have killed {target.agentId}");
    }

    private void NotifyKillWitnesses(Amongi victim)
    {
        if (locationManager == null || victim == null) return;

        bool electricalActive =
            SabotageManager.Instance != null &&
            SabotageManager.Instance.CurrentSabotage == SabotageType.Electrical;

        if (electricalActive) return;

        Vector3 killPos = transform.position;
        string killerId = agentId;
        string victimId = victim.agentId;
        string roomName = currentRoom != null ? currentRoom.name : "Unknown";

        foreach (Amongi witness in locationManager.allAmongi)
        {
            if (witness == null) continue;
            if (witness == this || witness == victim) continue;
            if (witness.currentState == "DEAD") continue;
            if (witness.role != "CREWMATE") continue;

            if (currentRoom != null && witness.currentRoom != currentRoom) continue;


            var extras = new JObject
            {
                ["killer"] = killerId,
                ["victim"] = victimId,
                ["room"] = roomName
            };

            witness.sendInformation(
                "seeKill",
                details: $"you saw {killerId} kill {victimId}"
            );
        }
    }

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
        NotifyNearbyAgents(transform.position, $"seeEnterVent", $"{agentId} was seen entering a vent at {origin}");
        myPathFollower?.ClearPath();
        myPathFollower?.SnapToNode(targetVent);
        IsVenting = true;
        currentState = "Venting";
        NotifyNearbyAgents(transform.position, $"seeExitVent", $"{agentId} was seen exiting a vent at {targetVent.name}");
        IsVenting = false;
        currentState = "Idle";
        currentRoom = targetVent;

        Debug.Log($"[{agentId}] vented from '{origin}' → '{targetVent.name}'.");
        sendInformation($"vent:", details: $"you have vented from {origin} to {targetVent.name}");
    }

    private void NotifyNearbyAgents(Vector3 origin, string reason, string details)
    {
        if (locationManager == null) return;
        foreach (Amongi a in locationManager.allAmongi)
        {
            if (a == null || a == this || a.currentState == "DEAD") continue;
            if (Vector3.Distance(origin, a.transform.position) <= a.near_radius)
                a.sendInformation(reason, details: details);
        }
    }

    public List<Waypoint> GetAvailableVents()
    {
        if (role != "IMPOSTER") return null;
        if (VentManager.Instance == null) return null;
        if (!VentManager.Instance.IsVent(currentRoom)) return null;
        return VentManager.Instance.GetConnectedVents(currentRoom);
    }

    public void ELECTRICAL_SABOTAGE()
    {
        if (!ValidateSabotage("ELECTRICAL")) return;
        SabotageManager.Instance.TriggerElectrical();
        sabotageTimer = 0f;
        _sabotageCooldownReady = false;
        BroadcastToAlive("sabotage", "electrical sabotage has been triggered, lights are out");
    }

    public void REACTOR_SABOTAGE()
    {
        if (!ValidateSabotage("REACTOR")) return;
        SabotageManager.Instance.TriggerReactor();
        sabotageTimer = 0f;
        _sabotageCooldownReady = false;
        BroadcastToAlive("sabotage", "reactor meltdown has been triggered, fix it before time runs out");
    }

    public void OXYGEN_SABOTAGE()
    {
        if (!ValidateSabotage("O2")) return;
        SabotageManager.Instance.TriggerOxygen();
        sabotageTimer = 0f;
        _sabotageCooldownReady = false;
        BroadcastToAlive("sabotage", "oxygen sabotage has been triggered, fix it before time runs out");
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

    private void BroadcastToAlive(string reason, string details = null)
    {
        if (locationManager == null) return;
        foreach (Amongi a in locationManager.allAmongi)
        {
            if (a != null && a.currentState != "DEAD")
                a.sendInformation(reason, details: details);
        }
    }

    public void OnSabotageResolved(string sabotageType)
    {
        sendInformation($"sabotageEnd", details: $"the {sabotageType} sabotage has been resolved");
    }

    private string GetAliveList()
    {
        if (locationManager == null) return "unknown";
        var alive = locationManager.allAmongi
            .Where(a => a != null && a.currentState != "DEAD")
            .Select(a => a.agentId);
        return string.Join(", ", alive);
    }

    public void REPORT()
    {
        if (currentState == "DEAD")
        {
            Debug.LogWarning($"[{agentId}] REPORT: dead agents cannot report.");
            return;
        }

        if (MeetingManager.Instance == null) return;

        bool found = MeetingManager.Instance.TryReport(this);
        if (found)
        {
            GameManager.instance.HandleReport();
            Connection.ClearQueue();                         
            var bodyFoundDetails = new JObject
            {
                ["caller"] = agentId,
                ["body"] = GetDeadBodyInRoom(),
                ["alivePlayers"] = GetAliveArray()
            };
            BroadcastToAlive("bodyFound", bodyFoundDetails.ToString(Newtonsoft.Json.Formatting.None));
            //Connection.FlushEvents();
        }
        else
            Debug.LogWarning($"[{agentId}] REPORT: no dead body found in '{currentRoom?.name}'.");
    }

    private JArray GetAliveArray()
    {
        var arr = new JArray();
        if (locationManager == null) return arr;
        foreach (Amongi a in locationManager.allAmongi)
            if (a != null && a.currentState != "DEAD")
                arr.Add(a.agentId);
        return arr;
    }

    private string GetDeadBodyInRoom()
    {
        if (locationManager == null || currentRoom == null) return "unknown";
        foreach (Amongi a in locationManager.allAmongi)
            if (a != null && a != this && a.currentState == "DEAD" && a.currentRoom == currentRoom)
                return a.agentId;
        return "unknown";
    }

    public void BUTTON()
    {
        if (currentState == "DEAD")
        {
            Debug.LogWarning($"[{agentId}] BUTTON: dead agents cannot press the button.");
            return;
        }

        if (MeetingManager.Instance == null) return;

        bool pressed = MeetingManager.Instance.TryButton(this);
        if (pressed)
        {
            GameManager.instance.HandleMeeting();
            Connection.ClearQueue();                         
            var meetingDetails = new JObject
            {
                ["caller"] = agentId,
                ["alivePlayers"] = GetAliveArray()
            };
            BroadcastToAlive("emergencyMeeting", meetingDetails.ToString(Newtonsoft.Json.Formatting.None));
            //Connection.FlushEvents();
        }
        else
            Debug.LogWarning($"[{agentId}] BUTTON: must be in Cafeteria to press the button.");
    }

    public string Security()
    {
        if (InformationManager.Instance == null) return null;
        String visible = InformationManager.Instance.GetSecurityData(this);
        if (visible == null) return null;
        sendInformation("security", details: "you are viewing the security cameras! " + visible);
        return visible;
    }

    public string Admin()
    {
        if (InformationManager.Instance == null) return null;
        String roomData = InformationManager.Instance.GetAdminData(this);
        if (roomData == null) return null;
        sendInformation("admin", details: "you are viewing the admin map." + roomData);
        return roomData;
    }

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

    public string sendInformation(string reason, JObject extras = null, string details = null)
    {
        bool electricalActive = SabotageManager.Instance != null &&
                                SabotageManager.Instance.CurrentSabotage == SabotageType.Electrical;

        string resolvedDetails = details ?? reason;

        if (!(role == "CREWMATE" && electricalActive))
        {
            List<string> roommates = GetAgentsInSameRoom();
            bool isJson = resolvedDetails.TrimStart().StartsWith("{");

            if (isJson)
            {
                try
                {
                    var jObj = JObject.Parse(resolvedDetails);
                    var roommateArr = new JArray();
                    foreach (var r in roommates) roommateArr.Add(r);
                    jObj["alsoInRoom"] = roommateArr;
                    resolvedDetails = jObj.ToString(Newtonsoft.Json.Formatting.None);
                }
                catch
                {
                }
            }
            else
            {
                resolvedDetails += roommates.Count > 0
                    ? $"; also in this room: {string.Join(", ", roommates)}"
                    : "; you are alone in this room";
            }
        }

        var eventObj = new JObject
        {
            ["type"] = reason,
            ["details"] = resolvedDetails,
            ["time"] = locationManager != null ? (float)locationManager.time : 0f
        };

        string loc = currentRoom != null ? currentRoom.name : "Unknown";

        var sabotageDict = new JObject();
        if (SabotageManager.Instance != null && SabotageManager.Instance.SabotageActive)
            sabotageDict[SabotageManager.Instance.CurrentSabotage.ToString()] = true;

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

        bool alive = currentState != "DEAD";

        var availableActions = new JArray();

        if (alive && !IsVenting)
            availableActions.Add("Move");

        if (alive && !IsVenting && currentRoom != null && tasks != null &&
            (tasks.commonTasks.Contains(currentRoom) || tasks.shortTasks.Contains(currentRoom) || tasks.longTasks.Contains(currentRoom)))
            availableActions.Add("Task");

        if (alive &&
            (reason.Contains("seeBody", StringComparison.OrdinalIgnoreCase) ||
             reason.Equals("seeKill", StringComparison.OrdinalIgnoreCase)))
            availableActions.Add("Report");

        if (alive && MeetingManager.Instance != null && currentRoom == MeetingManager.Instance.cafeteriaNode)
            availableActions.Add("CallMeeting");

        if (InformationManager.Instance != null && currentRoom == InformationManager.Instance.securityNode)
            availableActions.Add("Security");

        if (InformationManager.Instance != null && currentRoom == InformationManager.Instance.adminNode)
            availableActions.Add("Admin");

        if (role == "IMPOSTER" && alive && CanKill &&
            (reason.StartsWith("killRange:", StringComparison.OrdinalIgnoreCase) ||
             reason.StartsWith("seePlayer:", StringComparison.OrdinalIgnoreCase)))
            availableActions.Add("Kill");

        if (role == "IMPOSTER" && alive && VentManager.Instance != null)
        {
            List<Waypoint> connected = VentManager.Instance.GetConnectedVents(currentRoom);
            if (connected != null && connected.Count > 0)
                availableActions.Add("Vent");
        }

        if (role == "IMPOSTER" && alive && CanSabotage &&
            SabotageManager.Instance != null && !SabotageManager.Instance.SabotageActive)
            availableActions.Add("Sabotage");

        var stateObj = new JObject
        {
            ["location"] = loc,
            ["sabotage"] = sabotageDict,
            ["tasks"] = tasksList,
            ["imposterInformation"] = imposterInformation,
            ["availableActions"] = availableActions,
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

        if (reason.Equals("meetingEnd", StringComparison.OrdinalIgnoreCase))
            Connection.QueuePriorityEvent(json);
        else
            Connection.QueueEvent(json);
        return json;
    }

    private List<string> GetAgentsInSameRoom()
    {
        var result = new List<string>();
        if (currentRoom == null || locationManager == null) return result;
        foreach (Amongi a in locationManager.allAmongi)
        {
            if (a == null || a == this) continue;
            if (a.currentRoom == currentRoom)
                result.Add(a.agentId);
        }
        return result;
    }

    public void DispatchAction(string type, string details)
    {
        string key = (type ?? "").Trim().ToLowerInvariant();

        switch (key)
        {
            case "chat":
                GameManager.instance.HandleChat(agentId, details);
                break;
            case "vote":
                GameManager.instance.HandleVote(details); 
                break;
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
            if (dist <= near_radius + 3f && dist < closest)
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
