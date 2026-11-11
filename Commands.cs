using System;
using System.Reflection;
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
        private static bool _registered;

        /// <summary>
        /// Registers this mod's [Module]/[Command] types with the game console.
        /// CommandCollection.Collect only scans the assembly it is given; the game never
        /// scans mod DLLs, so without this "perf ..." is "command not found" in the server
        /// console. Called repeatedly from OnUpdate until the collection exists (it is built
        /// after the server starts), then once it succeeds it stops.
        /// </summary>
        public static bool TryRegister()
        {
            if (_registered)
                return true;
            CommandCollection collection = CommandService.CommandCollection;
            if (collection == null)
                return false;
            var found = collection.Collect(Assembly.GetExecutingAssembly());
            if (found != null)
                foreach (Alta.Console.Module module in found)
                    collection.Modules.Add(module);
            _registered = true;
            ShardPerformanceMod.Instance?.LoggerInstance.Msg("Registered perf console commands ("
                + (found == null ? 0 : found.Count) + " module(s)).");
            return true;
        }

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
