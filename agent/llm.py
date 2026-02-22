from typing import Callable, Literal
from agent.constants import ALL_VENTS, LOCATION_GRAPH, Action


def getFastestPath(start: str, end: str) -> list[str]:
    """
    Get the fastest path between two locations on the map.

    Args:
      start: The starting location (room or hallway).
      end: The destination location (room or hallway).

    Returns:
      a list of locations representing the path from start to end.
    """

    visited = set()
    queue = [[start]]

    if start == end:
        return [start]

    while queue:
        path = queue.pop(0)
        current = path[-1]

        if current in visited:
            continue

        visited.add(current)

        neighbors = LOCATION_GRAPH.get(current, set())
        for neighbor in neighbors:
            if neighbor == end:
                return path + [neighbor]
            if neighbor not in visited:
                queue.append(path + [neighbor])

    return []


def findClosestVent(location: str) -> tuple[str, int]:
    """
    Find the closest vent to a given location and how far it is.

    Args:
      location: The current location (room or hallway).

    Returns:
      the name of the closest vent and the distance to it.
    """
    visited = set()
    queue = [(location, 0)]

    while queue:
        current, distance = queue.pop(0)
        if current in visited:
            continue
        visited.add(current)
        if current in ALL_VENTS:
            return (current, distance)
        neighbors = LOCATION_GRAPH.get(current, set())
        for neighbor in neighbors:
            if neighbor not in visited:
                queue.append((neighbor, distance + 1))

    return ("", -1)


def think(new_thought: str):
    """
    Think about something. This doesn't directly affect the game state, but this will update your internal thoughts.
    Args:
      new_thought: The updated thought. You should format your thought as: "Current Priority: <what your current priority is, e.g. 'gathering information', 'completing tasks', 'sabotaging', etc.>. Reasoning: <your reasoning for this priority>. Next Steps: <what your next steps are to accomplish this priority, e.g. 'move to electrical to complete tasks', 'move to cafeteria to find a victim to kill'>. Additional Notes: <any additional notes you have, eg. 'I think Blue is suspicious because they were near the body and didn't report it'>"
    """
    return


def continue_current_action():
    """
    Continue doing the current action.
    """
    return


information_tools = [think]


def Move(
    to: Literal[
        "Cafeteria",
        "Weapons",
        "O2",
        "Navigation",
        "Shields",
        "Communications",
        "Storage",
        "Electrical",
        "Lower Engine",
        "Reactor",
        "Security",
        "Upper Engine",
        "MedBay",
        "Admin",
        "Hallway A",
        "Hallway B",
        "Hallway C",
        "Hallway D",
        "Hallway E",
        "Hallway F",
        "Hallway G",
    ],
) -> Action:
    """
    Move to a specified location.
    Args:
      to: The location to move to.
    """
    return Action(type="Move", details=to)


def Report() -> Action:
    """
    Reports the closest body.
    """
    return Action(type="Report", details="")


def CallMeeting() -> Action:
    """
    Call an emergency meeting.
    """
    return Action(type="CallMeeting", details="")


def Sabotage(system: Literal["Electrical", "O2", "Reactor"]) -> Action:
    """
    Sabotage a system.
    Args:
      system: The system to sabotage.
    """
    return Action(type="Sabotage", details=system)


def Kill() -> Action:
    """
    Kills the closest crewmate.
    """
    return Action(type="Kill", details="")


def Vent(vent: str) -> Action:
    """
    Vent into the specified vent. This teleports you to the requested vent. Other people can see you do this.
    Args:
      vent: The vent to go to.
    """
    return Action(type="Vent", details=vent)


def Security() -> Action:
    """
    Check security cameras.
    """
    return Action(type="Security", details="")


def Admin() -> Action:
    """
    Check admin map.
    """
    return Action(type="Admin", details="")


def Task() -> Action:
    """
    Perform a task.
    """
    return Action(type="Task", details="")


ACTION_MAP: dict[str, Callable] = {
    "Move": Move,
    "Report": Report,
    "CallMeeting": CallMeeting,
    "Sabotage": Sabotage,
    "Kill": Kill,
    "Vent": Vent,
    "Security": Security,
    "Admin": Admin,
    "Task": Task,
}
