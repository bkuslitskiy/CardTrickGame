# Design Decisions

## Architecture Overview

This document tracks all architectural and design decisions made during development. Each decision is recorded with its rationale and implications.

### Decision 1: Tech Stack - Unity + C#
**Date**: May 5, 2026
**Decision**: Use Unity as the game engine with C# for all logic.
**Rationale**:
- Cross-platform support (Windows desktop + Android with minimal code changes)
- Excellent for card game UI
- Strong sideload support for Android
- Clean separation between game logic and UI layer
**Implications**:
- Must maintain platform-agnostic core logic
- All game code in `Assets/Scripts/` compiles identically on both platforms
- UI layer will need touch input adaptation for Android later

### Decision 2: AI Abstraction via IPlayer Interface
**Date**: May 5, 2026
**Decision**: All players (human + AI) implement `IPlayer` interface.
**Rationale**:
- Decouples game logic from AI implementation
- Allows seamless swapping between rule-based and ML-based AI at runtime
- Game loop doesn't care if opponent is human, rule-based, or ML
- Future RL implementation requires only new `IPlayer` subclass
**Implications**:
- AI decisions must go through standardized interface (`SelectCard()`, `RevealCard()`)
- Game state fully observable to AI (imperfect information handled at AI level, not game level)
- No singletons or static AI logic

### Decision 3: Rule-Based AI with Difficulty Levels
**Date**: May 5, 2026
**Decision**: Start with rule-based AI (Easy/Medium/Hard) before adding ML.
**Rationale**:
- Fast iteration on game logic
- Predictable baseline to test game rules
- Provides comparison point for later RL training
- Each difficulty level uses strategy objects to modify decision-making
**Implications**:
- Easy AI: Random valid plays, basic reveals
- Medium AI: Prefers high-value cards, tracks discards
- Hard AI: Attempts to predict wins, card counting, strategic reveals
- ML later replaces strategy layer entirely

### Decision 4: Pure Game Logic Layer (No UI Dependencies)
**Date**: May 5, 2026
**Decision**: All game logic in `Core/` has zero dependencies on UI or MonoBehaviour.
**Rationale**:
- Core logic fully testable in unit tests
- Decoupled from Unity's Update/event loop
- Easier to port to Android or other platforms
- Facilitates RL training (no UI overhead)
**Implications**:
- Game state is plain C# classes (no Unity components)
- Game state is immutable where possible
- UI layer requests decisions via event system, not direct polling
- GameManager handles coordination between logic and UI

### Decision 5: Game State Machine (Reveal → Play → Determine → Score)
**Date**: May 5, 2026
**Decision**: Each round progresses through clearly defined phases.
**Rationale**:
- Matches the game rules exactly (Reveal, Play, Determination, Scoring)
- Clear state transitions prevent rule violations
- Easy to visualize in debug and testing
- Extensible for RL reward signal design
**Implications**:
- GameManager maintains current phase
- Each phase has entry/exit logic
- State transitions validated before occurring

### Decision 6: Separate AI Factory for Dependency Injection
**Date**: May 5, 2026
**Decision**: `AIFactory` class creates AI instances based on difficulty and type.
**Rationale**:
- Centralized AI instantiation
- Easy to switch between rule-based and ML implementations
- Configuration lives in one place
**Implications**:
- New AI types added via factory extension, not scattered across code
- Configuration file (or code constant) defines available difficulties

---

## Future Design Decisions (To Be Made)

### RL Integration
- State representation for neural networks (raw vs engineered features)
- Training environment (self-play loop design)
- Reward structure (win/loss vs margin)
- Model persistence (saving/loading trained agents)

### UI Architecture
- Event system for game→UI communication
- Input handling for card selection
- Animation/transition system

### Networking (Future Consideration)
- How will multiplayer work if added later?
- Server architecture for game state sync?

