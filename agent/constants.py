from json import dumps
from typing import Literal


def enumerate_with_numbers(items: list[str]) -> str:
    return "\n".join(f"{i+1}. {item}" for i, item in enumerate(items))


class Task:
    location: str
    type: Literal["short", "common", "long"]
    status: Literal["incomplete", "complete"] | None = None

    def __init__(
        self,
        location: str,
        type: Literal["short", "common", "long"],
        status: Literal["incomplete", "complete"] | None = None,
    ):
        self.location = location
        self.type = type
        self.status = status

    def __str__(self):
        text = f"{self.location} - {self.type}"
        if self.status:
            text += f" ({self.status})"
        return text


ROOMS = [
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
]

HALLWAYS = [
    "Hallway A",
    "Hallway B",
    "Hallway C",
    "Hallway D",
    "Hallway E",
    "Hallway F",
    "Hallway G",
]

VENTS = [
    {"Upper Engine", "Reactor"},
    {"Cafeteria", "Hallway E", "Admin"},
    {"MedBay", "Security", "Electrical"},
    {"Reactor", "Lower Engine"},
    {"Navigation", "Shields"},
]

ALL_VENTS = set()
for vent in VENTS:
    ALL_VENTS.update(vent)

PLAYERS = ["Red", "Yellow", "Green", "Blue", "Purple", "Pink"]

LOCATION_GRAPH = {}


def load_location_graph():
    LOCATION_GRAPH["Hallway A"] = {"Upper Engine", "MedBay", "Cafeteria"}
    LOCATION_GRAPH["Hallway B"] = {"Cafeteria", "Weapons"}
    LOCATION_GRAPH["Hallway C"] = {
        "Upper Engine",
        "Reactor",
        "Security",
        "Lower Engine",
    }
    LOCATION_GRAPH["Hallway D"] = {"Cafeteria", "Admin", "Storage"}
    LOCATION_GRAPH["Hallway E"] = {"Weapons", "O2", "Navigation", "Shields"}
    LOCATION_GRAPH["Hallway F"] = {"Lower Engine", "Electrical", "Storage"}
    LOCATION_GRAPH["Hallway G"] = {"Storage", "Communications", "Shields"}

    for hallway, rooms in LOCATION_GRAPH.items():
        for room in rooms:
            LOCATION_GRAPH[room].add(hallway)


load_location_graph()


tasks: list[Task] = [
    Task(location="ELECTRICAL", type="common"),
    Task(location="LOWER ENGINE", type="common"),
    Task(location="STORAGE", type="common"),
    Task(location="SHIELDS", type="common"),
    Task(location="NAVIGATION", type="common"),
    Task(location="OXYGEN", type="common"),
    Task(location="SECURITY", type="common"),
    Task(location="CAFETERIA", type="short"),
    Task(location="UPPER ENGINE", type="short"),
    Task(location="LOWER ENGINE", type="short"),
    Task(location="ELECTRICAL", type="short"),
    Task(location="REACTOR", type="short"),
    Task(location="SHIELDS", type="short"),
    Task(location="NAVIGATION", type="short"),
    Task(location="MEDBAY", type="long"),
    Task(location="REACTOR", type="long"),
    Task(location="STORAGE", type="long"),
    Task(location="COMMUNICATIONS", type="long"),
    Task(location="WEAPONS", type="long"),
]


INFORMATION = {
    "Rooms": enumerate_with_numbers(ROOMS),
    "Hallways": """
A: Upper Engine (left) - MedBay (down) - Cafeteria (right)
B: Cafeteria (left) - Weapons (right)
C: Upper Engine (up) - Reactor (left) - Security (right) - Lower Engine (down)
D: Cafeteria (up) - Admin (right) - Storage (down)
E: Weapons (up) - O2 (left) - Navigation (right) - Shields (down)
F: Lower Engine (left) - Electrical (up) - Storage (right)
G: Storage (left) - Communications (down) - Shields (right)
""".strip(),
    "Vents": enumerate_with_numbers([" - ".join(vent) for vent in VENTS]),
    "Tasks": enumerate_with_numbers([str(task) for task in tasks]),
    "Players": enumerate_with_numbers(PLAYERS),
    "Sabotages": """
1. Electrical - Imposter retains full vision, but crewmates only see the area around them. Requires a player to go to Electrical to fix.
2. O2 - Crewmates have 30 seconds to fix the O2 sabotage, or they lose the game. Requires a player to go to O2 and another player to go to Admin to fix.
3. Reactor - Crewmates have 30 seconds to fix the Reactor sabotage, or they lose the game. Requires 2 players to go to Reactor to fix.
""",
}

