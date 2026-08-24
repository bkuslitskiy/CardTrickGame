# Interactive Game Flow

This document outlines the exact sequence of events, user expectations, and underlying function calls during the interactive portion of the game loop.

## 1. Game Start & Setup
- **What happens:** The `GameManager` initializes the `GameState`, shuffles the deck, and deals 13 cards to each of the 4 players' Hidden hands.
- **Under the hood:** `GameManagerController` emits the `OnRoundStarted` event. `GameBoardUIEnhanced` catches this, updates the round text, and calls `RefreshAllPlayerHands()` to visually spawn the cards on screen.
- **Human expectation:** None yet. Watch the UI populate.

## 2. Reveal Phase
- **What happens:** The round starter must select an opponent to reveal a card from their Hidden hand.
- **Expected Input:** Human clicks on one of the 3 opponent player panels.
- **Waiting Function:** `InputManagerEnhanced.RequestPlayerSelection()` sets `awaitingPlayerInput = true` and tells `UIManager` to highlight valid opponents.
- **Action:** The UI click triggers `InputManagerEnhanced.OnPlayerClicked(player)`. The input manager validates the choice, ends the waiting state, and invokes the callback back to the game logic. 
- **Automated Step:** Immediately after the opponent is chosen, the game logic automates the card selection (since hidden cards are indistinguishable). `InputManagerEnhanced.RequestRevealSelection()` automatically picks a random card from the chosen opponent's hidden hand and passes it back to the game state. `GameBoardUIEnhanced.DisplayRevealedCard()` flips that card face-up into the Shown hand visually.

## 3. Play Phase
- **What happens:** Players take turns playing one card into the center Play Zone.
- **Expected Input:** Human clicks on any valid card in their own hand (either Hidden or Shown) to play it.
- **Waiting Function:** When it is the human's turn, their `IPlayer` implementation calls `InputManagerEnhanced.RequestCardSelection()`. This sets `awaitingCardInput = true` and tells `GameBoardUIEnhanced.EnableCardSelection()` to make the cards in the human's hand interactable buttons.
- **Action:** Clicking the card prefab fires `InputManagerEnhanced.OnCardClicked(card)`. If valid, the state clears, UI interactability is canceled, and the card is passed to `GameManager`. `GameBoardUIEnhanced.DisplayCardPlayed()` moves a visual copy of the card to the Play Zone, and the hand refreshes to remove the played card.

## 4. Determination & Scoring Phase
- **What happens:** Once 4 cards are in the Play Zone, the game calculates the winner based on the scoring rules and ties. Ties resolve twice: **Value** ties eliminate cards before the winner is determined, then **Score** ties eliminate prize candidates after the winner is determined but before the prize is awarded.
- **Expected Input:** None.
- **Action:** `GameState.AdvancePhase()` evaluates the trick. `GameManagerController` emits `OnTrickWon`. `GameBoardUIEnhanced.DisplayTrickWinner()` triggers a visual highlight on the winner's panel, spawns floating score text, updates the overall score texts, and then clears the Play Zone after a short delay.

## 5. Next Round / Game Over
- **What happens:** The starter token moves one seat clockwise from the previous leader — every round, regardless of who won the trick or whether a prize was taken — and the next Reveal Phase begins. This repeats until all 13 rounds are completed and hands are empty.
- **Action:** `GameManagerController` emits `OnGameComplete` and displays the final results menu.