from ollama import ChatResponse, AsyncClient

from agent.constants import (
    BASE_SYSTEM_MESSAGE,
    VENTS,
    AgentState,
    ChatMessage,
    Role,
    Event,
    Action,
)
from agent.llm import ACTION_MAP, findClosestVent, getFastestPath, information_tools


class Agent:
    system_prompt: str
    role: Role
    chat_history: list[ChatMessage] = []
    current_chat_history: list[ChatMessage] = []
    event_history: list[Event] = []
    action_history: list[Action] = []
    current_action: Action | None = None
    thought_history: list[str] = []
    thoughts: str = ""

    def __init__(self, role: Role, system_prompt="", other_imposters: list[str] = []):
        self.role = role
        self.system_prompt = BASE_SYSTEM_MESSAGE + f"Your role is {role}."
        if role == "impostor" and other_imposters:
            self.system_prompt += (
                f" The other impostors are: {', '.join(other_imposters)}."
            )
        self.system_prompt += "\n\nAdditional Instructions:\n" + system_prompt.strip()

    async def on_event(self, event: Event, state: AgentState) -> Action | None:
        if event.type == "reachLocation":
            if self.current_action is not None and self.current_action.type == "move":
                self.current_action.completedAt = event.time
                self.current_action = None
        if event.type == "completeTask":
            if self.current_action is not None and self.current_action.type == "task":
                self.current_action.completedAt = event.time
                self.current_action = None

        available_vents = set()

        if "Vent" in state.availableActions:
            vent_set = set([vents for vents in VENTS if state.location in vents][0])
            vent_set.remove(state.location)
            available_vents = vent_set

        allowed_actions = [
            ACTION_MAP[action] for action in state.availableActions
        ] + information_tools

        total_history = self.chat_history + self.action_history + self.event_history
        total_history.sort(key=lambda x: x.time)
        # total_history = total_history[-40:]

        messages: list[dict] = [
            {
                "role": "system",
                "content": self.system_prompt,
            },
            {
                "role": "user",
                "content": f"""Game History:
{"\n".join(str(x) for x in total_history)}

Current State:
{str(state)}

Current Action:
{str(self.current_action)}

Current Thoughts:
{self.thoughts}

What would you like to do?
You can either:
1. Update your thoughts using the think() function. Your future events will see this new thought. You should do this at max once. You should format your thought as:
"Current Priority: <what your current priority is, e.g. 'gathering information', 'completing tasks', 'sabotaging', etc.>
Reasoning: <your reasoning for this priority>
Next Steps: <what your next steps are to accomplish this priority, e.g. 'move to electrical to complete tasks', 'move to cafeteria to find a victim to kill'>
Additional Notes: <any additional notes you have, eg. 'I think Blue is suspicious because they were near the body and didn't report it'>"
2. Use getFastestPath() or findClosestVent() to get information about the map to help you make a decision.
3. Take an action using the allowed actions. Taking an action ends your turn.
4. Continue your current action using the continue_current_action() function. This ends your turn. If you are done thinking and just want to continue your current action, you should choose this.
You can only call one tool at a time."""
                + (
                    "\n\nNote: If you want to vent, you can only vent to "
                    + ", ".join(available_vents)
                    if available_vents
                    else ""
                ),
            },
        ]
        client = AsyncClient()

        while True:
            response: ChatResponse = await client.chat(
                model="gemma3:1b",
                messages=messages,
                tools=allowed_actions,
                think=True,
            )
            if response["message"].get("tool_calls", []):
                tool_call = response["message"]["tool_calls"][0]
                messages.append(
                    {
                        "role": "assistant",
                        "tool_calls": [response["message"]["tool_calls"][0]],
                    }
                )
                if tool_call["type"] == "think":
                    self.thoughts = tool_call["arguments"]["new_thought"]
                    self.thought_history.append(self.thoughts)
                    messages.append(
                        {
                            "role": "tool",
                            "name": "think",
                            "content": "Thought updated.",
                        }
                    )
                elif tool_call["type"] == "getFastestPath":
                    start = tool_call["arguments"]["start"]
                    end = tool_call["arguments"]["end"]
                    path = getFastestPath(start, end)
                    messages.append(
                        {
                            "role": "tool",
                            "name": "getFastestPath",
                            "content": f"The fastest path from {start} to {end} is: {', '.join(path)}.",
                        }
                    )
                elif tool_call["type"] == "findClosestVent":
                    location = tool_call["arguments"]["location"]
                    closest_vent = findClosestVent(location)
                    messages.append(
                        {
                            "role": "tool",
                            "name": "findClosestVent",
                            "content": f"The closest vent to {location} is {closest_vent}.",
                        }
                    )
                elif tool_call["type"] == "continue_current_action":
                    break
                else:
                    action: Action = ACTION_MAP[tool_call["type"]](
                        **tool_call["arguments"]
                    )
                    action.time = event.time
                    if self.current_action is not None:
                        self.current_action.interruptedAt = event.time
                        self.current_action.interruptedBy = event
                    if action.type in ["move", "task"]:
                        self.current_action = action
                    self.action_history.append(action)
                    return action

        self.event_history.append(event)
