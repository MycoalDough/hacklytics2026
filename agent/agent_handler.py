#import torch
#import torch.nn as nn
#import torch.nn.functional as F
#import torch.optim as optim
#import numpy as np
import os

if __name__ == '__main__':
    import data


    def on_connection_established(client_socket):
        print("Connection established")
        
        #code here.
        #data.py can be called via data.get_state_for_agent() or data.play_state(action)
    
    data.create_host(on_connection_established)