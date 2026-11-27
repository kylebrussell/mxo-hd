namespace HeadlessClient;

public static partial class Scenarios
{
    private static readonly byte[] OwnViewNavHeader = WireFormat.FromHex("02 00 01 1c");

    private static bool ContainsSelfSpawn(WorldPayload payload) =>
        payload.ViewSection != null && WireFormat.FindCreations(payload.ViewSection).Any(c => c.ViewId == 2);

    private static bool ContainsOwnViewNav(WorldPayload payload) =>
        payload.ViewSection != null && BytePattern.IndexOf(payload.ViewSection, OwnViewNavHeader, 0) >= 0;

    /// <summary>
    /// Reproduces the cold-login Negotiating stall seen in pr1248/pr1250/pr1253 when Server->Client UDP loss drops
    /// the spawn-critical object datagrams (SSEQ 4-6: self ViewCreate view 2 and initial own-view 02 00 01 1c nav
    /// position) while later packets (e.g. MOTD RPC) are ACKed past them with a PSS gap.
    /// </summary>
    private static async Task SpawnObjectLossRecovery(ScenarioContext context)
    {
        // Case 1 (pr1248): drop the entire first spawn object burst (SSEQ 4-6 / self ViewCreate + initial nav pos)
        // while letting subsequent packets arrive so the client ACKs past the dropped SSEQs with PSS gaps.
        var fullBurstPlayer = await context.AddPlayerAsync(new CharacterSeed("SpawnDropBurst"));
        await fullBurstPlayer.WaitForRpcAsync("06"); // World command.

        var burstDroppedSeqs = new List<ushort>();
        bool droppedSelfView = false;
        bool droppedOwnNav = false;
        fullBurstPlayer.World.SetServerDropFilter((sseq, payload) =>
        {
            if (payload.ViewSection == null) return false;
            bool isSelf = ContainsSelfSpawn(payload);
            bool isNav = ContainsOwnViewNav(payload);
            if ((isSelf && !droppedSelfView) ||
                (isNav && !droppedOwnNav) ||
                (sseq is >= 4 and <= 6 && payload.Rpcs.Count == 0 && !burstDroppedSeqs.Contains(sseq)))
            {
                if (isSelf) droppedSelfView = true;
                if (isNav) droppedOwnNav = true;
                burstDroppedSeqs.Add(sseq);
                return true;
            }
            return false;
        });

        fullBurstPlayer.SendRpc("05"); // SpawnRequest.

        // Wait until the server resends and delivers both self view 2 and own-view nav position after the drop.
        await fullBurstPlayer.WaitUntilAsync(
            () => fullBurstPlayer.World.CreationSnapshot().Any(c => c.ViewId == 2),
            "resent self spawn (view 2) after SSEQ 4-6 burst drop",
            TimeSpan.FromSeconds(10));
        await fullBurstPlayer.WaitUntilAsync(
            () => HasOwnViewNavAtOrAfterSelfSpawn(fullBurstPlayer.World),
            "resent own-view nav position (02 00 01 1c) at or after view 2 creation",
            TimeSpan.FromSeconds(10));

        Check.That(droppedSelfView, "Case 1 must actually drop the initial self ViewCreate datagram");
        Check.That(droppedOwnNav, "Case 1 must actually drop the initial own-view nav datagram");
        fullBurstPlayer.World.SetServerDropFilter(null);

        fullBurstPlayer.SendRpc("0c 01");
        fullBurstPlayer.SendRpc("81 48");
        await fullBurstPlayer.WaitForRpcAsync("81 49");
        Check.Equal(0, fullBurstPlayer.World.Problems.Count, "Case 1 framing and RPC stream stay clean");

        string logAfterBurst = context.Server.DebugLog();
        int resendLinesAfterAck = CountSpawnResendLines(logAfterBurst, fullBurstPlayer.CharId);
        Check.That(resendLinesAfterAck >= 1,
            $"Expected at least one '[NET] Resending ... spawn object(s)' log line for char {fullBurstPlayer.CharId}");

        // Confirm reliable spawn objects retire once ACKed and do not keep resending.
        await Task.Delay(1200);
        int resendLinesLater = CountSpawnResendLines(context.Server.DebugLog(), fullBurstPlayer.CharId);
        Check.Equal(resendLinesAfterAck, resendLinesLater,
            "Spawn objects must stop resending once the client ACKs them");

        // Case 2 (pr1250 / pr1253): drop ONLY the first self ViewCreate datagram while allowing any separate
        // own-view nav position datagram to arrive and be ACKed BEFORE view 2 exists. Verify both self view 2
        // and own-view nav position are delivered in order on resend.
        var partialDropPlayer = await context.AddPlayerAsync(new CharacterSeed("SpawnDropSelfOnly"));
        await partialDropPlayer.WaitForRpcAsync("06");

        bool droppedSelfOnly = false;
        partialDropPlayer.World.SetServerDropFilter((sseq, payload) =>
        {
            if (!droppedSelfOnly && ContainsSelfSpawn(payload))
            {
                droppedSelfOnly = true;
                return true;
            }
            return false;
        });

        partialDropPlayer.SendRpc("05");
        await partialDropPlayer.WaitUntilAsync(
            () => partialDropPlayer.World.CreationSnapshot().Any(c => c.ViewId == 2),
            "resent self spawn (view 2) after self-only drop",
            TimeSpan.FromSeconds(10));
        await partialDropPlayer.WaitUntilAsync(
            () => HasOwnViewNavAtOrAfterSelfSpawn(partialDropPlayer.World),
            "own-view nav position (02 00 01 1c) delivered at or after resent view 2 creation",
            TimeSpan.FromSeconds(10));

        Check.That(droppedSelfOnly, "Case 2 must drop the initial self ViewCreate datagram");
        partialDropPlayer.World.SetServerDropFilter(null);

        partialDropPlayer.SendRpc("0c 01");
        partialDropPlayer.SendRpc("81 48");
        await partialDropPlayer.WaitForRpcAsync("81 49");
        Check.Equal(0, partialDropPlayer.World.Problems.Count, "Case 2 framing and RPC stream stay clean");
        Check.That(CountSpawnResendLines(context.Server.DebugLog(), partialDropPlayer.CharId) >= 1,
            $"Expected spawn object resend log for Case 2 char {partialDropPlayer.CharId}");
    }

    private static bool HasOwnViewNavAtOrAfterSelfSpawn(WorldSession world)
    {
        var sections = world.ViewSnapshot();
        int selfIndex = -1;
        for (int i = 0; i < sections.Count; i++)
        {
            if (selfIndex < 0 && WireFormat.FindCreations(sections[i]).Any(c => c.ViewId == 2))
            {
                selfIndex = i;
            }
            if (selfIndex >= 0 && BytePattern.IndexOf(sections[i], OwnViewNavHeader, 0) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static int CountSpawnResendLines(string debugLog, uint charId) =>
        debugLog.Split('\n').Count(line =>
            line.Contains("Resending ") && line.Contains("spawn object(s)") && line.Contains($"char {charId};"));
}
