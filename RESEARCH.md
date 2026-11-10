# ShardPerformance research

What the A Township Tale server does that costs performance, from the decompiled PC game
(`Reference/Township`, made from `Root.Township-publicized.dll`; the Windows server is this
same game started with `/start_server`). Each item says whether it is **verified in code**
or still needs a **measurement** on a running server.

## How the server runs

- TavernLauncher starts the game with `/start_server -1 false <port>`, plus `-batchmode
  -nographics` **only when "Show game" is off** (`TavernLauncher/server/core/launcher_window.py`).
  With Show game on, the server also renders a full 3D view every frame. *Verified in code;
  cost needs a measurement.*
- Target frame rate is **90** (`ApplicationManager.Setup`). *Verified.*
- Physics runs at **45 Hz**, lowered toward **15 Hz** as server FPS falls from 20 to 5
  (`DynamicPhysicsTimeSet`, `ServerSettings` FPSForMaximumFixedFrameCount=20,
  FPSForMinimumFixedFrameCount=5, Max=45, Min=15, re-checked every 5 s). These are
  `[DevOnly]` server settings. *Verified.*

## Lag with many players

- Every connection's reliable channel keeps up to **64 packets** that failed to send. At 64
  the player is **kicked with "Reliable overflow"** (`OutgoingPacketManager.WriteToReliable`),
  counted in `NetworkManager.ReliablePacketOverflowDisconnects`. Players dropping when a
  server is busy are likely this. *Verified in code; how often it happens needs the counter
  on a busy server (perf status shows it).*
- Packets are batched per channel and flushed every `maximumSendInterval` (code default
  0.03 s; the real per-channel values are in game assets, not code). *Values need reading
  on a running server.*
- Rigidbodies only send updates while awake (`NetworkRigidbody` sleeping checks), so
  resting items cost little network. *Verified.*

## Chunk loading (joins, moving around, the Forest)

- **The server decides which chunks each player loads** (`PlayerChunkManager.LoadFromPosition`
  on the server, sent to the player as `ChunksToSync`). Every entity in those chunks is
  spawned for that player. *Verified.*
- PC players use `NormalChunkLoader`; **Quest players use `OptimisedChunkLoader`**: a 20 m
  load box and a 50 m unload box (`StreamingSettings`). *Verified.*
- **QuestForest widens the Quest box to 100 m / 160 m inside the Forest**
  (`QuestForest/src/Platform.cs`, `Reach`; server settings `HeadsetLoadMetres` /
  `HeadsetUnloadMetres`, console `forest headset-reach <load> <unload>`). That's 25x the
  load area and about 10x the unload area, so far more chunks and entities stream to Quest
  players in the Forest. This is the most likely cause of headset lag in the Forest.
  *Verified in code; the FPS effect needs a headset test.* Server owners can already lower
  it, e.g. `forest headset-reach 50 90`, at the cost of terrain loading closer to walkers.
- `LoadFromPosition` runs on **every position change** the server sees for a player
  (`LocomotionController.PositionChanged`), with LINQ and set comparisons each time.
  ShardPerformance measures it and can throttle it (`ChunkThrottle`). *Verified in code;
  cost needs a measurement.*

## Built-in tools worth using

Server console (game commands):

- `debug server-stats`: FPS, players, loaded overworld/cave chunks.
- `debug entity-health`, `debug prefabcounts`, `debug entities`: entity manager health,
  per-prefab counts, every entity to a CSV.
- `debug lag <fps>`: slows the server to a target FPS. Useful to **simulate an overloaded
  server** and check what players experience. `debug lag 0` removes it.
- `debug fixedtime`: current physics rate.
- `profiling sample`: a Unity profiler sample.

## Not yet researched

- How dropped items, felled trees and ore piles accumulate, and how decay
  (`DecayMultiplier`) and spawners (`SpawningRateMultiplier`) clean up.
- AI (creatures) update cost.
- Save/autosave stalls (`MinutesBetweenAutoSaving`, default 5).
- Join cost: the initial scene/chunk serialization a joining player receives.
