from asyncio import run

from agent.data import DataHandler

if __name__ == "__main__":
    handler = DataHandler()
    handler.initialize_agents()
    handler.create_host()
    run(handler.main_loop())
