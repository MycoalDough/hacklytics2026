from json import dumps, loads
import socket
from asyncio import gather

from agent.agent import Agent
from agent.constants import PLAYERS, AgentState, Event, Task


class DataConfig:
    HOST = "127.0.0.1"
    PORT = 12345
    BUFFER_SIZE = 500000


class DataHandler:
    agents: dict[str, Agent] = {}

    def __init__(self, config=DataConfig):
        self.config = config

        self.server_socket: socket.socket = None  # type: ignore
        self.client_socket: socket.socket = None  # type: ignore

        self._rx_buffer = ""  # IMPORTANT: init buffer for NDJSON

        print("initialized data handler for all agents")

    def create_host(self):
        if self.server_socket is not None:
            try:
                self.server_socket.close()
            except:
                pass

        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server_socket.bind((self.config.HOST, self.config.PORT))
        self.server_socket.listen()

        print(f"listening for connection on {self.config.HOST}:{self.config.PORT}")

        self.client_socket, client_address = self.server_socket.accept()
        print(f"accepted connection from {client_address}")
        self._rx_buffer = ""  # reset buffer per new connection

    def initialize_agents(self):
        imposter = "Pink"
        for player in PLAYERS:
            self.agents[player] = Agent(
                color=player, role="imposter" if player == imposter else "crewmate"
            )

    async def handle_meeting(self, event: Event):
        details = loads(event.details)
        caller = details["caller"]
        alive_players = details["alivePlayers"]
        chat_order = [caller] + [p for p in alive_players if p != caller]
        total_chat_history = []

        for round in range(1, 4):
            for player in chat_order:
                message = await self.agents[player].on_request_chat(
                    body_found=(event.type == "bodyFound"),
                    question_round=round,
                    was_reporter=player == caller,
                )
                to_send = (
                    dumps(
                        {
                            "type": "Chat",
                            "details": message,
                            "time": event.time,
                            "agent": player,
                        }
                    )
                    + "\n"
                )  # IMPORTANT: real newline delimiter
                self.client_socket.sendall(to_send.encode("utf-8"))
                chatMessage = Event(
                    type="chatMessage",
                    details=f"{player}: {message}",
                    time=event.time,
                )
                for agent in self.agents.values():
                    agent.on_chat_message(chatMessage)
                total_chat_history.append(chatMessage)

        votes = []

        for agent in self.agents.values():
            vote = agent.on_vote()
            votes.append(vote)
            for a in self.agents.values():
                a.event_history.append(
                    Event(
                        type="vote",
                        details=f"{agent.color} voted {vote}",
                        time=event.time,
                    )
                )

        greatest_vote = max(set(votes), key=votes.count)

        to_send = (
            dumps(
                {
                    "type": "Vote",
                    "details": greatest_vote,
                    "time": event.time,
                    "agent": "Red",  # Ignore
                }
            )
            + "\n"
        )  # IMPORTANT: real newline delimiter
        self.client_socket.sendall(to_send.encode("utf-8"))

        for agent in self.agents.values():
            agent.chat_history += total_chat_history

    async def main_loop(self):
        actions = await self.receive_events(
            [
                {
                    "agent": agent,
                    "event": {
                        "type": "seePlayer",
                        "details": "You have entered the game. You should go to a random location.",
                        "time": 0.0,
                    },
                    "state": {
                        "location": "Cafeteria",
                        "sabotage": {},
                        "tasks": [],
                        "imposterInformation": {},
                        "availableActions": [
                            "Move",
                        ],
                    },
                }
                for agent in self.agents.keys()
            ]
        )
        self.send_actions(actions)
        while True:
            chunk = self.client_socket.recv(self.config.BUFFER_SIZE).decode("utf-8")
            if not chunk:
                print("handle_client: no data received, closing connection.")
                self.client_socket.close()
                break

            self._rx_buffer += chunk

            # NDJSON: process complete lines
            while "\n" in self._rx_buffer:
                line, self._rx_buffer = self._rx_buffer.split("\n", 1)
                line = line.strip()
                if not line:
                    continue

                data: dict = loads(line)

                if data.get("type") == "requestChat":
                    print(f"Received chat request: {data}")
                elif data.get("type") == "events":
                    actions = await self.receive_events(data["events"])
                    self.send_actions(actions)

    async def receive_events(self, events: list[dict]) -> list[dict]:
        events.sort(key=lambda e: e["event"]["time"])
        events_by_agent = {agent: [] for agent in self.agents.keys()}
        for event in events:
            events_by_agent[event["agent"]].append(event)
        agent_states = {
            agent: {} for agent in self.agents.keys() if events_by_agent[agent]
        }
        for agent, agent_events in events_by_agent.items():
            if agent_events:
                agent_states[agent] = agent_events[-1]["state"]

        actions = await gather(
            *[
                self.agents[agent].on_event(
                    [
                        Event(
                            type=event["event"]["type"],
                            details=event["event"]["details"],
                            time=event["event"]["time"],
                        )
                        for event in events_by_agent[agent]
                    ],
                    AgentState(
                        location=agent_states[agent]["location"],
                        sabotage=agent_states[agent].get("sabotage", {}),
                        tasks=[
                            Task(
                                location=task["location"],
                                type=task["type"],
                                status=task.get("status"),
                            )
                            for task in agent_states[agent].get("tasks", [])
                        ],
                        imposterInformation=agent_states[agent].get(
                            "imposterInformation", {}
                        ),
                        availableActions=agent_states[agent].get(
                            "availableActions", []
                        ),
                    ),
                )
                for agent in agent_states.keys()
            ]
        )

        if events[-1]["event"]["type"] in ["bodyFound", "emergencyMeeting"]:
            await self.handle_meeting(events[-1]["event"])
            return []

        return [
            dict(action.__dict__(), agent=agent)
            for action, agent in zip(actions, agent_states.keys())
            if action is not None
        ]

    def send_actions(self, actions: list[dict]) -> None:
        try:
            to_send = dumps(actions) + "\n"  # IMPORTANT: real newline delimiter
            self.client_socket.sendall(to_send.encode("utf-8"))
        except Exception as e:
            print(f"Error in play_state: {e}")
