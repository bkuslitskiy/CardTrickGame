# Debug Log

This file tracks identified bugs, investigations, and fixes applied to the Card Game project.

## [2026-05-09] Initial Investigation

### Identified Issues:
1.  **[FIXED] Convoluted Origin Logic**: `GameRules.DetermineTrickWinner` had extremely complex and redundant logic for determining if a card came from a Hidden or Shown hand.
2.  **[FIXED] Origin Tracking Bug**: `GameState.AddCardToPlayZone` accepted a `HandSection` parameter but failed to store it, making it impossible to correctly score cards once they were removed from a player's hand.
3.  **Disabled Tests**: `Documentation/DisabledTests/GameLogicTests.cs` suggests known issues or incomplete testing.
4.  **Potential Logic Bug in Tie-Breaking**: The 3-card tie logic in `GameRules.cs` seems to award the win to the 4th player but gives them "no prize", which might not be intended or correctly implemented if the prize card is meant to be the 4th card itself.

*   Updated `GameState.cs` to include `_playZoneSections` dictionary to track `HandSection` for cards in play.
*   Added `GetCardSectionInPlayZone` and `GetPlayZoneWithSections` to `GameState.cs`.
*   Updated `GameRules.DetermineTrickWinner` to use `GameState.GetCardSectionInPlayZone` for both winner determination and prize calculation, significantly simplifying the logic and fixing the scoring bug.
*   Updated `GameManager.cs` to capture `HandSection` *before* playing the card, fixing the source tracking bug.
*   Updated `GameManager.ExecuteDeterminePhase` to use `GameState.GetPlayOrder` instead of `GameState.GetAllPlayers`, ensuring trick determination correctly maps played cards to the players in the correct turn order.
*   Fixed game hang during human turns: `HumanPlayer` now correctly notifies `GameManagerController` via `OnHumanPlayerMoveConfirmed` after a player makes a selection.
*   Updated `HumanPlayer` constructor to accept and store a `GameManagerController` reference.

---
