using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ATT.Character.QuickAccessMenu;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using HarmonyLib;
using Stream = Alta.Serialization.Stream;

namespace ShardPerformance
{
    /// <summary>
    /// "perf ..." commands sent from inside the game by the mod's developer, and nobody else.
    ///
    /// Transport: the game's own CommandSync (the in-game console's "Run on Server" path).
    /// A client's RouteCommand sends a SyncB entity message carrying a string; the server's
    /// handler (CommandSync.SyncCommand) only reads it into commandToSync and runs nothing,
    /// and EntityMessageHandler.HandleMessage has no authority check. A postfix here picks
    /// up strings starting with "shardperf ", and the reply goes back to that player only
    /// through syncCommandConfirmation, which the client shows in its console.
    ///
    /// Authentication: the developer build of ShardClient signs each command with an RSA
    /// private key kept on the developer's headset (never in any DLL). This file holds only
    /// the public key, so publishing it lets nobody else sign. Each signed payload is
    ///   v1|&lt;sender's player identifier&gt;|&lt;unix seconds&gt;|&lt;nonce&gt;|&lt;command&gt;
    /// and is refused unless the signature verifies, the identifier is the sender's own
    /// (a captured message can't be replayed by someone else), the time is within
    /// MaxClockSkewSeconds, and the nonce hasn't been used.
    ///
    /// A message must fit one 1100-byte packet (KcpTransport.PacketSize 1200 - 100), so
    /// replies are cut to ReplyLimit characters; the full text always goes to the log.
    /// </summary>
    internal static class RemoteCommands
    {
        internal const string Prefix = "shardperf ";
        private const int MaxClockSkewSeconds = 120;
        private const int ReplyLimit = 850;

        private const string PublicModulus = "tRMzLosY4mMstHifjdUTw5zmmoql5hwRxhwfopLLMyR6eKsAXZCq3L7X4fFdE99/np327WedkYxwwfCS2zIbaASS4xLp56M+BdaqQNrhlTbSHfuGzT/RpzZ3kNWr2P/xSg+RgAbiQkK0qqcC2Fjmqj0DR9S8/BDs+kuyDQX3sFxjvtueDFW+5JsEnWz4NdkoNaLy0VU30EtOoRr6ocb7p5yQVqBWy19lRF2jRwn0gD328TlL9zarb6dQVIr8wvmJDs7dSDuqRCNGD04t1/QX6ssrrImg+MvIdj7bNVxu8a6JhyE8sBjJvP3VOT5MKsDNulBBfB1svNiHEnh9/ggyyw==";
        private const string PublicExponent = "AQAB";

        private static readonly Dictionary<string, long> UsedNonces = new Dictionary<string, long>();
        private static RSAParameters? _publicKey;

