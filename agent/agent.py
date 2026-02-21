from agent.constants import BASE_SYSTEM_MESSAGE, ChatMessage, Role, Event, Action


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
        self.system_prompt = BASE_SYSTEM_MESSAGE + f"Your role is {role.value}."
        if role == Role.IMPOSTOR and other_imposters:
            self.system_prompt += (
                f" The other impostors are: {', '.join(other_imposters)}."
            )
        self.system_prompt += "\n\nAdditional Instructions:\n" + system_prompt.strip()

    async def on_event(self, event: Event) -> Action | None:
        self.event_history.append(event)
