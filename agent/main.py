from asyncio import run

from data import DataHandler

if __name__ == "__main__":
    handler = DataHandler()
    handler.initialize_agents()
    run(handler.main_loop())
