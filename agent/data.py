from json import dumps, loads
import socket
from random import randint
from asyncio import gather

from agent.agent import Agent
from agent.constants import PLAYERS, Role, Action, Event


# ============================================================================
# CONFIGURATION
# ============================================================================


class DataConfig:

    # DO NOT CHANGE
    HOST = "127.0.0.1"
    PORT = 12345

    BUFFER_SIZE = 500000  #buffer size for socket data transfer (good enough)


class DataHandler:
    agents: dict[str, Agent] = {}

    def __init__(self, config=DataConfig):
        self.config = config
        self.server_socket: socket.socket
        self.client_socket: socket.socket

        print(f"initialized data handler for all agents")

    def create_host(self, callback):
        if self.server_socket:
            self.server_socket.close()

        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.bind((self.config.HOST, self.config.PORT))
        self.server_socket.listen()

        print(f"listening for connection on {self.config.HOST}:{self.config.PORT}")

        while True:
            self.client_socket, client_address = self.server_socket.accept()
            print(f"accepted connection from {client_address}")
            callback(self.client_socket)

    def initialize_agents(self):
        imposter = PLAYERS[randint(0, len(PLAYERS) - 1)]
        for player in PLAYERS:
            self.agents[player] = Agent(
                role=Role.IMPOSTOR if player == imposter else Role.CREWMATE
            )

    async def main_loop(self):
        """
        Main receive loop. Blocks and waits for Unity to send:
            '[{"agent": "AGENT_NAME", "event": EVENT_INFO}, ...]'
        Then calls action_callback(agent_name, obs_info) to get the response action,
        """
        print("handle_client: listening for Unity agent requests...")
        while True:
            try:
                raw = (
                    self.client_socket.recv(self.config.BUFFER_SIZE)
                    .decode("utf-8")
                    .strip()
                )
                if not raw:
                    print("handle_client: no data received, closing connection.")
                    self.client_socket.close()
                    break

                data: dict = loads(raw)

                if data["type"] == "requestChat":
                    print(f"Received chat request: {data}")
                elif data["type"] == "events":
                    actions = await self.receive_events(data["events"])
                    self.send_actions(actions)

            except Exception as e:
                print(f"Error in handle_client loop: {e}")
                break

    # -------------------------------------------------------------------------
    # RECEIVE: Unity → Python
    # -------------------------------------------------------------------------

    async def receive_events(self, events: list[dict]) -> list[Action]:
        """
        Receives a state request sent by a Unity agent.
        """
        try:
            actions = await gather(
                *[
                    self.agents[event["agent"]].on_event(
                        Event(
                            type=event["event"]["type"],
                            details=event["event"]["details"],
                            time=event["event"]["time"],
                        )
                    )
                    for event in events
                ]
            )
            actions = [action for action in actions if action is not None]
            return actions

        except Exception as e:
            print(f"Error in get_state_agent: {e}")
            return []

    # -------------------------------------------------------------------------
    # SEND: Python → Unity  (response)
    # -------------------------------------------------------------------------

    def send_actions(self, actions: list[Action]) -> None:
        """
        Sends the action response back to Unity after receiving a get_state_agent request.
        step format:  'COLOR|ACTION_INT'
        Final message sent: 'play_state:COLOR|ACTION_INT'
        """
        try:
            to_send = dumps([action.__dict__() for action in actions])
            self.client_socket.send(to_send.encode("utf-8"))
            print(f"play_state sent: {to_send}")

        except Exception as e:
            print(f"Error in play_state: {e}")
