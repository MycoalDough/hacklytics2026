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

    def create_host(self, callback):
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

        while True:
            self.client_socket, client_address = self.server_socket.accept()
            print(f"accepted connection from {client_address}")
            self._rx_buffer = ""  # reset buffer per new connection
            callback(self.client_socket)

    def initialize_agents(self):
        imposter = "Pink"
        for player in PLAYERS:
            self.agents[player] = Agent(
                role="imposter" if player == imposter else "crewmate"
            )

    async def main_loop(self):
        print("handle_client: listening for Unity agent requests...")
        while True:
            try:
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

            except Exception as e:
                print(f"Error in handle_client loop: {e}")
                break

    async def receive_events(self, events: list[dict]) -> list[dict]:
        try:
            actions = await gather(
                *[
                    self.agents[event["agent"]].on_event(
                        Event(
                            type=event["event"]["type"],
                            details=event["event"]["details"],
                            time=event["event"]["time"],
                        ),
                        AgentState(
                            location=event["state"]["location"],
                            sabotage=event["state"].get("sabotage", {}),
                            tasks=[
                                Task(
                                    location=task["location"],
                                    type=task["type"],
                                    status=task.get("status"),
                                )
                                for task in event["state"].get("tasks", [])
                            ],
                            imposterInformation=event["state"].get(
                                "imposterInformation", {}
                            ),
                            availableActions=event["state"].get("availableActions", []),
                        ),
                    )
                    for event in events
                ]
            )

            return [
                dict(action.__dict__(), agent=event["agent"])
                for action, event in zip(actions, events)
                if action is not None
            ]

        except Exception as e:
            print(f"Error in get_state_agent: {e}")
            return []

    def send_actions(self, actions: list[dict]) -> None:
        try:
            to_send = dumps(actions) + "\n"  # IMPORTANT: real newline delimiter
            self.client_socket.sendall(to_send.encode("utf-8"))
            print(f"play_state sent: {to_send}")
        except Exception as e:
            print(f"Error in play_state: {e}")
