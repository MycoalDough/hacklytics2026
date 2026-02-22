// SabotageManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SabotageType { None, Electrical, Reactor, Oxygen }

public class SabotageManager : MonoBehaviour
{
    public static SabotageManager Instance { get; private set; }

    [Header("Sabotage Nodes")]
    public Waypoint electricalNode;
    public Waypoint reactorNode;
    public Waypoint oxygenNode;
    public Waypoint adminNode;

    [Header("Settings")]
    public float sabotageDuration = 30f;

    // Current active sabotage — SabotageType.None means no sabotage is running
    public SabotageType CurrentSabotage { get; private set; } = SabotageType.None;
    public bool SabotageActive => CurrentSabotage != SabotageType.None;

    // Countdown for timed sabotages (Reactor / Oxygen). -1 when not applicable.
    public float TimeRemaining { get; private set; } = -1f;

    private LocationManager _locationManager;
    private Coroutine _sabotageCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _locationManager = FindFirstObjectByType<LocationManager>();
        if (_locationManager == null)
            Debug.LogError("[SabotageManager] No LocationManager found in scene.");
    }

    // -------------------------------------------------------------------------
    // Public triggers — called from Amongi
    // -------------------------------------------------------------------------
    public void TriggerElectrical()
    {
        if (!BeginSabotage(SabotageType.Electrical)) return;
        _sabotageCoroutine = StartCoroutine(ElectricalRoutine());
    }

    public void TriggerReactor()
    {
        if (!BeginSabotage(SabotageType.Reactor)) return;
        _sabotageCoroutine = StartCoroutine(ReactorRoutine());
    }

    public void TriggerOxygen()
    {
        if (!BeginSabotage(SabotageType.Oxygen)) return;
        _sabotageCoroutine = StartCoroutine(OxygenRoutine());
    }

    // -------------------------------------------------------------------------
    // Electrical — no countdown.
    // Crewmate near-vision stays disabled until any live crewmate enters
    // the electrical room.
    // -------------------------------------------------------------------------
    private IEnumerator ElectricalRoutine()
    {
        Debug.Log("[SabotageManager] ELECTRICAL sabotaged — crewmate near-vision disabled!");
        TimeRemaining = -1f;

        yield return new WaitUntil(() =>
        {
            foreach (Amongi a in _locationManager.allAmongi)
                if (a.role == "CREWMATE" && a.currentState != "DEAD" && a.currentRoom == electricalNode)
                    return true;
            return false;
        });

        ResolveSabotage("ELECTRICAL fixed — lights restored.");
    }

    // -------------------------------------------------------------------------
    // Reactor — 30s countdown.
    // Requires 2 live crewmates to be in the reactor room simultaneously.
    // -------------------------------------------------------------------------
    private IEnumerator ReactorRoutine()
    {
        Debug.Log("[SabotageManager] REACTOR meltdown! 30s — need 2 crewmates at Reactor.");
        TimeRemaining = sabotageDuration;

        while (TimeRemaining > 0f)
        {
            int count = 0;
            foreach (Amongi a in _locationManager.allAmongi)
                if (a.role == "CREWMATE" && a.currentState != "DEAD" && a.currentRoom == reactorNode)
                    count++;

            if (count >= 2) { ResolveSabotage("REACTOR fixed!"); yield break; }

            TimeRemaining -= Time.deltaTime;
            yield return null;
        }

        ImposterWin("Reactor meltdown not stopped — Imposters win!");
    }

    // -------------------------------------------------------------------------
    // Oxygen — 30s countdown.
    // Requires 1 live crewmate in oxygenNode AND 1 in adminNode simultaneously.
    // -------------------------------------------------------------------------
    private IEnumerator OxygenRoutine()
    {
        Debug.Log("[SabotageManager] OXYGEN depleted! 30s — need 1 in Oxygen + 1 in Admin.");
        TimeRemaining = sabotageDuration;

        while (TimeRemaining > 0f)
        {
            bool adminCovered = false;
            bool oxygenCovered = false;

            foreach (Amongi a in _locationManager.allAmongi)
            {
                if (a.role != "CREWMATE" || a.currentState == "DEAD") continue;
                if (a.currentRoom == adminNode) adminCovered = true;
                if (a.currentRoom == oxygenNode) oxygenCovered = true;
            }

            if (adminCovered && oxygenCovered) { ResolveSabotage("OXYGEN restored!"); yield break; }

            TimeRemaining -= Time.deltaTime;
            yield return null;
        }

        ImposterWin("Oxygen ran out — Imposters win!");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private bool BeginSabotage(SabotageType type)
    {
        if (SabotageActive)
        {
            Debug.LogWarning($"[SabotageManager] Cannot trigger {type} — {CurrentSabotage} already active.");
            return false;
        }
        CurrentSabotage = type;
        return true;
    }

    private void ResolveSabotage(string message)
    {
        Debug.Log($"[SabotageManager] {message}");
        CurrentSabotage = SabotageType.None;
        TimeRemaining = -1f;
        _sabotageCoroutine = null;
    }

    private void ImposterWin(string message)
    {
        Debug.Log($"[SabotageManager] {message}");
        CurrentSabotage = SabotageType.None;
        TimeRemaining = -1f;
        _sabotageCoroutine = null;

        // TODO: Hook your GameManager here, e.g.:
        // GameManager.Instance?.ImposterWin();
    }
}
