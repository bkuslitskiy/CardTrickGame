# AI Decision Logic & Strategy

This document explains the behavioral choices and rules driving the three difficulty levels in the game.

## Easy AI (Random)
- **Reveal Target**: Uniform random from all valid opponents holding hidden cards.
- **Reveal Card**: Uniform random from the selected opponent's hidden hand.
- **Play Card**: Uniform random from its own valid playable cards.

## Medium AI (Aggressive Basic Strategy)
- **Reveal Target**: Targets the opponent with the *fewest* hidden cards. This forces them to play shown cards sooner, limiting their options and forcing transparency.
- **Reveal Card**: Indistinguishable, so it picks randomly.
- **Play Card**: Aggressively plays the highest scoring valid card in its hand to forcefully secure the trick, without saving resources for later rounds.

## Hard AI (Advanced Baiting & Disruption)
- **Reveal Target**: Identifies the player currently leading in points and explicitly targets them for a reveal to expose and disrupt their strategy.
- **Reveal Card**: Indistinguishable, so it picks randomly.
- **Play Card**:
  - **If Leading the Trick**: Plays a mid-to-high card (its second highest). This acts as "bait" to force opponents to burn their Kings and Aces to win.
  - **If Following**: 
    - Evaluates the current highest score in the play zone.
    - If it holds cards capable of beating that score, it plays the *lowest possible card* that still secures the win, maximizing efficiency.
    - If it cannot win the trick, it deliberately throws away its absolute lowest scoring card.