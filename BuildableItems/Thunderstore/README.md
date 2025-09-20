# Buildable Items

This patch makes **all items buildable**, similar to how feasts can be placed in the world.

### How it works
- At startup, every prefab with an `ItemDrop` is scanned.
- If the prefab is **readable** and not already a `Piece`, it is automatically given:
    - A `Piece` component
    - Name, icon, and description from its `ItemData`
    - Placement effects inherited from the workbench
    - A build requirement that consumes one instance of the item itself
- The prefab is then added to the Hammer’s build list.

### Notes
- Meshes with **Read/Write disabled** cannot be processed (Unity strips vertex data).
    - These are skipped to avoid NavMesh build errors.
    - Items with unreadable meshes will **not** be buildable until their import settings are updated.
- Special cases like **Fish**, `_material` prefabs, and anything already flagged as a `Piece` are also ignored.
