# UI Implementation Guide

## Overview
This document describes the complete UI architecture for the card game, including scene structure, prefabs, input handling, and integration with the core game logic.

## Architecture Diagram

```
┌──────────────────────────────────────────────────────────┐
│                    Main Menu Scene                       │
│  ├─ MenuUI (Button handlers)                            │
│  └─ Difficulty selection → Start Game                   │
└──────────────────────────────┬───────────────────────────┘
                               │
                               ↓
┌──────────────────────────────────────────────────────────┐
│                   GameBoard Scene                        │
│  ├─ GameBootstrapper (Init & Setup)                     │
│  ├─ GameManagerController (Core logic bridge)           │
│  ├─ InputManager (Human input handling)                 │
│  ├─ UIManager (Event coordination)                      │
│  └─ GameBoardUIEnhanced (Visual updates)                │
│      ├─ HandDisplay[4] (Player hands)                   │
│      ├─ PlayZone (Card display area)                    │
│      └─ ScorePanel (Score tracking)                     │
└──────────────────────────────────────────────────────────┘
```

## Scenes

### MainMenu Scene
**Purpose**: Start screen with difficulty selection

**Components**:
- `MenuUI` script - Handles button clicks
- 5 Buttons:
  - Easy (all players Easy AI)
  - Medium (all players Medium AI)
  - Hard (all players Hard AI)
  - Mixed (Easy/Medium/Hard/Easy)
  - Quit

**Flow**:
1. Player selects difficulty
2. `MenuUI.StartGameWithDifficulty()` calls `GameBootstrapper.SetGameDifficulties()`
3. Scene transitions to GameBoard

### GameBoard Scene
**Purpose**: Main game play area

**Hierarchy**:
```
GameBoard/
├─ GameManager (GameObject)
│  └─ GameManagerController (Component)
├─ Canvas (UI Root)
│  ├─ UIManager (Component)
│  ├─ GameBoardUIEnhanced (Component)
│  ├─ GameBootstrapper (Component)
│  ├─ InputManager (Component)
│  ├─ Background (Image)
│  ├─ TopUI
│  │  ├─ PhaseDisplay (Text)
│  │  └─ RoundDisplay (Text)
│  ├─ PlayZone (Container for card displays)
│  ├─ Player0Hand (HandDisplay) - Bottom
│  ├─ Player1Hand (HandDisplay) - Left
│  ├─ Player2Hand (HandDisplay) - Top
│  ├─ Player3Hand (HandDisplay) - Right
│  └─ ScoresPanel (Text displays for scores)
```

## Prefabs

### CardDisplay.prefab
**Purpose**: Visual representation of a single card

**Components**:
- `Image` - Card background/image
- `Button` - Clickable interaction
- `CardDisplay` script - Handles card data and interaction

**Features**:
- Display card rank and suit
- Show card back for hidden cards
- Clickable for selection
- Highlight on hover

**Usage**:
```csharp
CardDisplay card = Instantiate(Resources.Load<CardDisplay>("Prefabs/CardDisplay"));
card.SetCard(myCard);
card.MakeClickable(() => OnCardSelected(myCard));
```

### HandDisplay.prefab
**Purpose**: Display a player's hand (hidden + shown cards)

**Structure**:
```
HandDisplay
├─ HiddenHandContainer (shows card backs)
└─ ShownHandContainer (shows face-up cards)
```

**Features**:
- Separate visible and hidden sections
- Automatic card display updates
- Card selection handling

**Usage**:
```csharp
HandDisplay hand = GetComponent<HandDisplay>();
hand.RefreshDisplay(player.Hand);
hand.EnableCardSelection(callback);
```

## Scripts

### UIManager.cs
**Role**: Central event coordinator between GameManager and UI elements

**Responsibilities**:
- Subscribe to GameManager events
- Broadcast phase/score updates to UI
- Request input from human players
- Manage UI state

**Key Methods**:
- `NotifyPhaseChanged(GamePhase)`
- `NotifyScoreUpdated(playerId, score)`
- `NotifyCardPlayed(card, player)`
- `RequestCardSelection(player, callback)`

### GameManagerController.cs
**Role**: Unity wrapper for core GameManager

**Responsibilities**:
- Bridge between GameManager and UI event system
- Expose game state to UI
- Handle phase execution
- Manage game lifecycle

**Key Methods**:
- `InitializeGame(difficulties[])`
- `StartGame()`
- `ExecutePhase()`
- `RunCompleteGame()` (for testing)

**Events**:
- `OnPhaseChanged(GamePhase)`
- `OnRoundStarted(int)`
- `OnCardPlayed(Card, int playerId)`
- `OnTrickWon(Player, Card)`
- `OnGameComplete()`

### GameBoardUIEnhanced.cs
**Role**: Main UI update handler

**Responsibilities**:
- Update phase/round displays
- Display cards in play zone
- Update player scores
- Handle animations
- Enable/disable input controls

**Key Methods**:
- `UpdatePhaseDisplay(GamePhase)`
- `DisplayCardPlayed(Card, playerId)`
- `DisplayTrickWinner(Player, Card)`
- `EnableCardSelection(Player, callback)`
- `RefreshAllHands(Player[])`

