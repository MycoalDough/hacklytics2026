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
        self.num_agents = config.NUM_AGENTS
        self.server_socket: Optional[socket.socket] = None
        self.client_socket: Optional[socket.socket] = None
        
        print(f"initialized data handler for all agents")
    
    def create_host(self, callback, num_agents):
        if self.server_socket:
            self.server_socket.close()
        
        self.num_agents = num_agents
        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.bind((self.config.HOST, self.config.PORT))
        self.server_socket.listen()
        
        print(f"listening for connection on {self.config.HOST}:{self.config.PORT}")
        
        while True:
            self.client_socket, client_address = self.server_socket.accept()
            print(f"accepted connection from {client_address}")
            callback(self.client_socket)
    
    def get_state_for_agent(self, param: str) -> tuple:
        """will be sent by server to agent. expect: "data:RED:information of state and actions..."""
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
    
    def play_state(self, step: str) -> tuple:
        """play state for agent (param: "RED" "BLUE" "GREEN" "YELLOW" "PURPLE" "PINK") : "action as integer" (need to give this as context
        above for get_state_for_agent) 
        
        will look like: play_state("RED|3")"""
        try:
            to_send = f"play_state:{step}" #so will finally look like play_state:RED|3 when sending
            self.client_socket.send(to_send.encode("utf-8"))

        except Exception as e:
            print(f"Error in play_step: {e}")

    
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
    """gets state using global handler"""
    return _global_handler.get_state_for_agent()

def play_state(step):
    """plays step using global handler"""
    return _global_handler.play_state(step)

def convert_list(data_string):
    """converts list using global handler"""
    return _global_handler._convert_list(data_string)

def is_float(value):
    """checks float using global handler"""
    return DataHandler.is_float(value)


# ============================================================================
# Usage Example
# ============================================================================

if __name__ == '__main__':
    handler = DataHandler()
    
    def on_connected(sock):
        print("was able to fully connect to server. starting simulation.")
    
    # handler.create_host(on_connected)
    
    # Example 2: Using legacy API (for backward compatibility)
    # create_host(on_connected)