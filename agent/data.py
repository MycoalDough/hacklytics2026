import socket
import time
import json
import numpy as np 
import threading
from typing import Optional, List


# ============================================================================
# CONFIGURATION
# ============================================================================


class DataConfig:
    
    #DO NOT CHANGE
    HOST = "127.0.0.1"
    PORT = 12345
        
    BUFFER_SIZE = 500000  #buffer size for socket data transfer (good enough)
    
    AGENT_SEPARATOR = '%'  #seperates diff agent states (DO NOT CHANGE)
    VALUE_SEPARATOR = '|'  #seperates diff agent values within a state (DO NOT CHANGE)



class DataHandler:
    
    def __init__(self, config=DataConfig):
        self.config = config
        self.server_socket: Optional[socket.socket] = None
        self.client_socket: Optional[socket.socket] = None
        
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
    def handle_client(self, action_callback):
        """
        Main receive loop. Blocks and waits for Unity to send:
            'get_state_agent:AGENT_NAME:obs1|obs2|obs3'
        Then calls action_callback(agent_name, obs_info) to get the actions string,
        and responds to Unity with:
            'play_state:ACTION1|ACTION2|ACTION3'

        Usage:
            def my_callback(agent_name, obs_info):
                # run your model here
                return "MOVE_LEFT|STAY|MOVE_RIGHT"

            handler.create_host(lambda sock: handler.handle_client(my_callback))
        """
        print("handle_client: listening for Unity agent requests...")
        while True:
            try:
                agent_name, obs_info = self.get_state_agent()
                if agent_name == "ERROR":
                    print("handle_client: error receiving state, stopping loop.")
                    break
                
                print(f"handle_client: got request from agent '{agent_name}' | obs: {obs_info}")
                actions = action_callback(agent_name, obs_info)
                self.play_state(actions)

            except Exception as e:
                print(f"Error in handle_client loop: {e}")
                break

    # -------------------------------------------------------------------------
    # RECEIVE: Unity → Python
    # -------------------------------------------------------------------------

    def get_state_agent(self) -> tuple:
        """
        Receives a state request sent by a Unity agent.
        Expects format: 'get_state_agent:AGENT_NAME:DATA'
        Returns: (agent_name: str, obs_info: str)
        """
        try:
            raw = self.client_socket.recv(self.config.BUFFER_SIZE).decode("utf-8").strip()
            
            if not raw:
                raise ValueError("no data received from Unity")
            
            # format: get_state_agent:RED:DATA
            parts = raw.split(':', 2)  # max 3 parts so obs_info can contain colons safely
            
            if len(parts) < 3 or parts[0] != "get_state_agent":
                raise ValueError(f"unexpected message format: '{raw}'")
            
            agent_name = parts[1]   #"RED"
            obs_info   = parts[2]   #"DATA"
            
            return (agent_name, obs_info)

        except Exception as e:
            print(f"Error in get_state_agent: {e}")
            return ("ERROR", "")

    # -------------------------------------------------------------------------
    # SEND: Python → Unity  (response)
    # -------------------------------------------------------------------------

    def play_state(self, step: str) -> None:
        """
        Sends the action response back to Unity after receiving a get_state_agent request.
        step format:  'COLOR|ACTION_INT'
        Final message sent: 'play_state:COLOR|ACTION_INT'
        """
        try:
            to_send = f"play_state:{step}"
            self.client_socket.send(to_send.encode("utf-8"))
            print(f"play_state sent: {to_send}")

        except Exception as e:
            print(f"Error in play_state: {e}")

    # -------------------------------------------------------------------------
    # LEGACY (old protocol where python wasn't the receiever)
    # -------------------------------------------------------------------------

    def get_state_for_agent(self, param: str) -> tuple:
        """
        [LEGACY] Old protocol: Python sent 'get_data_for:param' and Unity responded.
        Kept for backward compatibility — use get_state_agent() for new protocol.
        """
        try:
            to_send = f"get_data_for:{param}"
            self.client_socket.send(to_send.encode("utf-8"))
            state_data = self.client_socket.recv(self.config.BUFFER_SIZE).decode("utf-8")
            
            if not state_data:
                raise ValueError("no data recieved from server")
                        
            return state_data
            
        except Exception as e:
            print(f"Error in get_state_for_agent: {e}")
            return "error in getting data. please return the string ERROR IN GETTING DATA."

    # -------------------------------------------------------------------------
    # UTILITIES
    # -------------------------------------------------------------------------

    #converts data string to list of floats
    def _convert_list(self, data_string: str) -> List[float]:
        try:
            data_string = data_string.strip('[]')
            
            if ',' in data_string:
                return [float(x.strip()) for x in data_string.split(',') if x.strip()]
            elif self.config.VALUE_SEPARATOR in data_string:
                return [float(x.strip()) for x in data_string.split(self.config.VALUE_SEPARATOR) if x.strip()]
            elif ' ' in data_string:
                return [float(x.strip()) for x in data_string.split() if x.strip()]
            else:
                return [float(data_string.strip())]
        except Exception as e:
            print(f"Error converting list: {e}")
            return []
    
    @staticmethod
    def is_float(value: str) -> bool:
        try:
            float(value)
            return True
        except ValueError:
            return False



# ============================================================================
# legacy api (backwards compatability)
# ============================================================================


#global instance if used as main or called as .data
_global_handler = DataHandler()


host = DataConfig.HOST
port = DataConfig.PORT
current_data = []
server_socket = None
client_socket = None


def create_host(callback):
    """create host using global handler"""
    global client_socket
    
    def wrapped_callback(sock):
        global client_socket
        client_socket = sock
        callback(sock)
    
    _global_handler.create_host(wrapped_callback)


def get_state_for_agent():
    """[LEGACY] gets state using global handler"""
    return _global_handler.get_state_for_agent()


def get_state_agent():
    """gets incoming agent state request using global handler"""
    return _global_handler.get_state_agent()


def play_state(step):
    """sends action response using global handler"""
    return _global_handler.play_state(step)


def convert_list(data_string):
    """converts list using global handler"""
    return _global_handler._convert_list(data_string)


def is_float(value):
    """checks float using global handler"""
    return DataHandler.is_float(value)