### HandDisplay.cs
**Role**: Individual player hand display

**Responsibilities**:
- Display hidden and shown cards
- Enable/disable card selection
- Refresh based on game state
- Handle click events

**Key Methods**:
- `RefreshDisplay(Hand)`
- `EnableCardSelection(callback)`
- `EnableRevealSelection(callback)`
- `DisableCardSelection()`

### CardDisplay.cs
**Role**: Individual card visual

**Responsibilities**:
- Display card information
- Handle click interaction
- Show card back for hidden cards
- Manage visual state

**Key Methods**:
- `SetCard(Card)`
- `SetCardBack()`
- `MakeClickable(callback)`
- `MakeUnclickable()`
- `Highlight()` / `RemoveHighlight()`

### InputManager.cs
**Role**: Handle human player input

**Responsibilities**:
- Request card selection
- Request player selection
- Manage input state
- Forward input to GameManager

**Key Methods**:
- `RequestCardInput(Player, callback)`
- `RequestPlayerInput(Player, callback)`
- `RequestRevealInput(Player, callback)`
- `CancelInput()`

### GameBootstrapper.cs
**Role**: Scene initialization and game startup

**Responsibilities**:
- Initialize GameManager on scene load
- Set game difficulties
- Handle scene transitions
- Manage game startup flow

**Key Methods**:
- `InitializeGame()`
- `SetGameDifficulties(Difficulty[])`
- `GetGameDifficulties()`

### UIEnums.cs
**Role**: UI-specific enums

**Contains**:
- `GamePhase` - Game states
- `Difficulty` - AI difficulty levels
- `Suit`, `Rank` - Card properties
- `HandSection` - Hidden vs Shown

## Event Flow Examples

### Example 1: Playing a Card

```
1. GamePhase = Play
2. GameBoardUIEnhanced.UpdatePhaseDisplay("PHASE: Play")
3. InputManager.RequestCardInput(humanPlayer, callback)
4. Human clicks card in HandDisplay
5. CardDisplay.OnClick() fires
6. InputManager.OnCardClicked() invokes callback
7. GameManagerController receives Card selection
8. GameManager.ExecutePlayPhase() places card in play zone
9. GameManagerController.OnCardPlayed fires
10. GameBoardUIEnhanced.DisplayCardPlayed() shows card in play zone
```

### Example 2: Game Complete

```
1. GameManagerController.ExecutePhase() detects game complete
2. GameManagerController.OnGameComplete fires
3. GameBoardUIEnhanced.DisplayGameEnd() shows results
4. Debug logs final scores
5. (Future: Show end-game UI, option to play again)
```

## Integration Checklist

- [x] Create UI scenes (MainMenu, GameBoard)
- [x] Create prefabs (CardDisplay, HandDisplay)
- [x] Implement UIManager for event coordination
- [x] Implement GameManagerController wrapper
- [x] Implement InputManager for human input
- [x] Implement HandDisplay for card display
- [x] Implement CardDisplay for individual cards
- [x] Implement GameBootstrapper for scene init
- [ ] Test menu → game flow
- [ ] Test card selection
- [ ] Test score updates
- [ ] Test game completion
- [ ] Add animations/polish
- [ ] Test Android input

## Next Steps After UI Scaffolding

### Immediate (Step 7: Input Handling)
1. Test card selection in Play phase
2. Test reveal target selection in Reveal phase
3. Test human player vs AI opponents
4. Add input validation (prevent invalid moves)

### Short-term (Step 8: Visual Feedback)
1. Add card play animations
2. Add trick winner highlight
3. Add score update animations
4. Add phase transition effects

### Medium-term (Polish)
1. Add sound effects
2. Add background music
3. Improve UI/UX styling
4. Add accessibility features

## Troubleshooting

### Cards not appearing
- Verify `CardDisplay` prefab exists at `Resources/Prefabs/CardDisplay`
- Check `Canvas` has `Graphic Raycaster` component
- Verify `HandDisplay` containers are set

### Input not working
- Check `InputManager.Instance` is initialized
- Verify callbacks are being invoked
- Check `Button` components are interactable

### Game not starting
- Verify `GameManagerController.Instance` exists in scene
- Check difficulties are set before `StartGame()`
- Look for errors in console

### Scores not updating
- Verify `OnTrickWon` event is firing
- Check `playerScoreTexts` array is populated
- Verify score calculation in core GameManager

## Testing Checklist

```csharp
// Test 1: Menu → Game transition
// Click "Mixed" button, verify GameBoard scene loads

// Test 2: Card display
// Verify 4 hands visible, cards displayed correctly

// Test 3: Play phase input
// Verify human can select and play cards

// Test 4: AI plays correctly
// Verify AI opponents play valid cards each turn

// Test 5: Scoring
// Verify scores update after each trick

// Test 6: Game completion
// Verify game ends after 13 rounds, winner announced
```

---

**Last Updated**: May 5, 2026
**Status**: UI Scaffolding Complete
**Next Phase**: Input Handling & Testing
