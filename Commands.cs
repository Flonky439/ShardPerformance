using System;
using Alta.Console;

namespace ShardPerformance
{
    /// <summary>
    /// "perf ..." in the server console (TavernLauncher's console, or the remote console).
    /// The game collects [Module]/[Command] classes from loaded assemblies, the same way
    /// QuestForest's "forest ..." commands are found.
    /// </summary>
    [Module("perf", "ShardPerformance: server performance stats and settings")]
    public static class Commands
    {
        [Command("status", "Frame times, physics rate, GC, chunk loading, loaded chunks, entities and players since the last report")]
        public static string Status()
        {
            return PerfMonitor.Report(resetWindow: false);
        }

        [Command("reset", "Start a new measuring window now")]
        public static string Reset()
        {
            PerfMonitor.Reset();
            return "perf: new window started";
        }

        [Command("settings", "Show ShardPerformance's settings")]
        public static string Settings()
        {
            return "perf: " + ShardPerformanceMod.Instance.Settings();
        }

        [Command("set", "Change a setting and save it: perf set <name> <value>, e.g. perf set ForestNearOnly on")]
        public static string Set(string name, string value)
        {
            try
            {
                ShardPerformanceMod.Instance.Set(name, value);
                return "perf: " + ShardPerformanceMod.Instance.Settings();
            }
            catch (Exception exception)
            {
                return "perf: " + exception.Message;
            }
        }
    }
}
