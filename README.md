# Trick-Taking Card Game

## Project status (June 2026) — read this first

This repository contains **two implementations** of the same 4-player trick-taking game:

| Implementation | Where | Status |
|---|---|---|
| **Browser webapp (canonical)** | `index.html`, `engine.js`, `ui.js`, `styles.css` at the repo root | **Playable and tested.** This is the active version — all current development happens here. |
| Unity / C# scaffold | `Assets/Scripts/` | Paused. Complete logic architecture, but UI wiring requires Unity-Editor work (see `Documentation/MANUAL_TODO.md`). Kept as the long-term base for a possible Android port. |

**Where the rules disagree, the webapp + `Basic rules.txt` are authoritative.** Canonical tie rules: ties are resolved **twice** per round — on **Value** before the winner is determined, then on **Score** among the prize candidates after the winner is determined but before the prize is awarded. All value-tied cards are discarded whatever the tie size; on a 3-way tie the lone survivor **wins the round but takes no prize**; a 4-way tie or two 2-way ties means nobody wins; score-tied prize candidates are discarded and the next highest is taken. The leader rotates **one seat clockwise from the previous leader every round, regardless of the outcome** — who won and whether a prize was taken are irrelevant.

### Run the webapp

```powershell
npm run serve     # http://localhost:4173
```

### Run the tests

```powershell
npm install
npx playwright install chromium
npm test          # 110+ Playwright tests, see tests/README.md
```

---

# Unity Scaffold (paused)

The remainder of this document describes the **Unity/C# scaffold**.

## Overview

This is a complete architecture scaffold for a cross-platform trick-taking card game for 4 players. Built with **Unity + C#**, designed for both Windows desktop and future Android sideload.

**Current Status**: Logic architecture complete; UI wiring paused in favour of the JS webapp.

---

## Project Structure

```
Card Game/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                    # Game logic (platform-agnostic)
│   │   │   ├── Card.cs              # Card definition & scoring
│   │   │   ├── Deck.cs              # Deck management
│   │   │   ├── Hand.cs              # Player hand (Hidden/Shown)
│   │   │   ├── Player.cs            # Player state & scoring
│   │   │   ├── GameState.cs         # Central game state machine
│   │   │   └── GameRules.cs         # All rule validation & scoring
│   │   ├── AI/                      # AI implementations
│   │   │   ├── IPlayer.cs           # Player interface
│   │   │   ├── RuleBasedAI.cs       # Base AI class
│   │   │   ├── EasyAI.cs            # Easy difficulty
│   │   │   ├── MediumAI.cs          # Medium difficulty
│   │   │   ├── HardAI.cs            # Hard difficulty
│   │   │   └── AIFactory.cs         # AI instantiation
│   │   ├── Game/
│   │   │   └── GameManager.cs       # Game loop orchestration
│   │   └── Tests/
│   │       └── GameLogicTests.cs    # 40+ unit tests
│   ├── Scenes/                      # (To be created)
│   ├── Prefabs/                     # (To be created)
│   └── Resources/                   # (To be created)
└── Documentation/
    ├── DESIGN_DECISIONS.md          # Architecture rationale
    ├── GAME_LOGIC.md                # Complete game rules
    ├── FOLDER_STRUCTURE.md          # This structure explained
    └── NEXT_STEPS.md                # Prioritized work items
```

---

## Game Rules (Summary)

### Players & Setup
- 4 players, standard 52-card deck
- Each player dealt 13 cards into Hidden hand
- Game runs 13 rounds (13 tricks per player)

### Round Flow
1. **Reveal Phase**: Round starter chooses a player, and one random card from their Hidden hand is revealed → moves to their Shown hand
2. **Play Phase**: Players play cards (in order) from either Hidden or Shown hands
3. **Determination Phase**: Value-tied cards are discarded; the highest remaining **Value** wins the trick; the winner's own card is discarded; score-tied prize candidates are discarded; the winner takes the highest remaining **Score** as the prize
4. **Score Phase**: Advance the leader one seat clockwise and prepare for next round

### Card Scoring

**Card Value (Trick Determination - Hidden vs Shown matters):**
- Hidden hand: Face value (2-14)
- Shown hand: 2-10 get +2, J=13, Q=14, K=5, A=10

**Card Score (Prize Cards & Final Tally):**
- 2-10: Face value
- J=11, Q=12, K=13, A=14

### Tie-Breaking

Ties are checked at two separate points in the round.

**Value ties — before the winner is determined:**
- **2-card tie**: Discard tied Values, winner is highest remaining Value.
- **3-card tie**: The three tied cards are discarded; the remaining player wins the round, discards their own card, and takes no prize.
- **4-card tie** (or two separate 2-card ties): All discarded, no winner.

