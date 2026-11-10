using System;
using Alta.Networking;
using MelonLoader;
using UnityEngine;

namespace ShardPerformance
{
    /// <summary>
    /// Server-side performance mod for A Township Tale community servers.
    ///
    /// Everything here runs only while this game is the server
    /// (NetworkSceneManager.IsServer); on a PC player's game it does nothing, so players
    /// never need it. Measuring is always on and cheap; each optimisation has its own
    /// setting and starts off until it has been measured on a real server.
    ///
    /// Settings live in UserData/MelonPreferences.cfg under [ShardPerformance], and the
    /// server console has "perf ..." commands (see Commands.cs).
    /// </summary>
    public class ShardPerformanceMod : MelonMod
    {
        internal static ShardPerformanceMod Instance;

        private MelonPreferences_Category _settings;
        private MelonPreferences_Entry<float> _reportIntervalSeconds;
        private MelonPreferences_Entry<bool> _forestNearOnly;
        private MelonPreferences_Entry<bool> _itemsSameChunkOnly;
        private MelonPreferences_Entry<float> _gateIntervalSeconds;

        private float _nextReportAt;
        private float _nextGateAt;

        internal float ReportIntervalSeconds => _reportIntervalSeconds.Value;
        internal bool ForestNearOnly => _forestNearOnly.Value;
        internal bool ItemsSameChunkOnly => _itemsSameChunkOnly.Value;
        internal float GateIntervalSeconds => _gateIntervalSeconds.Value;

        public override void OnInitializeMelon()
        {
            Instance = this;
            _settings = MelonPreferences.CreateCategory("ShardPerformance");
            _reportIntervalSeconds = _settings.CreateEntry("ReportIntervalSeconds", 60f,
                description: "Seconds between performance reports in the server log. 0 = no automatic reports (perf status still works).");
            _forestNearOnly = _settings.CreateEntry("ForestNearOnly", true,
                description: "Only keep a forest chunk's trees/bushes generated while a player stands in it or an adjacent chunk; release the rest. Rebuilds when a player returns.");
            _itemsSameChunkOnly = _settings.CreateEntry("ItemsSameChunkOnly", true,
                description: "Only stream a chunk's loose items/props while a player is in that exact chunk. Aggressive: neighbouring chunks' loose content is dropped.");
            _gateIntervalSeconds = _settings.CreateEntry("GateIntervalSeconds", 2f,
                description: "Seconds between forest/item gating sweeps. Lower = tighter but more CPU; 2s is a good balance.");

            LoggerInstance.Msg(ForestGatePatch.Apply(HarmonyInstance));
            LoggerInstance.Msg(RemoteCommands.Apply(HarmonyInstance));
            LoggerInstance.Msg("ShardPerformance loaded. It only acts while this game is a server.");
        }

        public override void OnUpdate()
        {
            if (!NetworkSceneManager.IsServer)
                return;

            PerfMonitor.Sample(Time.unscaledDeltaTime);

            if (Time.realtimeSinceStartup >= _nextGateAt && (_forestNearOnly.Value || _itemsSameChunkOnly.Value))
            {
                _nextGateAt = Time.realtimeSinceStartup + Math.Max(0.5f, _gateIntervalSeconds.Value);
                try { ForestGatePatch.Tick(_forestNearOnly.Value, _itemsSameChunkOnly.Value); }
                catch (Exception ex) { LoggerInstance.Warning("Gate sweep failed: " + ex.Message); }
            }

            float interval = _reportIntervalSeconds.Value;
            if (interval <= 0f || Time.realtimeSinceStartup < _nextReportAt)
                return;
            bool first = _nextReportAt == 0f;
            _nextReportAt = Time.realtimeSinceStartup + Math.Max(5f, interval);
            if (first)
            {
                PerfMonitor.Reset();
                return;
            }

            try
            {
                LoggerInstance.Msg(PerfMonitor.Report(resetWindow: true));
            }
            catch (Exception exception)
            {
                LoggerInstance.Warning("Report failed: " + exception.Message);
            }
        }

        internal void Set(string name, string value)
        {
            switch (name.ToLowerInvariant())
            {
                case "reportintervalseconds":
                    _reportIntervalSeconds.Value = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "forestnearonly":
                    _forestNearOnly.Value = ParseBool(value);
                    break;
                case "itemssamechunkonly":
                    _itemsSameChunkOnly.Value = ParseBool(value);
                    break;
                case "gateintervalseconds":
                    _gateIntervalSeconds.Value = Mathf.Clamp(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture), 0.5f, 30f);
                    break;
                default:
                    throw new ArgumentException("unknown setting '" + name + "'");
            }
            _settings.SaveToFile(false);
        }

        internal string Settings()
        {
            return "ReportIntervalSeconds=" + _reportIntervalSeconds.Value
                + " ForestNearOnly=" + (_forestNearOnly.Value ? "on" : "off")
                + " ItemsSameChunkOnly=" + (_itemsSameChunkOnly.Value ? "on" : "off")
                + " GateIntervalSeconds=" + _gateIntervalSeconds.Value
                + " | released " + ForestGatePatch.ForestsReleased
                + ", item-chunks tightened " + ForestGatePatch.ItemChunksTightened;
        }

        private static bool ParseBool(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "on": case "true": case "1": case "yes": return true;
                case "off": case "false": case "0": case "no": return false;
                default: throw new ArgumentException("expected on or off, got '" + value + "'");
            }
        }
    }
}
