from ollama import ChatResponse, chat

from agent.constants import (
    BASE_SYSTEM_MESSAGE,
    AgentState,
    ChatMessage,
    Role,
    Event,
    Action,
)
from agent.llm import ACTION_MAP, information_tools


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
        allowed_actions = [
            ACTION_MAP[action] for action in state.availableActions
        ] + information_tools

        total_history = self.chat_history + self.action_history + self.event_history
        total_history.sort(key=lambda x: x.time)
        # total_history = total_history[-40:]

        messages = [
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
1. Update your thoughts using the think() function. Your future events will see this new thought. You should format your thought as:
"Current Priority: <what your current priority is, e.g. 'gathering information', 'completing tasks', 'sabotaging', etc.>
Reasoning: <your reasoning for this priority>
Next Steps: <what your next steps are to accomplish this priority, e.g. 'move to electrical to complete tasks', 'move to cafeteria to find a victim to kill'>
Additional Notes: <any additional notes you have, eg. 'I think Blue is suspicious because they were near the body and didn't report it'>"
2. Use getFastestPath() or findClosestVent() to get information about the map to help you make a decision.
3. Take an action using the allowed actions. Taking an action ends your turn.""",
            },
        ]

        response: ChatResponse = chat(
            model="gemma3:1b",
            messages=messages,
            tools=allowed_actions,
        )

        self.event_history.append(event)
