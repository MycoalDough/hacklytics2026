def str_enumerate(lst: list[str]) -> str:
    return "\n".join(f"{i+1}. {item}" for i, item in enumerate(lst))