        internal static string Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                harmony.Patch(
                    AccessTools.Method(typeof(CommandSync), nameof(CommandSync.SyncCommand)),
                    postfix: new HarmonyMethod(typeof(RemoteCommands), nameof(SyncCommandPostfix)));
            }
            catch (Exception exception)
            {
                return "Remote perf commands not patched: " + exception.Message;
            }
            return "Patched CommandSync.SyncCommand for signed remote perf commands.";
        }

        private static void SyncCommandPostfix(CommandSync __instance, IPlayer player, Stream stream)
        {
            if (!NetworkSceneManager.IsServer || stream == null || !stream.IsReading)
                return;
            string text = __instance.commandToSync;
            if (text == null || !text.StartsWith(Prefix, StringComparison.Ordinal))
                return;

            string reply;
            try
            {
                string command = Authenticate(text.Substring(Prefix.Length), player, out string who);
                ShardPerformanceMod.Instance.LoggerInstance.Msg("Remote perf command from " + who + ": " + command);
                reply = Run(command);
                ShardPerformanceMod.Instance.LoggerInstance.Msg(reply);
            }
            catch (Exception exception)
            {
                reply = "perf: refused: " + exception.Message;
                ShardPerformanceMod.Instance.LoggerInstance.Warning("Remote perf command "
                    + (player == null || player.UserInfo == null ? "" : "from " + player.UserInfo.Username + " ")
                    + "refused: " + exception.Message);
            }

            if (reply.Length > ReplyLimit)
                reply = reply.Substring(0, ReplyLimit) + "... (full report in the server log)";
            Reply(__instance, player, reply);
        }

        /// <summary>Checks the signed message and returns the command it carries.</summary>
        private static string Authenticate(string wire, IPlayer player, out string who)
        {
            who = player == null || player.UserInfo == null ? "?" : player.UserInfo.Username;
            string[] parts = wire.Split(' ');
            if (parts.Length != 3 || parts[0] != "v1")
                throw new Exception("unreadable message");
            byte[] payload = Convert.FromBase64String(parts[1]);
            byte[] signature = Convert.FromBase64String(parts[2]);
            if (!Verify(payload, signature))
                throw new Exception("bad signature");

            string[] fields = Encoding.UTF8.GetString(payload).Split(new[] { '|' }, 5);
            if (fields.Length != 5 || fields[0] != "v1")
                throw new Exception("unreadable payload");
            if (player == null || player.UserInfo == null || fields[1] != player.UserInfo.Identifier.ToString())
                throw new Exception("signed for a different player");
            long sent = long.Parse(fields[2]);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - sent) > MaxClockSkewSeconds)
                throw new Exception("too old (clock difference " + (now - sent) + " s)");

            foreach (string stale in UsedNonces.Where(pair => now - pair.Value > 2 * MaxClockSkewSeconds).Select(pair => pair.Key).ToList())
                UsedNonces.Remove(stale);
            if (UsedNonces.ContainsKey(fields[3]))
                throw new Exception("already used");
            UsedNonces[fields[3]] = now;
            return fields[4];
        }

        internal static bool Verify(byte[] payload, byte[] signature)
        {
            if (_publicKey == null)
                _publicKey = new RSAParameters
                {
                    Modulus = Convert.FromBase64String(PublicModulus),
                    Exponent = Convert.FromBase64String(PublicExponent),
                };

            // RSAPKCS1SignatureDeformatter is the oldest API for this and is in Unity's Mono.
            using (var rsa = new RSACryptoServiceProvider())
            using (var sha = SHA256.Create())
            {
                rsa.ImportParameters(_publicKey.Value);
                var deformatter = new RSAPKCS1SignatureDeformatter(rsa);
                deformatter.SetHashAlgorithm("SHA256");
                return deformatter.VerifySignature(sha.ComputeHash(payload), signature);
            }
        }

        /// <summary>The same commands as the server console's "perf" module.</summary>
        private static string Run(string command)
        {
            string[] words = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0 && words[0].Equals("perf", StringComparison.OrdinalIgnoreCase))
                words = words.Skip(1).ToArray();
            string verb = words.Length == 0 ? "status" : words[0].ToLowerInvariant();
            switch (verb)
            {
                case "status": return Commands.Status();
                case "reset": return Commands.Reset();
                case "settings": return Commands.Settings();
                case "set":
                    if (words.Length != 3)
                        return "perf: usage: perf set <name> <value>";
                    return Commands.Set(words[1], words[2]);
                default:
                    return "perf: commands: status, reset, settings, set <name> <value>";
            }
        }

        private static void Reply(CommandSync sync, IPlayer player, string text)
        {
            try
            {
                // CommandSyncInfo's fields are readonly and its constructor wants a game
                // CommandResult, so fill a boxed copy by reflection.
                object info = default(CommandSync.CommandSyncInfo);
                typeof(CommandSync.CommandSyncInfo).GetField("CommandName", BindingFlags.Public | BindingFlags.Instance).SetValue(info, "perf");
                typeof(CommandSync.CommandSyncInfo).GetField("Result", BindingFlags.Public | BindingFlags.Instance).SetValue(info, text);
                sync.syncCommandConfirmation.SendToPlayer(player, (CommandSync.CommandSyncInfo)info);
            }
            catch (Exception exception)
            {
                ShardPerformanceMod.Instance.LoggerInstance.Warning("Remote perf reply failed: " + exception.Message);
            }
        }
    }
}
