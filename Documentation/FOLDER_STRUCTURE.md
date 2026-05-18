# Folder Structure and File Guide

## Project Organization

```
Card Game/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                    # Game logic (platform-agnostic)
│   │   ├── AI/                      # AI implementations
│   │   ├── Game/                    # Game management and coordination
│   │   └── Tests/                   # Unit tests
│   ├── Scenes/                      # Unity scenes (to be created)
│   ├── Prefabs/                     # UI prefabs (to be created)
│   └── Resources/                   # Card graphics, sounds (to be created)
├── Documentation/                   # All project documentation
└── ProjectSettings/                 # Unity project configuration
```

## Core Scripts (`Assets/Scripts/Core/`)

### Card.cs
**Purpose**: Represents a single playing card.
**Responsibilities**:
- Store suit and rank
- Calculate scoring values based on hand type (Hidden vs Shown)
- Provide card information (name, visual ID)
**Dependencies**: None (utility class)
**Key Methods**:
- `GetFaceValue()`: Returns base value (2-14)
- `GetHiddenHandScore()`: Returns score when played from Hidden hand
- `GetShownHandScore()`: Returns score when played from Shown hand

### Deck.cs
**Purpose**: Manages the deck of cards.
**Responsibilities**:
- Create standard 52-card deck
- Shuffle and deal cards
- Track remaining cards
**Dependencies**: Card.cs
**Key Methods**:
- `Shuffle()`
- `Deal(int count)`: Returns list of cards
- `GetRemainingCount()`: Returns cards left in deck

### Hand.cs
**Purpose**: Represents a player's hand divided into Hidden and Shown sections.
**Responsibilities**:
- Maintain separate Hidden and Shown card lists
- Manage card additions and removals
- Expose only appropriate information to observers
**Dependencies**: Card.cs
**Key Methods**:
- `AddToHidden(Card)`, `AddToShown(Card)`
- `RemoveCard(Card, HandSection)`: Returns true/false
- `GetHiddenCount()`, `GetShownCount()`
- `CanRevealFrom()`: Checks if Hidden hand has cards
- `GetVisibleCards()`: Returns Shown + own Hidden (for local player)

### Player.cs
**Purpose**: Represents a game player.
**Responsibilities**:
- Maintain player identity (ID, name)
- Manage hand and scoring zone
- Calculate final score
- Reference to AI implementation (if applicable)
**Dependencies**: Hand.cs, Card.cs
**Key Properties**:
- `ID`: 1-4
- `Hand`: Hand object
- `ScoringZone`: List of won cards
- `AIImplementation`: IPlayer (null for human)
**Key Methods**:
- `GetScore()`: Sum of scoring zone
- `CanPlay(Card)`: Validates card is in hand
- `PlayCard(Card)`: Removes and returns card

### GameState.cs
**Purpose**: Central game state machine.
**Responsibilities**:
- Track game phase (Reveal → Play → Determine → Score)
- Track current round (1-13)
- Maintain Play zone and all players
- Validate and execute state transitions
- Provide queries for AI decision-making
**Dependencies**: Player.cs, Card.cs
**Key Properties**:
- `CurrentPhase`: GamePhase enum
- `CurrentRound`: 1-13
- `Players`: List of 4 Player objects
- `PlayZone`: List of cards currently in play
- `RoundStarter`: Player reference
**Key Methods**:
- `AdvancePhase()`: Validates and moves to next phase
- `GetValidCards(Player)`: Returns cards they can play
- `GetWinnerOfRound()`: Returns winning Player after Determine phase
- `AddCardToPlayZone(Card, Player)`
- `TransitionToNextRound()`

### GameRules.cs
**Purpose**: Encapsulates all game rules and scoring logic.
**Responsibilities**:
- Calculate card scores based on hand type and rank
- Determine trick winner given 4 cards
- Handle tie-breaking logic
- Validate game state transitions
**Dependencies**: Card.cs, Player.cs, GameState.cs
**Key Methods**:
- `ScoreCard(Card, HandType)`: Returns score for a card
- `DetermineTrickWinner(Card[], Player[])`: Returns winning Player or null
- `IsValidCardPlay(Player, Card, GameState)`: Boolean validation
- `IsValidReveal(Player, TargetPlayer, Card)`: Boolean validation

---

## AI Scripts (`Assets/Scripts/AI/`)

### IPlayer.cs
**Purpose**: Interface all players implement (human, rule-based AI, ML AI).
**Responsibilities**: Define contract for player decisions
**Dependencies**: None (GameState passed as parameter)
**Key Methods**:
- `SelectCardToPlay(GameState)`: Returns Card to play during Play phase
- `SelectCardToReveal(GameState, TargetPlayer)`: Returns Card to reveal
- `SelectPlayerToReveal(GameState)`: Returns Player to reveal from

