using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Alta.Chunks;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using UnityEngine;

namespace ShardPerformance
{
    /// <summary>
    /// Frame-time and world statistics for the server, over a window that the periodic
    /// report resets. Per-frame cost is one float appended to a list; everything else
    /// (entity and chunk counts) is only computed when a report is asked for.
    /// </summary>
    internal static class PerfMonitor
    {
        private const float HitchSeconds = 0.1f;

        private static readonly List<float> Frames = new List<float>(8192);
        private static float _windowStart;
        private static int _gc0, _gc1, _gc2;
        private static ulong _overflows;
        private static long _chunkCalls, _chunkSkipped, _chunkTicks;

        internal static void Sample(float deltaTime)
        {
            if (Frames.Count < 200000)
                Frames.Add(deltaTime);
        }

        internal static void Reset()
        {
            Frames.Clear();
            _windowStart = Time.realtimeSinceStartup;
            _gc0 = GC.CollectionCount(0);
            _gc1 = GC.CollectionCount(1);
            _gc2 = GC.CollectionCount(2);
            _overflows = NetworkManager.ReliablePacketOverflowDisconnects;
            _chunkCalls = ForestGatePatch.Calls;
            _chunkSkipped = ForestGatePatch.Skipped;
            _chunkTicks = ForestGatePatch.Ticks;
        }

        internal static string Report(bool resetWindow)
        {
            var text = new StringBuilder();
            float seconds = Math.Max(0.001f, Time.realtimeSinceStartup - _windowStart);
            text.Append("perf over ").Append(F(seconds, 0)).Append(" s: ");

            if (Frames.Count == 0)
            {
                text.Append("no frames sampled yet");
            }
            else
            {
                float[] sorted = Frames.ToArray();
                Array.Sort(sorted);
                double total = 0;
                int hitches = 0;
                foreach (float frame in sorted)
                {
                    total += frame;
                    if (frame >= HitchSeconds)
                        hitches++;
                }
                text.Append("fps ").Append(F((float)(sorted.Length / total), 1))
                    .Append(" | frame ms p50 ").Append(Ms(Percentile(sorted, 0.50f)))
                    .Append(" p95 ").Append(Ms(Percentile(sorted, 0.95f)))
                    .Append(" p99 ").Append(Ms(Percentile(sorted, 0.99f)))
                    .Append(" max ").Append(Ms(sorted[sorted.Length - 1]))
                    .Append(" | hitches>100ms ").Append(hitches);
            }

            text.Append(" | physics ").Append(F(1f / Time.fixedDeltaTime, 0)).Append(" Hz");
            text.Append(" | gc ").Append(GC.CollectionCount(0) - _gc0).Append('/')
                .Append(GC.CollectionCount(1) - _gc1).Append('/')
                .Append(GC.CollectionCount(2) - _gc2)
                .Append(" heap ").Append(GC.GetTotalMemory(false) / (1024 * 1024)).Append(" MB");

            long calls = ForestGatePatch.Calls - _chunkCalls;
            long skipped = ForestGatePatch.Skipped - _chunkSkipped;
            double chunkMs = ForestGatePatch.Milliseconds(ForestGatePatch.Ticks - _chunkTicks);
            text.Append("\n  chunk loading: ").Append(F(calls / seconds, 1)).Append(" recomputes/s, ")
                .Append(F((float)(chunkMs / seconds), 2)).Append(" ms/s, avg ")
                .Append(calls == 0 ? "-" : F((float)(chunkMs / calls), 3)).Append(" ms");
            if (skipped > 0)
                text.Append(", throttled ").Append(skipped);

            ulong overflows = NetworkManager.ReliablePacketOverflowDisconnects - _overflows;
            text.Append("\n  ").Append(World()).Append(" | overflow kicks ").Append(overflows);
            text.Append("\n  ").Append(PlayersLine());

            if (resetWindow)
                Reset();
            return text.ToString();
        }

        /// <summary>Loaded chunks and entities, the same counts "debug server-stats" uses.</summary>
        internal static string World()
        {
            int overworld = 0, caves = 0;
            foreach (Chunk chunk in Chunk.ChunksByIndex.Values)
            {
                if (chunk == null || !chunk.IsLoaded || !(chunk is LocationChunk))
                    continue;
                if (chunk.ProceduralStreamer is CaveLayerStreamer)
                    caves++;
                else
                    overworld++;
            }

            int entities = 0;
            long spawnedFor = 0;
            var scene = NetworkSceneManager.Current as NetworkScene;
            if (scene != null && scene.entityManager != null)
            {
                foreach (NetworkEntity entity in scene.entityManager.IterateEntities())
                {
                    if (entity == null)
                        continue;
                    entities++;
                    spawnedFor += entity.PlayersSpawnedForCount;
                }
            }

            return "chunks loaded " + overworld + " overworld, " + caves + " cave | entities "
                + entities + ", sent to players " + spawnedFor + " (entity x player)";
        }

        internal static string PlayersLine()
        {
            var players = Player.AllPlayers.OfType<Player>().ToList();
            if (players.Count == 0)
                return "players 0";
            var text = new StringBuilder("players ").Append(players.Count).Append(':');
            foreach (Player player in players)
            {
                string name = player.UserInfo == null ? "?" : player.UserInfo.Username;
                text.Append(' ').Append(name).Append(" (")
                    .Append(player.PlatformTarget).Append(", ")
                    .Append(player.LoadedChunks.Count).Append(" chunks)");
            }
            return text.ToString();
        }

        private static float Percentile(float[] sorted, float p)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(p * sorted.Length) - 1, 0, sorted.Length - 1);
            return sorted[index];
        }

        private static string Ms(float seconds) => F(seconds * 1000f, 1);

        private static string F(float value, int decimals) =>
            value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }
}
