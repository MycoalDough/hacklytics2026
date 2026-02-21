using UnityEngine;
using 

public class Amongi : MonoBehaviour
{
    public string agentId = "Red";

    void Start()
    {
        Connection.RegisterAgent(agentId, HandleAction);
        RequestAction(); // ask for first action immediately
    }

    public string BuildObservation()
    {
        return null;
    }

    void RequestAction()
    {
        string obs = BuildObservation(); // your JSON snapshot
        Connection.SendData(agentId, obs);
    }

    void HandleAction(string action)
    {
        // action is whatever Python sends back, e.g. "move:WP_MedBay" or "kill:Blue"
        StartCoroutine(ExecuteAction(action));
    }

    IEnumerator ExecuteAction(string action)
    {
        // parse and run the action...
        yield return new WaitForSeconds(0.5f);
        RequestAction(); // ask for next action when done
    }
}
