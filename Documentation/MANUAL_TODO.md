# Manual To-Do Items

These tasks require action inside the **Unity Editor** or the file system directly.
They cannot be automated from code and were left out of the scripted fix pass.

---

## 1 — Delete stray script files (Bugs 9 & 10)

Two `.cs` files sitting outside `Assets/Scripts/` were blanked out and replaced
with deprecation comments. The actual files must be deleted via the Unity
**Project** panel so Unity also removes their associated `.meta` files. Deleting
from Windows Explorer leaves orphan `.meta` files behind and produces import
warnings.

| File | Reason |
|---|---|
| `Assets/Prefabs/GameBoardUI.cs` | Stray duplicate — real MonoBehaviour is `Assets/Scripts/UI/GameBoardUIEnhanced.cs` |
| `Assets/Prefabs/AnimationController.cs` | Stray duplicate — real MonoBehaviour is `Assets/Scripts/UI/AnimationController.cs` |

**Steps:**
1. Open Unity → Project panel
2. Navigate to `Assets/Prefabs/`
3. Right-click `GameBoardUI.cs` → **Delete**
4. Right-click `AnimationController.cs` → **Delete**
5. Confirm Unity removes the `.meta` files automatically

---

## 2 — Clean up scenes after GameBoardUI.cs class removal (Bug 9)

`Assets/Scripts/UI/GameBoardUI.cs` had its class body gutted (only a comment
block remains). Any scene that previously referenced a `GameBoardUI` component
will now show a **"Missing Script"** error in the Inspector.

**Steps:**
1. Open every scene in `Assets/Scenes/`
2. In the **Hierarchy**, select each GameObject one by one
3. Open the **Inspector** and look for any component labelled *"Missing Script"*
4. Click the three-dot menu (⋮) on the missing component → **Remove Component**
5. Save the scene (`Ctrl+S`)

**Note:** If `GameBoardUI` was the primary UI driver in a scene, that scene will
need `GameBoardUIEnhanced` wired up in its place. Check whether any public fields
that used to be on `GameBoardUI` need to be re-assigned on `GameBoardUIEnhanced`.

---

## 3 — Delete the stub file created in the wrong location (Bug 11)

During the fix pass a placeholder was written to `Assets/Scripts/UI/index.html`.
The real web implementation lives at the **project root** (`/index.html`).
The stub file in the Unity asset tree serves no purpose and will generate a
harmless-but-noisy Unity import warning.

**Steps:**
1. In the Unity Project panel navigate to `Assets/Scripts/UI/`
2. Right-click `index.html` → **Delete** (this also removes its `.meta` file)

Alternatively, delete via Windows Explorer **and** manually delete
`Assets/Scripts/UI/index.html.meta`.

**Also:** The folder `webapp/` at the project root contains a blank
`webapp/index.html` (intentionally emptied). The entire `webapp/` folder can be
deleted from Windows Explorer — it was created in error and is not referenced
anywhere.

---

## 4 — Add the Tips button to the Main Menu scene (TipsWindowUI)

`TipsWindowUI.cs` and the `MenuUI.cs` wiring are complete. The only remaining
step is connecting them in the scene Inspector.

**Steps:**
1. Open the Main Menu scene in `Assets/Scenes/`
2. In the Hierarchy, locate the **MenuUI** GameObject
3. In the Inspector, find the `MenuUI` component
4. Add a new **Button** GameObject as a sibling of the Easy / Medium / Hard /
   Mixed buttons (or use an existing placeholder button)
   - Label: `Tips` (or `Strategy Tips`)
   - Position it visually near the other menu buttons
5. Drag that Button into the **Tips Button** slot on the `MenuUI` component
6. Save the scene

The button's `onClick` is wired entirely in code (`MenuUI.Initialize()`), so no
additional click-handler setup is needed in the Inspector.

---

## 5 — Enable Unity Test Framework for the C# test suite (Bug 12)

`Assets/Scripts/Tests/Tests.asmdef` and `GameLogicTests.cs` are in place, but
the Unity Test Framework package must be present for the runner to discover them.

**Steps:**
1. Open **Window → Package Manager**
2. Search for **"Test Framework"** (com.unity.test-framework)
3. If not already installed, click **Install**
4. Once installed, open **Window → General → Test Runner**
5. Switch to the **Edit Mode** tab
6. The `CardGame.Tests` assembly and all tests inside should appear
7. Click **Run All** to verify all tests pass

---

*Last updated after the Bug-fix pass (Bugs 1–3, 5–13 addressed in code).*