BASE_SYSTEM_MESSAGE = f"""
You are an agent playing a variant of the game Among Us.
The game takes place on a spaceship with 14 rooms, connected by hallways and vents.
There are 6 players on the spaceship, each with a unique color.
Each player is either a crewmate or an imposter.
The crewmates' goal is to complete tasks around the spaceship, while the imposters' goal is to kill the crewmates without being caught.
Short tasks take 4 seconds, common tasks take 6 seconds, and long tasks take 12 seconds.
Imposters can also sabotage the spaceship to make it harder for the crewmates to complete their tasks and to create opportunities to kill crewmates.
Entering a room where a sabotage fix can be done instantly fixes the sabotage if all the players are in the required rooms.
Imposters have a kill cooldown of 30 seconds and can only kill crewmates in range.
Imposters can also vent to quickly move around the spaceship and hide, but they can be spotted venting.

Here is some information about the game:
{"\n\n".join(f"## {key}:\n{value}" for key, value in INFORMATION.items())}

"""

Role = Literal["crewmate", "imposter"]

EventType = Literal[
    "seePlayer",
    "seePlayerEnd",
    "seeBody",
    "seeEnterVent",
    "seeExitVent",
    "completeTask",
    "sabotage",
    "sabotageEnd",
    "killRange",
    "killRangeEnd",
    "bodyFound",
    "emergencyMeeting",
    "killCooldownEnd",
    "sabotageCooldownEnd",
    "security",
    "admin",
    "reachLocation",
]

class Event:
    type: EventType
    details: str
    time: float

    def __init__(self, type: EventType, details: str, time: float):
        self.type = type
        self.details = details
        self.time = time

    def __str__(self):
        return f"{self.details} at {self.time}"


class ChatMessage:
    sender: str
    content: str
    time: float

    def __init__(self, sender: str, content: str, time: float):
        self.sender = sender
        self.content = content
        self.time = time

    def __str__(self):
        return f"{self.sender}: {self.content} (t={self.time})"


ActionType = Literal[
    "move",
    "report",
    "callMeeting",
    "sabotage",
    "kill",
    "vent",
    "security",
    "admin",
    "task",
]

class Action:
    type: ActionType
    details: str
    time: float
    interruptedAt: float | None = None
    completedAt: float | None = None
    interruptedBy: Event | None = None

    def __init__(self, type: ActionType, details: str, time: float = 0.0):
        self.type = type
        self.details = details
        self.time = time

    def __str__(self):
        to_return = ""
        if self.type == "move":
            to_return += f"You began moving to {self.details}"
        elif self.type == "report":
            to_return += f"You reported a body"
        elif self.type == "callMeeting":
            to_return += f"You called an emergency meeting"
        elif self.type == "sabotage":
            to_return += f"You sabotaged {self.details}"
        elif self.type == "kill":
            to_return += f"You killed a crewmate"
        elif self.type == "vent":
            to_return += f"You vented to {self.details}"
        elif self.type == "security":
            to_return += f"You checked security cameras"
        elif self.type == "admin":
            to_return += f"You checked the admin map"
        elif self.type == "task":
            to_return += f"You started a task at {self.details}"
        to_return += f" at t={self.time}"
        if self.completedAt is not None:
            to_return += f" and completed it at t={self.completedAt}"
        elif self.interruptedAt is not None:
            to_return += f" but were interrupted at t={self.interruptedAt} by: {str(self.interruptedBy)}"

    def __dict__(self):
        action: dict = {
            "type": self.type,
            "details": self.details,
            "time": self.time,
        }
        if self.interruptedAt is not None:
            action["interruptedAt"] = self.interruptedAt
        if self.completedAt is not None:
            action["completedAt"] = self.completedAt
        if self.interruptedBy is not None:
            action["interruptedBy"] = str(self.interruptedBy)
        return action


class AgentState:
    location: str
    sabotage: dict[str, bool]
    tasks: list[Task]
    imposterInformation: dict
    availableActions: list[ActionType]

    def __init__(
        self,
        location: str,
        sabotage: dict = {},
        tasks: list[Task] = [],
        imposterInformation: dict = {},
        availableActions: list[ActionType] = [],
    ):
        self.location = location
        self.sabotage = sabotage
        self.tasks = tasks
        self.imposterInformation = imposterInformation
        self.availableActions = availableActions

    def __str__(self):
        to_return = f"""Current Location: {self.location}"""
        if self.sabotage:
            to_return += f"\nCurrent Sabotage:\n{dumps(self.sabotage, indent=2)}"
        if self.tasks:
            to_return += (
                f"\nCurrent Tasks: {', '.join(str(task) for task in self.tasks)}"
            )
        if self.imposterInformation:
            to_return += f"\nImposter Information: {self.imposterInformation}"
        if self.availableActions:
            to_return += f"\nAvailable Actions: {', '.join(self.availableActions)}"
        return to_return
