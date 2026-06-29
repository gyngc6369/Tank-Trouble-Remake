# Tank Trouble Unity Migration

This folder is the Unity/C# migration target for the existing Python/Pygame project.

Completed phases:

- Phase 0: Unity project skeleton and frozen Pygame parameters.
- Phase 1: Optimized map data, preset/random map generation, merged wall segments, and wall rendering with BoxCollider2D.
- Phase 2: Tank body/controller/view and direct player input using Physics2D casts for wall collision.
- Phase 3: Bullet pool, Physics2D bullet collision, bounce counting, ammo regeneration, shooting cooldown, and tank hit notification.
- Phase 4: Round flow, countdown, survivor scoring, match win detection, and pause handling.
- Phase 5: uGUI menu/HUD/overlay controllers, map selection, score selection, difficulty selection, and map thumbnails.
- Phase 6: AI pathfinding, fast danger prediction, Physics2D multi-bounce aiming up to 4 bounces, burst fire, and AI command driving.

Confirmed migration rules:

- Use current Pygame source parameters.
- Bullet disappears on the wall hit after completing 7 bounces.
- Bullet collision uses Unity Physics2D instead of Pygame-style per-wall geometry loops.
- Bullet speed is normalized after collisions to avoid physics drift.
- Tank movement also uses Unity Physics2D queries instead of per-wall geometry loops.
- Tank hit body excludes barrel.
- Walls are thin grid-edge line segments, not filled cells.
- Wall segments are merged before rendering to reduce GameObject and collider count.
- In three-tank mode, a round ends only when at most one tank remains alive.
- Round scoring awards 1 point to the sole survivor.
- AI uses the same tank movement/rotation commands as players.
- AI ballistics supports direct shots and up to 4 wall bounces using Physics2D CircleCast simulation.
- AI danger prediction uses fast Physics2D CircleCast trajectory prediction and grid-cell marking.

Next phase: audio, visual polish, and final scene wiring.