**Score ties — after the winner is determined, before the prize is awarded:**
- The non-winning survivors are the prize candidates, compared on **Score** (face value).
- Candidates that tie each other on Score are discarded and the next highest is taken; if every candidate is tied away, the winner takes no prize.

Neither outcome affects who leads next: the leader always moves one seat clockwise from the previous leader.

---

## Architecture Principles

### 1. **Pure Game Logic**
- All Core/ code is platform-agnostic C# with zero UI dependencies
- Fully testable without MonoBehaviour
- Runs identically on Windows and Android

### 2. **AI Abstraction (IPlayer Interface)**
```csharp
// All players implement this
interface IPlayer {
    int SelectPlayerToRevealFrom(GameState gameState);
    Card SelectCardToReveal(GameState gameState, int targetPlayerId);
    Card SelectCardToPlay(GameState gameState);
    void ObserveGameState(GameState gameState);
}
```
- Enables seamless swapping between rule-based and ML/RL AI
- GameManager doesn't care which implementation plays

### 3. **Difficulty Levels as Strategies**
- `EasyAI`: Random valid moves
- `MediumAI`: Basic strategy + discard tracking
- `HardAI`: Advanced prediction + card counting
- All extend `RuleBasedAI` abstract base

### 4. **Game State Machine**
```
Reveal → Play → Determine → Score → (next round or Complete)
```
- Explicit state tracking prevents rule violations
- Clear phase boundaries for UI/animation coordination

### 5. **Factory Pattern for Dependency Injection**
```csharp
IPlayer ai = AIFactory.CreateAI(playerId, Difficulty.Hard);
// Easy to swap for ML implementation later
```

---

## Current Implementation Status

### ✅ Complete
- [x] Card scoring (Hidden vs Shown)
- [x] Trick determination with tie-breaking
- [x] Hand management (Hidden/Shown separation)
- [x] Player state tracking and scoring
- [x] Game state machine
- [x] All game rule validation
- [x] AI abstraction layer
- [x] 3 difficulty levels (rule-based)
- [x] GameManager orchestration
- [x] 40+ unit tests covering all rules

### 🔄 In Progress (Next Phase)
- [ ] Unity project setup (.csproj, project settings)
- [ ] Basic UI (board layout, card displays)
- [ ] Input handling (human player moves)
- [ ] Event system (UI ↔ GameManager)
- [ ] Visual feedback (animations, highlights)

### 📋 Future
- [ ] ML/RL implementation (MLAIPlayer.cs)
- [ ] Android port
- [ ] Networking (optional)
- [ ] Save/Load system
- [ ] Replay system

---

## How to Use This Scaffold

### 1. **Run Unit Tests**
```
In Unity Editor → Test Runner → GameLogicTests
All 40+ tests should pass
```

### 2. **Inspect Game Logic**
- Start with [GAME_LOGIC.md](Documentation/GAME_LOGIC.md)
- Read [DESIGN_DECISIONS.md](Documentation/DESIGN_DECISIONS.md) for architecture
- Source code in `Assets/Scripts/Core/`

### 3. **Play an Automated Game**
```csharp
GameManager manager = new GameManager(new[] { 
    Difficulty.Easy, 
    Difficulty.Medium, 
    Difficulty.Hard, 
    Difficulty.Easy 
});
manager.RunCompleteGame();

var results = manager.GetFinalResults();
foreach (var (player, score) in results)
    Debug.Log($"{player.Name}: {score}");
```

### 4. **Extend AI Difficulty**
1. Create new class extending `RuleBasedAI`
2. Override `SelectTargetPlayerForReveal()`, `SelectCardForReveal()`, `SelectCardForPlay()`
3. Register in `AIFactory.CreateAI()`

### 5. **Integrate ML/RL Later**
1. Create `class MLAIPlayer : IPlayer`
2. Implement the 3 decision methods (use neural network inference)
3. No changes needed to GameManager or GameRules

---

## Key Classes Reference

| Class | Purpose |
|-------|---------|
| `Card` | Single card (suit/rank); scoring methods |
| `Deck` | 52-card deck management; shuffle/deal |
| `Hand` | Player hand (Hidden/Shown sections) |
| `Player` | Player state, hand, scoring zone |
| `GameState` | Central game state machine; phase tracking |
| `GameRules` | Static methods for validation & scoring |
| `IPlayer` | Interface all players implement |
| `RuleBasedAI` | Base class for rule-based AI |
| `EasyAI` / `MediumAI` / `HardAI` | Difficulty implementations |
| `AIFactory` | Creates AI instances by difficulty |
| `GameManager` | Orchestrates game loop & coordinates execution |

---

## Testing

### Run All Tests
```
Unity Editor → Window → General → Test Runner
Run: Assets/Scripts/Tests/GameLogicTests.cs
```

