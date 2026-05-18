# UI Functionality Trace

This document traces the complete lifecycle of the Unity UI, tracking how user input flows into the platform-agnostic C# core logic, and how the core logic renders back to the screen.

### 1. Program Loading & Bootstrapping (`SceneBootstrapper.cs`)
*   **Trigger**: The Unity Play button is pressed.
*   **Action**: The `[RuntimeInitializeOnLoadMethod]` attribute fires automatically. `SceneBootstrapper.cs` checks if a Canvas exists. Since the scene is empty, it programmatically generates the entire UI hierarchy.
*   **Generation**: It creates the `EventSystem`, the `Canvas`, the 4 `HandDisplay` panels, the center `PlayZoneContainer`, and attaches global managers (`GameManagerController`, `GameLoopManager`, `UIManager`).
*   **Result**: The Game Board is hidden, and the dynamically generated Main Menu is displayed.

### 2. Main Menu Interaction (`UIManager.cs` / `MenuUI.cs`)
*   **Trigger**: The user clicks "Auto Play (4 AI)" or "Play (1 Human vs 3 AI)".
*   **Action**: The `onClick` listener bound in the bootstrapper fires.
*   **Routing**: 
    1. Calls `GameManagerController.InitializeGame()`, passing the requested difficulties and the human player ID (-1 for Auto, 0 for Human).
    2. Calls `GameLoopManager.StartGameLoop()`.
    3. Calls `UIManager.Instance.HideMenu()`, which disables the Menu GameObjects and makes the GameBoard visible.

### 3. Game Loop & Phase Execution (`GameLoopManager.cs`)
*   **Trigger**: The `StartGameLoop()` coroutine starts ticking every 1.5 seconds.
*   **Action**: Every tick, the loop verifies the game is not paused (`!IsGamePaused`) and calls `GameManagerController.ExecutePhase()`.
*   **Backend Execution**: The Controller delegates the command to your pure C# `GameManager`, which calculates the next AI move or advances the state machine (e.g., `Reveal` → `Play` → `Determine`).

### 4. Human Input Interception (`GameManagerController.cs`)
*   **Trigger**: Before the backend executes a phase, `PrepareForPhaseIfHuman()` checks if it is currently the Human Player's turn.
*   **Action**: If human input is required, the Controller flags `waitingForHumanInput = true`, which temporarily halts the `GameLoopManager` ticks.
*   **Routing**: The Controller requests input via `InputManagerEnhanced.RequestCardSelection()`. This calls `GameBoardUI.EnableCardSelection()`, which highlights valid cards in green and attaches `onClick` listeners to the `CardDisplay` prefabs.
*   **Resolution**: When the user clicks a valid card, `OnHumanPlayerMoveConfirmed()` fires, lifting the pause flag and resuming the `ExecutePhase()` loop.

### 5. Visual Rendering & Animations (`GameBoardUI.cs`)
*   **Trigger**: As the backend `GameManager` processes rules, it fires pure C# events (e.g., `OnCardPlayed`, `OnPhaseChanged`).
*   **Action**: `GameManagerController` intercepts these C# events and invokes Unity Actions. `GameBoardUI` is subscribed to these actions.
*   **Rendering**: 
    *   **Text**: Phase and Round texts are updated immediately.
    *   **Cards**: When `OnCardPlayed` fires, `GameBoardUI` instantiates a fallback UI card via `PrefabFactory`.
    *   **Animations**: It passes the instantiated card to `AnimationController.AnimateCardPlay()`, which uses a coroutine to smoothly slide the card from the player's hand into the center `PlayZoneContainer`.

### 6. Final Score Tallying (`GameBoardUI.cs`)
*   **Trigger**: The 13th round concludes and the backend state machine reaches `GamePhase.Complete`.
*   **Action**: `gameManager.OnGameComplete` is fired and caught by the Controller.
*   **Rendering**: `GameBoardUI.DisplayGameResults()` executes. It programmatically generates a dark, semi-transparent overlay covering the screen, prints the final tally by querying `player.GetScore()`, and creates a "Main Menu" button.
*   **Reset**: Clicking "Main Menu" stops the game loop, destroys the overlay, and calls `UIManager.ShowMenu()` to restart the process.