# ShardPerformance

A **server-side** performance mod for *A Township Tale* community servers. It runs only
on the machine hosting the server — **players install nothing**. It measures where the
server is spending frame time, and (optionally) cuts two of the biggest costs: forest
content and loose-item streaming for areas no player is near.

> ⚠️ **Experimental / untested.** As of 0.3.0 this has **not been run on a live server**.
> The gating below is aggressive by design. Try it when your server is quiet, watch
> `perf status`, and use the off-switches below if anything looks wrong. Removing the DLL
> and restarting reverts everything.

Download: **[latest release](https://github.com/Flonky439/ShardPerformance/releases/latest)** (`ShardPerformance.dll`).

## Install (server host only)

1. Put `ShardPerformance.dll` in your server's MelonLoader **`Mods/`** folder (the same
   folder your other server mods are in).
2. Restart the server.
3. In the server console (TavernLauncher's console, or the remote console), run
   `perf status` to confirm it loaded.

It does nothing on a client — only while the game instance is the server.

## What it does

| Setting | Default | Effect |
| --- | --- | --- |
| `ForestNearOnly` | **on** | A forest chunk keeps its trees/bushes generated only while a player is in it or an adjacent chunk; the rest are released and rebuilt when a player returns. The main fix for forest lag. |
| `ItemsSameChunkOnly` | **on** | A chunk streams its loose items/props only while a player is standing in that exact chunk. |
| `GateIntervalSeconds` | `2` | How often the gating sweep runs. Lower = tighter, more CPU. |
| `ReportIntervalSeconds` | `60` | Seconds between performance reports in the server log. `0` = off. |
| `AdminUsers` | (empty) | Usernames (comma-separated) allowed to run `shard admin` server commands from inside the game. The game still applies each player's real permissions. |

Measuring (frame times, physics rate, GC, chunk recompute cost, loaded chunks, entities,
per-player chunk counts) is always on and cheap, regardless of the gating settings.

## Commands (server console)

| Command | Does |
| --- | --- |
| `perf status` | the performance report for the window since the last one |
| `perf reset` | start a new measuring window now |
| `perf settings` | show current settings and gating counters |
| `perf set <name> <value>` | change a setting, e.g. `perf set ForestNearOnly off` |

### Letting admins run commands from in-game

If your admins also run **ShardClient**, they can run server commands from the VR console
with `shard admin <command>`. Add their usernames to `AdminUsers`:

```
perf set AdminUsers irishduo,SomeAdmin
```

Their command is sent to this mod, which runs it through the game's own command system —
so each admin's real permissions still apply. Anyone not on the list is refused.

**Turn the aggressive parts off instantly:**
```
perf set ForestNearOnly off
perf set ItemsSameChunkOnly off
```

## What it can't do

It can't change rendering — fog, draw distance, particles and anything visual are the
client's job, not the server's. ShardPerformance only governs what the server simulates
and streams.

---
Use at your own risk. Not affiliated with Alta or Modding Tavern.
Copyright © 2026 Flonky439.