### Test Coverage
- ✅ Card scoring (Hidden vs Shown vs final tally)
- ✅ Hand management (add, reveal, play)
- ✅ Player scoring zone
- ✅ Game state transitions
- ✅ Trick determination (all tie scenarios)
- ✅ Rule validation
- ✅ Full game flow (13 rounds)

### Expected Results
All 40+ tests pass. Total score across all players = 260 (sum of all card values).

---

## Documentation Files

### [DESIGN_DECISIONS.md](Documentation/DESIGN_DECISIONS.md)
Why we made each architectural choice:
- Tech stack (Unity + C#)
- IPlayer abstraction
- Rule-based AI first, RL later
- Pure game logic layer
- State machine pattern
- Future considerations (RL, UI, networking)

### [GAME_LOGIC.md](Documentation/GAME_LOGIC.md)
Complete game rules in machine-readable format:
- Card scoring (detailed)
- Game flow (phase by phase)
- Edge cases
- Card movement rules
- Data structures
- Implementation guidelines

### [FOLDER_STRUCTURE.md](Documentation/FOLDER_STRUCTURE.md)
Guide to every file and folder:
- Purpose of each class
- Dependencies
- Key methods
- Naming conventions

### [NEXT_STEPS.md](Documentation/NEXT_STEPS.md)
Prioritized development roadmap:
- 9 high-priority tasks (phases 1-3)
- Medium-term improvements
- Known limitations
- Risk analysis
- Milestones

---

## Architecture Diagram

```
┌─────────────────────────────────────────┐
│         GameManager (Orchestrator)      │
│  - Game loop coordination               │
│  - Phase transitions                    │
│  - Event system                         │
└─────────────────────────────────────────┘
         ↓                    ↓
    ┌─────────────┐    ┌──────────────┐
    │  GameState  │    │  GameRules   │
    │  - Phase    │    │  - Scoring   │
    │  - Round    │    │  - Validation│
    │  - Players  │    │  - Win logic │
    │  - PlayZone │    └──────────────┘
    └─────────────┘            ↑
         ↓                      │
    ┌──────────────────────────┘
    ↓
┌────────────────────────────────────────┐
│         Player[] (4 Players)           │
│  each has: Hand + AI (IPlayer)        │
└────────────────────────────────────────┘
         ↓              ↓
    ┌────────┐    ┌─────────────────┐
    │  Hand  │    │  IPlayer (AI)   │
    │ Hidden │    │  - SelectCard() │
    │ Shown  │    │  - SelectPlayer │
    └────────┘    │  - Observe()    │
                  └─────────────────┘
                         ↑
           ┌─────────────┼──────────────┐
           ↓             ↓              ↓
      ┌─────────┐  ┌─────────┐   ┌──────────┐
      │ EasyAI  │  │MediumAI │   │ HardAI   │
      │(Random) │  │(Strategy│   │(Predict) │
      └─────────┘  │ +Track) │   └──────────┘
                   └─────────┘
                   
    Future: MLAIPlayer (RLalso IPlayer)
```

---

## Next Steps

### Immediate (This Sprint)
1. **Set up Unity project**
   - Create .csproj files
   - Configure project settings
   - Ensure all scripts compile

2. **Run unit tests**
   - Verify 100% pass rate
   - Measure code coverage

3. **Build basic UI**
   - Create game board scene
   - Display 4 hands
   - Show play zone & scoring zone

### Short-term (Sprint 2-3)
4. Connect GameManager to UI events
5. Implement human player input
6. Add animations & visual feedback
7. Playtest 50+ games

### Medium-term (Sprint 4-6)
8. Polish UI/UX
9. Add sound design
10. Prepare for Android port

### Long-term (Future)
11. Train RL agent (if desired)
12. Android sideload
13. Networking (if desired)

---

## Dependencies

- **C# 8.0+** (features used: switch expressions, nullable reference types)
- **NUnit** (for testing)
- **Unity 2020 LTS+** (for eventual UI/Android)

## No External Packages Required for Core Logic

All game logic uses only C# standard library. Zero external dependencies.

---

## Questions?

Refer to the [Documentation](Documentation/) folder:
1. Unsure about a design choice? → [DESIGN_DECISIONS.md](Documentation/DESIGN_DECISIONS.md)
2. Need to understand the rules? → [GAME_LOGIC.md](Documentation/GAME_LOGIC.md)
3. Lost in the file structure? → [FOLDER_STRUCTURE.md](Documentation/FOLDER_STRUCTURE.md)
4. What to build next? → [NEXT_STEPS.md](Documentation/NEXT_STEPS.md)

---

**Last Updated**: June 10, 2026
**Status**: Unity track paused — the browser webapp at the repo root is the active, canonical implementation
**Phase**: Architecture scaffold complete (100%); Unity UI wiring on hold
