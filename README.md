# MASDE / Multi-Agent Social Deduction Engine — LLM Social Deduction Desktop Pet <!-- [cite:1][cite:6] -->

A Unity + Python project that lets AI agents play an Among Us–inspired social deduction game autonomously (meetings, accusations, deception, voting), running like a “desktop pet” simulation. <!-- [cite:1][cite:6] -->

> If your repo uses a different name than “MASDE / Amongi”, rename this title + any paths below. <!-- (no citation; instruction) -->

---

## What this is

Social deduction games are interesting AI environments because they require partial information reasoning, trust, deception, and theory-of-mind–style inference. <!-- [cite:1] -->
This project explores that space by wiring Unity gameplay/visuals to a Python “brain” that drives each agent’s actions and meeting dialogue. <!-- [cite:1][cite:4][cite:9] -->

## Hackathon context (optional)

Built for Hacklytics 2026 (Golden Byte), Entertainment track, with the “always-on desk pet” angle in mind. <!-- [cite:6][cite:1] -->

> If you don’t want hackathon context in the public README, delete this section. <!-- (no citation; instruction) -->

---

## Features

- Among Us–style core loop: roaming, kills, vents, sabotages, meetings, and voting. <!-- [cite:1] -->
- Multi-agent support, with plans to pit different LLMs against each other (e.g., GPT/Claude/Gemini). <!-- [cite:1] -->
- Event-driven Unity ↔ Python integration (Unity C# scripts send observations/events; Python returns actions/chat). <!-- [cite:9][cite:14] -->
- Future-facing: analytics/dashboard ideas and lighter-weight local model “desk pet” variants. <!-- [cite:1] -->

---

## High-level architecture

- **Unity (C#):** world simulation, agent controllers, meeting UI/animation, and networking/transport glue. <!-- [cite:4][cite:11][cite:9] -->
- **Python:** receives batched game events, generates agent decisions + meeting messages, and pushes actions back. <!-- [cite:9][cite:14] -->
- **Protocol:** newline-delimited JSON messages; you may receive both single JSON objects (e.g., Chat/Vote pushes) and JSON arrays (batched responses). <!-- [cite:14][cite:15] -->

---

## Repo layout (edit to match your tree)

```text
.
├─ Unity/                  # Unity project (scenes, prefabs, C# scripts)
├─ server/                 # Python server (LLM agent logic, routing, prompts)
├─ docs/                   # (optional) diagrams, writeups, demo notes
└─ README.md
