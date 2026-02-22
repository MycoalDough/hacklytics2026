using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 

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

    [Header("UI")]                               
    public Text sabotageText;                    

    public SabotageType CurrentSabotage { get; private set; } = SabotageType.None;
    public bool SabotageActive => CurrentSabotage != SabotageType.None;
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

    private void Update()
    {
        if (sabotageText == null) return;

        if (!SabotageActive)
        {
            sabotageText.text = "CURRENT SABOTAGE: NONE";
        }
        else if (TimeRemaining >= 0f)
        {
            sabotageText.text = $"CURRENT SABOTAGE: {CurrentSabotage.ToString().ToUpper()}\n{TimeRemaining:F1}s";
        }
        else
        {
            sabotageText.text = $"CURRENT SABOTAGE: {CurrentSabotage.ToString().ToUpper()}";
        }
    }
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
    }
}
