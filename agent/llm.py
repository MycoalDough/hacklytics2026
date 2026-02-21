from ollama import chat
from ollama import ChatResponse
from constants import ALL_VENTS, LOCATION_GRAPH, Action, ActionType


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


information_tools = [getFastestPath, findClosestVent]


def move(to: str) -> Action:
    """
    Move to a specified location.
    Args:
      to: The location to move to.
    """
    return Action(type="move", details=to)


def report(to: str) -> Action:
    """
    Report a body.
    Args:
      to: The location where the body was found.
    """
    return Action(type="report", details=to)


response: ChatResponse = chat(
    model="gemma3",
    messages=[
        {
            "role": "user",
            "content": "Why is the sky blue?",
        },
    ],
    tools=information_tools,
)
print(response["message"]["content"])
# or access fields directly from the response object
print(response.message.content)
