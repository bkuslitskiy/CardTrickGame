# Recommendations & Next Steps

## Immediate Priorities
- [ ] **Re-enable & Fix Tests**: The `DisabledTests` are crucial. They should be ported to a supported test framework (e.g., Unity Test Framework) and fixed.
- [ ] **AI Strategy Calibration**: Now that game logic is reliable, re-evaluate AI difficulty levels (Easy, Medium, Hard) to ensure they play optimally.
- [ ] **Performance Optimization**: Profile `DetermineTrickWinner`. The current dictionary lookups and sorting in every trick might be inefficient for large numbers of simulations (for RL).

## Architectural Improvements
- [ ] **Event System for Game State**: Instead of polling or complex coordination, use a more robust event-driven system to communicate `GameState` changes to the UI.
- [ ] **Refactoring Logic**: `GameRules.DetermineTrickWinner` is still quite complex. Consider decomposing it into sub-functions (e.g., `ScoreCards`, `IdentifyWinners`, `ProcessTies`).
- [ ] **Immutability Enforcement**: Further enhance `GameState` to be truly immutable, which would greatly simplify complex AI simulations.

## Documentation
- [ ] **Update Documentation**: Keep `GAME_LOGIC.md` and `DESIGN_DECISIONS.md` synchronized with the recent fixes to `GameState` and `GameManager`.
