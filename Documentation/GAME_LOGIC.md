# Game Logic Documentation

## Overview

This document defines the complete game logic for the trick-taking card game. It serves as a reference for implementation and testing.

## Card Scoring

### Card Value (Determining Trick Winner)

Value is calculated based on which hand the card was played from:

**From Hidden Hand:**
- 2-10: Face value
- J: 11
- Q: 12
- K: 13
- A: 14

**From Shown Hand:**
- 2-10: Face value + 2
- J: 13
- Q: 14
- K: 5
- A: 10

### Card Score (Prizes & Final Tally)

Score is always the base face value, used for awarding prizes and the final win condition:
- 2-10: Face value
- J: 11
- Q: 12
- K: 13
- A: 14

## Game Flow

### Setup
1. Shuffle standard 52-card deck
2. Deal 13 cards to each of 4 players into their Hidden hand
3. Each player has empty Shown hand and empty scoring zone
4. Player 1 becomes the round starter

### Each Round (13 rounds total)

#### Phase 1: Reveal
- Current round starter selects a target player (must have cards in Hidden hand)
- A random card is automatically selected from the target player's Hidden hand (as all hidden cards are indistinguishable to the starter)
- Card is revealed and moved to target player's Shown hand (visible to all)
- Edge case: If all players have empty Hidden hands, no reveal occurs

#### Phase 2: Play
- Round starter plays a card from their hand (Hidden or Shown)
- Player 2 plays a card from their hand (Hidden or Shown)
- Player 3 plays a card from their hand (Hidden or Shown)
- Player 4 plays a card from their hand (Hidden or Shown)
- No suit-following requirement
- All 4 cards now in Play zone

#### Phase 3: Determination
1. **Calculate Value** for all 4 cards based on their origin (Hidden vs Shown).
2. **Identify winner(s)**:
   - If 3 or 4 cards tie in Value, those cards are discarded.
   - If 2 cards tie in Value, those cards are discarded.
   - From the remaining untied cards, the one with the highest Value wins the trick.
3. **Award cards**:
   - If winner exists: They take the remaining card with the highest **Score** to their scoring zone. If potential prize cards tie in **Score**, they are discarded and the next highest is taken.
   - All other cards discarded
   - If no winner (all four cards eliminated by ties — a 4-way tie or two 2-way ties): All cards discarded, no one scores

4. **Advance round starter**: Move counterclockwise from the round's winner — **only if a prize was taken**. If no prize was taken (no winner, or the winner's prize pool was emptied — e.g. the 3-way-tie survivor, or all prize candidates score-tied away), the same player leads the next round.

### End of Game
- All 13 rounds complete, all hands empty
- Each player tallies their scoring zone using the **Score** of each card.
- Highest total wins

## Edge Cases

### Hidden Hand Exhaustion
- If all players have 0 cards in Hidden hand before reveal phase, no card is revealed that round
- Play phase proceeds as normal with only Shown hand cards available

### Ties in Determination
- **2-card tie**: Tied cards discarded, remaining 2 cards rescore
  - Example: [10, 10, 5, 3] → discard 10s → winner is 5, takes 3
- **3-card tie**: All 3 tied cards discarded
  - Example: [10, 10, 10, 3] → winner is 3, but must discard their card (0 prize)
- **4-card tie**: All cards discarded, no one wins round

### Empty Play Zone
- Should not occur given rules, but: If somehow only 1 card in play zone, that player wins by default

## Card Movement Rules

**Card states:**
1. Deck (face-down, undealt)
2. Hidden Hand (private to player)
3. Shown Hand (visible to all)
4. Play Zone (during round)
5. Scoring Zone (won cards)
6. Discarded (removed from game)

**Valid transitions:**
- Deck → Hidden Hand (during setup only)
- Hidden Hand → Shown Hand (reveal phase)
- Hidden Hand → Play Zone (play phase)
- Shown Hand → Play Zone (play phase)
- Play Zone → Scoring Zone (if winner)
- Play Zone → Discarded (if not winner)
- Play Zone → Shown Hand (if revealed, not played) ✓ stays in Shown
- Shown Hand → Shown Hand (remains visible throughout game)

## Data Structures

### Card
- Suit: Clubs, Diamonds, Hearts, Spades
- Rank: 2-10, J, Q, K, A
- Methods: GetFaceValue(), GetHiddenHandScore(), GetShownHandScore()

### Hand
- Hidden: List of cards (private)
- Shown: List of cards (public)
- Methods: AddToHidden(), AddToShown(), RemoveCard(), HasCards()

### Player
- ID: 1-4
- Hand: Hidden + Shown sections
- Scoring Zone: List of won cards
- Methods: PlayCard(), GetScore(), CanRevealFrom()

### GameState
- CurrentRound: 1-13
- CurrentPhase: Reveal, Play, Determine, Score
- PlayZone: 4 cards (max)
- Players: 4 Player objects
- Discards: All discarded cards (for RL training)
- Methods: AdvancePhase(), GetValidMoves(), GetWinner()

## Implementation Guidelines

### Immutability
- Card and GameState objects should be immutable or pseudo-immutable
- Enables easier RL state snapshots and testing

### Validation
- GameState validates all transitions
- AI cannot perform invalid moves (enforced at interface level)
- Unit tests verify rule enforcement

### Hidden Information
- GameState stores full knowledge
- IPlayer.SelectCard() receives only what that player can see
- AI must deduce hidden cards from game history
