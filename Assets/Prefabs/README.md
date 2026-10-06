# Prefabs

This folder is intentionally empty.

Garage Tycoon builds its entire interface **in code at runtime** rather than from prefabs. The
`GameBootstrap` component creates the canvas, and the classes in `Assets/Scripts/Unity/UI` create
every panel, card, button and bar from there.

Why it was done this way:

- The whole UI lives in source control as readable C#, so changes show up as normal diffs.
- There are no prefab merge conflicts, and nothing can break by a reference coming unhooked in the
  inspector.
- Sprites are generated at runtime (`UISprites.cs`), so the project ships with no art dependencies.

If you later want to build screens by hand in the editor, this is where the prefabs would go — start
by replacing one screen at a time in `Assets/Scripts/Unity/UI`.
