import json

from openai import AsyncOpenAI

from agent.constants import (
    ALL_VENTS,
    BASE_SYSTEM_MESSAGE,
    HALLWAYS,
    ROOMS,
    VENTS,
    AgentState,
    ChatMessage,
    Role,
    Event,
    Action,
)
from agent.llm import (
    ACTION_MAP,
    continue_current_action,
    findClosestVent,
    getFastestPath,
    information_tools,
)


class Agent:
    system_prompt: str
    role: Role
    color: str
    chat_history: list[ChatMessage]
    current_chat_history: list[ChatMessage]
    event_history: list[Event]
    action_history: list[Action]
    current_action: Action | None
    thought_history: list[str]
    thoughts: str

    def __init__(
        self, role: Role, color: str, system_prompt="", other_imposters: list[str] = []
    ):
        self.role = role
        self.color = color
        self.chat_history = []
        self.current_chat_history = []
        self.event_history = []
        self.action_history = []
        self.current_action = None
        self.thought_history = []
        self.thoughts = ""

        self.system_prompt = (
            BASE_SYSTEM_MESSAGE + f"Your role is {role}. Your color is {color}."
        )
        if role == "impostor" and other_imposters:
            self.system_prompt += (
                f" The other impostors are: {', '.join(other_imposters)}."
            )
        self.system_prompt += "\n\nAdditional Instructions:\n" + system_prompt.strip()

    @staticmethod
    def _tool_schema(name: str, description: str, properties=None, required=None):
        return {
            "type": "function",
            "function": {
                "name": name,
                "description": description,
                "parameters": {
                    "type": "object",
                    "properties": properties or {},
                    "required": required or [],
                },
            },
        }

    @classmethod
    def _build_tool_schemas(
        cls, location: str, available_vents: list[str]
    ) -> dict[str, dict]:
        return {
            "think": cls._tool_schema(
                "think",
                "Update internal thoughts for future decisions.",
                properties={"new_thought": {"type": "string"}},
                required=["new_thought"],
            ),
            "getFastestPath": cls._tool_schema(
                "getFastestPath",
                "Get the fastest path between two locations.",
                properties={
                    "start": {"type": "string"},
                    "end": {"type": "string"},
                },
                required=["start", "end"],
            ),
            "findClosestVent": cls._tool_schema(
                "findClosestVent",
                "Find the closest vent to a location.",
                properties={"location": {"type": "string"}},
                required=["location"],
            ),
            "continue_current_action": cls._tool_schema(
                "continue_current_action",
                "Continue the current action without changes.",
            ),
            "Move": cls._tool_schema(
                "Move",
                "Move to a specified location.",
                properties={
                    "to": {
                        "type": "string",
                        "enum": [room for room in ROOMS + HALLWAYS if room != location],
                    }
                },
                required=["to"],
            ),
            "Report": cls._tool_schema(
                "Report",
                "Report the closest body.",
            ),
            "CallMeeting": cls._tool_schema(
                "CallMeeting",
                "Call an emergency meeting.",
            ),
            "Sabotage": cls._tool_schema(
                "Sabotage",
                "Sabotage a system.",
                properties={
                    "system": {
                        "type": "string",
                        "enum": ["O2", "Reactor", "Electrical"],
                    }
                },
                required=["system"],
            ),
            "Kill": cls._tool_schema(
                "Kill",
                "Kill the closest crewmate.",
            ),
            "Vent": cls._tool_schema(
                "Vent",
                "Vent to a specified vent.",
                properties={"vent": {"type": "string", "enum": available_vents}},
                required=["vent"],
            ),
            "Security": cls._tool_schema(
                "Security",
                "Check security cameras.",
            ),
            "Admin": cls._tool_schema(
                "Admin",
                "Check the admin map.",
            ),
            "Task": cls._tool_schema(
                "Task",
                "Perform a task at the current location.",
            ),
        }

    async def on_event(self, events: list[Event], state: AgentState) -> Action | None:
        for event in events:
            if event.type == "reachLocation":
                if (
                    self.current_action is not None
                    and self.current_action.type == "Move"
                ):
                    self.current_action.completedAt = event.time
                    self.current_action = None
            if event.type == "completeTask":
                if (
                    self.current_action is not None
                    and self.current_action.type == "Task"
                ):
                    self.current_action.completedAt = event.time
                    self.current_action = None

        available_vents = set()

        self.event_history += events

        if "Vent" in state.availableActions:
            vent_set = set([vents for vents in VENTS if state.location in vents][0])
            vent_set.remove(state.location)
            available_vents = vent_set

        allowed_actions = [
            ACTION_MAP[action] for action in state.availableActions
        ] + information_tools

        if self.current_action is not None:
            allowed_actions.append(continue_current_action)

        total_history = self.chat_history + self.action_history + self.event_history
        total_history.sort(key=lambda x: x.time)
        total_history = total_history[-50:]

        messages: list[dict] = [
            {
                "role": "system",
                "content": self.system_prompt,
            },
            {
                "role": "user",
                "content": f"""Game History:
{"\n".join(str(x) for x in total_history)}

Current Time:
{events[-1].time}

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
Additional Notes: <any additional notes you have>"
2. Use getFastestPath() or findClosestVent() to get information about the map to help you make a decision.
3. Take an action using the allowed actions. Taking an action ends your turn.
{"4. Continue your current action using the continue_current_action() function. This ends your turn. If you are done thinking and just want to continue your current action, you should choose this." if self.current_action is not None else ""}
You can only call one tool at a time.
Unless something really unexpected happens, you should probably continue your current action.
Note: Do not move to the room you are currently in.
Note: No matter what, you must send a tool call. If you don't want to do anything, you can call continue_current_action() to continue doing nothing. If you want to think, use the think() function."""
                + (
                    "\n\nNote: If you want to vent, you can only vent to "
                    + ", ".join(available_vents)
                    if available_vents and self.role == "imposter"
                    else ""
                )
                + (
                    "\n\nRemember, you are an imposter. If you see a free kill without many people around, you should take it. Additionally, you should be relatively aggressive in kills."
                    if self.role == "imposter"
                    else ""
                ),
            },
        ]
        tool_schemas = self._build_tool_schemas(
            location=state.location, available_vents=list(available_vents)
        )
        tools = [tool_schemas[action.__name__] for action in allowed_actions]
        client = AsyncOpenAI()

        hasThought = False

        while True:
            response = await client.chat.completions.create(
                model="gpt-4.1-mini",
                messages=messages,
                tools=tools,
                tool_choice=(
                    "required"
                    if hasThought
                    else {"type": "function", "function": {"name": "think"}}
                ),
            )
            hasThought = True
            tool_calls = response.choices[0].message.tool_calls or []
            if tool_calls:
                tool_call = tool_calls[0]
                tool_name = tool_call.function.name
                tool_args = json.loads(tool_call.function.arguments or "{}")
                messages.append(
                    {
                        "role": "assistant",
                        "content": None,
                        "tool_calls": [
                            {
                                "id": tool_call.id,
                                "type": "function",
                                "function": {
                                    "name": tool_name,
                                    "arguments": tool_call.function.arguments,
                                },
                            }
                        ],
                    }
                )
                if tool_name == "think":
                    self.thoughts = tool_args["new_thought"]
                    self.thought_history.append(self.thoughts)
                    if self.role == "imposter":
                        print("\n\n\n" + self.thoughts + "\n\n\n")
                    messages.append(
                        {
                            "role": "tool",
                            "tool_call_id": tool_call.id,
                            "content": "Thought updated.",
                        }
                    )
                elif tool_name == "getFastestPath":
                    start = tool_args["start"]
                    end = tool_args["end"]
                    path = getFastestPath(start, end)
                    messages.append(
                        {
                            "role": "tool",
                            "tool_call_id": tool_call.id,
                            "content": f"The fastest path from {start} to {end} is: {', '.join(path)}.",
                        }
                    )
                elif tool_name == "findClosestVent":
                    location = tool_args["location"]
                    closest_vent = findClosestVent(location)
                    messages.append(
                        {
                            "role": "tool",
                            "tool_call_id": tool_call.id,
                            "content": f"The closest vent to {location} is {closest_vent}.",
                        }
                    )
                elif tool_name == "continue_current_action":
                    break
                else:
                    if tool_name == "Move" and tool_args["to"] == state.location:
                        messages.append(
                            {
                                "role": "tool",
                                "tool_call_id": tool_call.id,
                                "content": f"You are already in {state.location}. Please choose a different location to move to.",
                            }
                        )
                        continue
                    action: Action = ACTION_MAP[tool_name](**tool_args)
                    action.time = event.time
                    if self.current_action is not None:
                        self.current_action.interruptedAt = event.time
                        self.current_action.interruptedBy = event
                    if action.type in ["Move", "Task"]:
                        self.current_action = action
                    self.action_history.append(action)
                    self.event_history.append(event)

                    if self.role == "imposter":
                        print(f"Imposter Action: {action}")

                    return action
