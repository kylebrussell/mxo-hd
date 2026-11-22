using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DeflectVirusParentRadiusAndOverlap(ScenarioContext context)
    {
        const ushort parent = 378, child = 693, melee = 1009, viral = 183;
        const string isUpdate = "02 80 80 80 10";
        // Complete stock request and dismissal from proxy_024 lines 12961–62 and 13475.
        const string use = "80 b9 7a 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 15 00 00 e0 d1 2c 08 6b 00 00 00 00 00";
        const string dismiss = "80 bb 7a 01";
        const int x = 17000, y = 495, firstZ = 6400, secondZ = 7743, edgeZ = 7060;
        var stands = new[] { (x, firstZ), (x, secondZ), (x, edgeZ), (x + 661, firstZ), (x + 150, firstZ) };
        Check.Equal(6d, AbilityCatalog.Find(child).Number("Ability/RadiusOfEffect"), "The owner invokes the six-metre child");
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        foreach (var (standX, standZ) in stands)
            Check.That(NavMesh.Locate(1, standX, y, standZ, out var stand) && !stand.indoors && stand.floorY + 95 == y,
                $"Outdoor NAV for Deflect Virus at ({standX},{y},{standZ})");

        var first = await At("VirusSource22", 22, stands[0], caster: true);
        var second = await At("VirusSource30", 30, stands[1], caster: true);
        var edge = await At("VirusShared", 50, stands[2]);
        var outside = await At("VirusOutside", 50, stands[3]);
        var outsider = await At("VirusOutsider", 50, stands[4]);
        var players = new[] { first, second, edge, outside, outsider };
        foreach (var player in players) await player.EnterWorldAsync();

        foreach (var (caster, expectedRadiusBonus) in new[] { (first, 10), (second, 14) })
        {
            var bonus = await Watch(caster, 0, frame => BytePattern.Parse(ModPattern(74, 127)).IsMatch(frame.Bytes),
                $"{caster.Handle} has required Resist Damage loaded");
            Check.Equal(expectedRadiusBonus, BitConverter.ToInt32(bonus.Bytes, 13), "Child radius uses the caster's legal range bonus");
        }
        foreach (var (caster, index, subject) in new[] { (first, 2, edge), (second, 2, edge), (first, 3, outside), (first, 4, outsider) })
        {
            var seen = await caster.WaitForViewNamedAsync(subject.Handle);
            Check.That(EngageFoesFixture.Position(seen).SequenceEqual(new double[] { stands[index].Item1, y, stands[index].Item2 }),
                $"{caster.Handle} sees {subject.Handle} at the intended stand");
        }
        foreach (var member in new[] { second, edge, outside })
        {
            first.SendRpc(GroupInvite(Team, member.Handle, ""));
            await member.WaitForRpcAsync("80 8f 0c 00");
            member.SendRpc(InviteAnswer(Team, 0, first.Handle));
            await member.WaitForRpcAsync("80 8e");
            await first.WaitForRpcAsync($"70 03 {Le(member.CharId)} 09 00");
        }

        int start = edge.World.RpcSnapshot().Count;
        await Start(first);
        await Pair(first, 0, 10, 21);
        await Pair(edge, start, 10, 21);
        Check.Equal(0, Defenses(second, 0).Count, "The other caster is outside the first source's radius");

        int secondStart = edge.World.RpcSnapshot().Count;
        await Start(second);
        await Pair(second, 0, 14, 28);
        await Pair(edge, secondStart, 14, 28);
        var fx = await Watch(edge, secondStart, frame => IsBonus(frame, parent, parent, 0x28000820, 0.2f), "The second source sends Deflect Virus FX");
        ushort fxRecord = BitConverter.ToUInt16(fx.Bytes, 4);
        await Watch(edge, secondStart, frame => IsRemoval(frame, parent, parent, fxRecord), "The linked FX removes its own record");

        await Stop(first);
        int stoppedISStart = first.World.ViewSnapshot().Count;
        ushort stoppedBalance = RefusalOwnValue(first, "InnerStrengthAvailable", isUpdate);
        int rollingISStart = second.World.ViewSnapshot().Count;
        ushort rollingBalance = RefusalOwnValue(second, "InnerStrengthAvailable", isUpdate);
        int refreshStart = edge.World.RpcSnapshot().Count;
        var final = await Pair(edge, refreshStart, 14, 28);
        var rollingIS = ViewUpdateValues(second, 2, isUpdate, rollingISStart).Prepend(rollingBalance);
        Check.That(rollingIS.Zip(rollingIS.Skip(1)).Any(pair => pair.First - pair.Second == 7),
            "The remaining source pays its next seven-IS tick");
        var stoppedIS = ViewUpdateValues(first, 2, isUpdate, stoppedISStart).Prepend(stoppedBalance);
        Check.That(stoppedIS.Zip(stoppedIS.Skip(1)).All(pair => pair.First <= pair.Second),
            "The dismissed source stops paying upkeep");

        int offAt = edge.World.RpcSnapshot().Count;
        await Stop(second);
        Check.That(Defenses(edge, offAt).All(record => record.AddRemoveType != 1), "Dismissal preserves the most recent child snapshot");
        foreach (var (stat, record) in new[] { (melee, final.Melee), (viral, final.Viral) })
            await Watch(edge, offAt, frame => IsRemoval(frame, child, stat, record), $"Target stat {stat} expires with its own record");

        var active = new Dictionary<ushort, ushort>();
        foreach (var record in Defenses(edge, start))
        {
            if (record.AddRemoveType == 0)
                Check.That(active.TryAdd(record.TargetId, record.RecordId), "Two sources never stack a second copy of either defense");
            else if (record.AddRemoveType == 1)
                Check.That(active.Remove(record.TargetId, out ushort old) && old == record.RecordId, "Replacement and expiry remove the exact previous record");
        }
        Check.Equal(0, active.Count, "Both defense records have expired");
        foreach (var excluded in new[] { outside, outsider })
        {
            Check.Equal(0, Defenses(excluded, 0).Count, $"{excluded.Handle} never receives the child");
            Check.Equal(0, excluded.Received(ModPattern(parent, parent)).Count, $"{excluded.Handle} never receives the FX");
        }
        Check.That(players.All(player => player.World.Problems.Count == 0), "Deflect Virus transport framing");

        async Task Start(HeadlessPlayer caster)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SendRpc(use);
            await Watch(caster, cursor, frame => BytePattern.Parse("80 ac 7a 01 00 00 00 00 00 00 00 00 00 00 00 00 00 40").IsMatch(frame.Bytes),
                $"{caster.Handle} casts for two seconds");
            var timer = await Watch(caster, cursor, frame => BytePattern.Parse("80 bd 01 7a 01 04 00 00 20 41 01").IsMatch(frame.Bytes),
                $"{caster.Handle} starts the stock-shaped 10-second reuse");
            var on = await Watch(caster, cursor, frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(parent, true)),
                $"{caster.Handle} enables the aura");
            var rpcs = caster.World.RpcSnapshot();
            Check.That(rpcs.IndexOf(timer) < rpcs.IndexOf(on), "The own timer precedes exclusive on in the captured order");
            Check.That(context.Server.DebugLog().Contains($"[ABILITY-COST] char={caster.CharId} ability=378 paid=10 IS="),
                "The real cast pays the stored 10 IS");
        }

        async Task Stop(HeadlessPlayer caster)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SendRpc(dismiss);
            await Watch(caster, cursor, frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(parent, false)),
                $"{caster.Handle} disables the aura with stock dismissal");
        }

        async Task<(ushort Melee, ushort Viral)> Pair(HeadlessPlayer actor, int cursor, int meleeValue, int viralValue)
        {
            var meleeAdd = await Watch(actor, cursor, frame => IsBonus(frame, child, melee, meleeValue, 5), "A melee defense snapshot lands");
            var viralAdd = await Watch(actor, cursor, frame => IsBonus(frame, child, viral, viralValue, 5), "A viral defense snapshot lands");
            return (BitConverter.ToUInt16(meleeAdd.Bytes, 4), BitConverter.ToUInt16(viralAdd.Bytes, 4));
        }

        static List<ManageBonusRecord> Defenses(HeadlessPlayer actor, int cursor) => actor.World.RpcSnapshot().Skip(cursor)
            .Select(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record) ? record : null)
            .OfType<ManageBonusRecord>().Where(record => record.SourceId == child && record.TargetId is melee or viral).ToList();

        static bool IsBonus(RpcRecord frame, ushort source, ushort stat, int value, float duration) =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == source && record.TargetId == stat &&
            record.SignedValue == value && record.Duration == duration && record.AddRemoveType == 0;

        static bool IsRemoval(RpcRecord frame, ushort source, ushort stat, ushort recordId) =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == source && record.TargetId == stat &&
            record.RecordId == recordId && record.AddRemoveType == 1;

        static async Task<RpcRecord> Watch(HeadlessPlayer player, int cursor, Func<RpcRecord, bool> match, string description)
        {
            RpcRecord? result = null;
            await player.WaitUntilAsync(() => (result = player.World.RpcSnapshot().Skip(cursor).FirstOrDefault(match)) is not null,
                description, HeadlessPlayer.DefaultTimeout);
            return result!;
        }

        Task<HeadlessPlayer> At(string name, ushort level, (int X, int Z) stand, bool caster = false)
        {
            ushort[] roots = caster ? new ushort[] { 2, 17, 1006, 383, parent } : new ushort[] { 2, 17 };
            var profile = roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
                .Distinct().Select(id => (id, level)).ToArray();
            Check.That(profile.Sum(row => AbilityMemory.Cost(row.id)) <= AbilityMemory.ForLevel(level), $"{name}'s profile fits memory");
            return context.AddPlayerAsync(new CharacterSeed(name)
            {
                Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level), X = stand.X, Y = y, Z = stand.Z,
                Organization = 1, InnerStrength = 1000, MaxInnerStrength = 1000, OwnedLoadedAbilities = profile
            });
        }
    }
}
