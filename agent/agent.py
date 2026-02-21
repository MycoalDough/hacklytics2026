from enum import Enum

INFORMATION = {
    "Rooms": """
1. Cafeteria
2. Weapons
3. O2
4. Navigation
5. Shields
6. Communications
7. Storage
8. Electrical
9. Lower Engine
10. Reactor
11. Security
12. Upper Engine
13. MedBay
14. Admin
""".strip(),
    "Hallways": """
A: Upper Engine (left) - MedBay (down) - Cafeteria (right)
B: Cafeteria (left) - Weapons (right)
C: Upper Engine (up) - Reactor (left) - Security (right) - Lower Engine (down)
D: Cafeteria (up) - Admin (right) - Storage (down)
E: Weapons (up) - O2 (left) - Navigation (right) - Shields (down)
F: Lower Engine (left) - Electrical (up) - Storage (right)
G: Storage (left) - Communications (down) - Shields (right)
""".strip(),
    "Vents": """
1. Upper Engine - Reactor
2. Cafeteria - Hallway E - Admin
3. MedBay - Security - Electrical
4. Reactor - Lower Engine
5. Navigation - Shields
""".strip(),
    "Tasks": """
""".strip(),
}


class Role(Enum):
    CREWMATE = "crewmate"
    IMPOSTOR = "impostor"


class Agent:
    system_prompt: str
    role: Role
    chat_history: list[dict[str, str]] = []
    event_history: list[str] = []
    thought_history: list[str] = []
    thoughts: str = ""

    def __init__(self, role, system_prompt):
        self.role = role
        self.system_prompt = system_prompt
