# Setup Instructions - Card Game Project

## After Opening in Unity Editor

### 1. Fix Test Framework (Optional - for tests)

The project has some test files that require NUnit. To enable them:

1. **Window → TextMesh Pro → Import TMP Essential Resources** (if prompted)
2. **Window → General → Package Manager**
3. In Package Manager, search for **"Test Framework"**
4. Click on **"Unity Test Framework"** and click **Install**
   - This will add NUnit support automatically

If you don't need tests for now, you can skip this and just delete:
- `Assets/Scripts/Tests/GameLogicTests.cs`

### 2. Verify All Scenes Are Set Up

1. Go to **File → Build Settings**
2. Add both scenes:
   - `Assets/Scenes/MainMenu.unity`
   - `Assets/Scenes/GameBoard.unity`
3. MainMenu should be Scene 0 (top)

### 3. Play the Game

1. Go to **Assets/Scenes** folder
2. Double-click **MainMenu.unity** to open it
3. Press **Play** (▶ button) or Ctrl+P

### 4. Common Issues

**"Assets not found" / missing GameManager etc:**
- Reload the project: File → Close Scene → File → Open Scene
- Or restart Unity entirely

**Still getting compile errors:**
- Assets → Reimport All (bottom of File menu)
- This will rebuild all scripts

## Controls

- **Click cards** to play them (only valid cards are clickable)
- **Click player panels** to select reveal targets
- **Green highlight** = valid card to play
- **Gray** = invalid card (can't click)

## Next Development Steps

- Sound Design (Step 10)
- Android Export (Step 11)  
- ML/RL Training (Step 12)
