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

Determination resolves ties **twice**: once on **Value**, before the winner is
known, and once on **Score**, after the winner is known but before the prize is
awarded. The two stages are independent — a card eliminated by neither is a
prize candidate.

1. **Calculate Value** for all 4 cards based on their origin (Hidden vs Shown).
2. **Value ties — resolved before the winner is determined**:
   - Every card that shares its Value with another card is discarded, whatever
     the size of the tie (2-, 3-, or 4-way), and even if the tie is not for the
     highest Value.
   - 2 tied → both discarded, 2 cards remain. 3 tied → all three discarded, 1
     card remains. 4 tied, or two separate 2-way ties → nothing remains.
3. **Identify the winner**:
   - From the remaining untied cards, the one with the highest Value wins the trick.
   - If nothing remains (a 4-way tie, or two 2-way ties): no winner, all cards
     discarded, no one scores.
4. **Score ties — resolved after the winner is determined, before the prize is
   awarded**:
   - The winner discards the card they played. The cards still remaining are the
     **prize candidates**.
   - Every candidate that shares its **Score** with another candidate is
     discarded, and the next highest is taken.
   - If no candidates survive (the 3-way-tie survivor has none to begin with, or
     every candidate was score-tied away), the winner takes no prize.
5. **Award the prize**: the winner takes the surviving candidate with the highest
   **Score** into their scoring zone. All other cards are discarded.

6. **Advance round starter**: move **one seat clockwise from the previous round's
   leader**, every round, **regardless of the outcome**. Who won the trick and
   whether a prize was taken have no effect on who leads next — the leader is
   simply whoever played second in the round just finished, so over any four
   rounds each player leads exactly once.

### End of Game
- All 13 rounds complete, all hands empty
- Each player tallies their scoring zone using the **Score** of each card.
- Highest total wins

## Edge Cases

### Hidden Hand Exhaustion
- If all players have 0 cards in Hidden hand before reveal phase, no card is revealed that round
- Play phase proceeds as normal with only Shown hand cards available

### Value Ties (before the winner is determined)
- **2-card tie**: Tied cards discarded, remaining 2 cards rescore
  - Example: [10, 10, 5, 3] → discard 10s → winner is 5, takes 3
- **3-card tie**: All 3 tied cards discarded
  - Example: [10, 10, 10, 3] → winner is 3, but must discard their card (0 prize)
- **4-card tie** (or two separate 2-card ties): All cards discarded, no one wins round

### Score Ties (after the winner is determined, before the prize is awarded)
- Only the non-winning survivors — the prize candidates — are compared, and on
  **Score** (face value), not Value.
- Candidates that tie each other on Score are discarded and the next highest is taken.
  - Example: A♥ Hidden (Value 14) wins; the candidates are K♠ Hidden (Value 13,
    Score 13), K♦ Shown (Value 5, Score 13) and 7♣ Hidden (Value 7, Score 7).
    The two Kings did not tie in Value — 13 vs 5 — so both survived the Value
    step, but they tie on Score, so both are discarded and the prize is 7♣.
- If every candidate is discarded this way, the winner wins the round and takes
  no prize.

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