### RuleBasedAI.cs
**Purpose**: Base implementation of rule-based AI decision-making.
**Responsibilities**:
- Implement core strategic logic
- Delegate difficulty-specific behavior to strategy objects
- Track game history (for Medium/Hard difficulty)
**Dependencies**: IPlayer.cs, GameState.cs
**Key Methods**:
- `SelectCardToPlay()`: Uses strategy to decide
- `SelectCardToReveal()`: Uses strategy to decide
- Protected: `EvaluateCard()`, `EvaluatePlayer()`

### EasyAI.cs
**Purpose**: Easy difficulty AI opponent.
**Responsibilities**:
- Play random valid cards
- Reveal random valid cards
- Minimal strategic thinking
**Dependencies**: RuleBasedAI.cs
**Strategy**:
- Card selection: Uniform random from valid plays
- Card reveal: Uniform random from Hidden hand
- Player reveal: Uniform random from valid players

### MediumAI.cs
**Purpose**: Medium difficulty AI opponent.
**Responsibilities**:
- Preferentially play high-value cards to win tricks
- Track card discards to estimate remaining cards
- Reveal cards strategically to avoid giving opponents high cards
**Dependencies**: RuleBasedAI.cs
**Strategy**:
- Card selection: Prefers highest-scoring card when ahead, lowest when behind
- Card reveal: Avoid revealing high-value cards
- Player reveal: Target player with fewest Hidden cards

### HardAI.cs
**Purpose**: Hard difficulty AI opponent.
**Responsibilities**:
- Attempt to predict outcomes
- Card counting and probability estimation
- Strategic reveals based on opponent analysis
- Optimize for both winning and score margin
**Dependencies**: RuleBasedAI.cs, GameState.cs
**Strategy**:
- Card selection: Estimate win probability, play accordingly
- Card reveal: Reveal to disrupt opponent predictions
- Player reveal: Select to maximize information gained

### MLAIPlayer.cs (Placeholder)
**Purpose**: Future ML/RL implementation.
**Responsibilities**: To be defined during RL implementation phase
**Current Status**: Stub interface implementation
**Future Dependencies**: TensorFlow/Barracuda, training environment

---

## Game Management (`Assets/Scripts/Game/`)

### GameManager.cs
**Purpose**: Orchestrates the complete game loop and phase transitions.
**Responsibilities**:
- Coordinate between GameState, AI, and UI
- Manage phase flow (Reveal → Play → Determine → Score)
- Emit events for UI updates
- Handle human player input requests
**Dependencies**: GameState.cs, IPlayer.cs, GameRules.cs
**Key Methods**:
- `StartGame(difficultyLevel)`
- `ExecutePhase()`: Performs actions for current phase
- `OnPlayerInput(Card)`: Receives human player moves
- `AdvanceRound()`

### AIFactory.cs
**Purpose**: Centralized creation of AI players.
**Responsibilities**:
- Instantiate correct AI based on difficulty
- Manage AI configuration
**Dependencies**: IPlayer.cs, EasyAI.cs, MediumAI.cs, HardAI.cs
**Key Methods**:
- `CreateAI(Difficulty)`: Returns IPlayer instance
- `GetAvailableDifficulties()`: Returns list of supported difficulties

---

## Tests (`Assets/Scripts/Tests/`)

### GameLogicTests.cs
**Purpose**: Comprehensive unit tests for game logic.
**Coverage**:
- Card scoring (Hidden vs Shown hands)
- Trick determination and tie-breaking
- Hand management and card validity
- Game state transitions
- AI decision-making consistency
**Framework**: NUnit (Unity's built-in testing framework)

### [Additional test files to be created as coverage grows]

---

## Documentation (`Documentation/`)

### DESIGN_DECISIONS.md
All architectural decisions and their rationale.

### GAME_LOGIC.md
Complete game rules in machine-readable format.

### FOLDER_STRUCTURE.md
This file. Guide to project organization.

### NEXT_STEPS.md
Prioritized work items and future improvements.

---

## File Naming Conventions

- **Classes**: PascalCase (e.g., `GameManager.cs`, `RuleBasedAI.cs`)
- **Interfaces**: PascalCase prefixed with `I` (e.g., `IPlayer.cs`)
- **Methods**: PascalCase (e.g., `SelectCardToPlay()`)
- **Private fields**: _camelCase (e.g., `_playerScore`)
- **Public properties**: PascalCase (e.g., `CurrentPhase`)

