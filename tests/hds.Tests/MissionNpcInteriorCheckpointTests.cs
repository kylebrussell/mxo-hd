using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using G = hds.MissionOpenFloorResolver;
using E = hds.MissionInteriorEvidence;
namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionNpcInteriorCheckpointTests
{
    private static string Repo { get { string p = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(p, "hds.sln"))) p = Path.GetDirectoryName(p)!; return p; } }
    private static readonly Lazy<E> Primary = new Lazy<E>(() => E.LoadAudited(Path.Combine(Repo, "data", "mission_interior_evidence", "1"), 1, 550));
    private static StaticWorldObject Raw(uint key, ushort goid, double x, double y, double z, double yaw, string q) => new StaticWorldObject
    { metrId = 1, sectorID = (ushort)(key >> 20), staticId = key & 0xfffff, mxoStaticId = key, type = BitConverter.GetBytes((uint)goid), pos_x = x, pos_y = y, pos_z = z, rot = yaw, quat = q };
    private static StaticWorldObject Marker() => Raw(0x226005f5, 19, 38490, 6145, 26980, 148.999967603, "00000000e946773f00000000f083843e");
    private static StaticWorldObject Wood() => Raw(0x2260056b, 346, 40800, 6145, 28100, 88.9999883795, "00000000f204353f00000000f304353f");
    private static MissionSite Site()
    {
        var s = new MissionSite { metrId = 1, style = "Office", floorY = 6145, minX = 37300, maxX = 40950, minZ = 22050, maxZ = 29550,
            elevatorDoor = Raw(0x22602817, 419, 40300, 6145, 25800, 90, "0000000000000000000000000000803f"),
            lobbyDoor = Raw(0x22600f04, 419, 40300, 145, 25800, 90, "0000000000000000000000000000803f") };
        s.shaft.Add(s.lobbyDoor); s.shaft.Add(s.elevatorDoor); var marker = Marker(); s.npcSpots.Add(new[] { marker.pos_x, marker.pos_y - 50, marker.pos_z, marker.rot });
        // Finite unused spot intentionally has no source/NAV proof; the selected one-role prefix must not borrow or validate it.
        s.npcSpots.Add(new[] { 40300d, 6095, 25800, 0 }); return s;
    }
    private static Mission Definition()
    {
        var m = new Mission { id = 201201, district = 1 }; m.phases.Add(new MissionPhase { site = "Office" }); m.phases.Add(new MissionPhase { site = "Office" }); m.phases.Add(new MissionPhase { site = "Office" });
        m.npcs.Add(new MissionNpc { id = 6, at = 3, phase = 0, friendly = true, arrival = new MissionArrival() }); return m;
    }
    private static WorldClient Client(MissionProgress progress)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient)); c.playerData = new ClientData(); c.playerData.setCharID(110); c.playerData.setDistrictId(1); c.playerData.setOnWorld(true);
        c.playerData.activeMission = progress.mission; c.playerData.missionProgress = progress; c.Alive = true;
        typeof(WorldClient).GetField("lastUsedTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, new WorldSendFence()); return c;
    }
    private sealed class Provider : IMissionNpcInteriorEvidenceProvider
    {
        internal int Reads, EntryCalls; internal Action? OnEntry; internal bool MissingMarker;
        public E Evidence(ushort world, ushort sector) { Reads++; Assert.Equal((ushort)1, world); Assert.Equal((ushort)550, sector); return Primary.Value; }
        public StaticWorldObject[] Markers(ushort world, ushort sector) { Reads++; return MissingMarker ? Array.Empty<StaticWorldObject>() : new[] { Marker() }; }
        public StaticWorldObject Door(ushort world, uint key) { Reads++; return key == 0x2260056b ? Wood() : null!; }
        public MissionOpenFloorEntry.Result Entry(ushort world, G.Position door, double x, double z, string evidence)
        {
            EntryCalls++; OnEntry?.Invoke(); bool lobby = door.Y == 145;
            return MissionOpenFloorEntry.Calculate(door, x, z, evidence, _ => true, _ => new NavLocation { sector = 550, region = (ushort)(lobby ? 57 : 125), polygon = (ushort)(lobby ? 230 : 30), floorY = lobby ? 0 : 6000, indoors = true });
        }
    }
    private static (MissionProgress Progress, MissionSite Site, Provider Provider, MissionNpcInteriorPending Pending) Prepared()
    {
        var p = new MissionProgress(Definition()) { phaseIndex = 1, persistenceId = "npc-identity", startedAt = 123 }; var site = Site(); var provider = new Provider(); var c = Client(p);
        Assert.True(MissionNpcInteriorPlanning.Prepare(c, p, 2, site, provider, out var plan, out var selected, out var why), why); Assert.True(plan.BeforeCurrent(c)); return (p, site, provider, selected);
    }
    [Fact] public void PurePlanPreservesStoryStageWireByteRolePrefixRawSourcesAndNoRuntimeState()
    {
        var p = new MissionProgress(Definition()) { phaseIndex = 1, clock = () => 100 }; var c = Client(p); var site = Site(); var provider = new Provider();
        string before = JsonSerializer.Serialize(p.Capture(47, Array.Empty<Mob>()), MissionProgress.SaveJson);
        Assert.True(MissionNpcInteriorPlanning.Prepare(c, p, 2, site, provider, out var plan, out var pending, out var why), why);
        var actor = Assert.Single(pending.Selection.Actors); Assert.Equal("Npc", actor.Kind); Assert.Equal(6, actor.Id); Assert.Equal((byte)0, actor.WirePhase); Assert.Equal(2, pending.Selection.Phase);
        Assert.Equal(new[] { 38490d, 6095, 26980, 148.999967603 }, actor.Standing); Assert.Equal(2, provider.EntryCalls);
        Assert.Equal(before, JsonSerializer.Serialize(p.Capture(47, Array.Empty<Mob>()), MissionProgress.SaveJson)); Assert.Null(p.npcInteriorPending); Assert.Null(p.site); Assert.Empty(p.sites); Assert.True(plan.BeforeCurrent(c));
    }
    [Fact] public void PendingModelsOwnArraysAndRoundtripWithoutRefillingMissingWitness()
    {
        var (p, site, provider, original) = Prepared(); var selected = original.Selection; var actor = selected.Actors[0]; var standing = actor.Standing; standing[0] = 0;
        var input = new[] { 1d, 2, 3, 4 }; var isolated = new MissionNpcInteriorActor("Npc", 1, 0, 0, false, "raw", input); input[0] = 99; Assert.Equal(1, isolated.Standing[0]);
        Assert.Equal(38490, actor.Standing[0]); var actors = selected.Actors; actors[0] = null!; Assert.NotNull(selected.Actors[0]);
        p.npcInteriorPending = new MissionNpcInteriorPending(2, MissionNpcInteriorPlanning.Kind, selected, null!, "RetainedExactRefusal"); p.itemSlots[6783] = 6;
        var saved = p.CaptureWithNpcCatalog(47, Array.Empty<Mob>(), new[] { site }); Assert.Equal(2, saved.Version); Assert.NotEqual(JsonValueKind.Undefined, saved.NpcInterior.ValueKind);
        string before = saved.NpcInterior.GetRawText(); int reads = provider.Reads;
        var restored = MissionProgress.RestoreWithNpcCatalog(p.mission, saved, new[] { site }, provider);
        Assert.Equal(reads, provider.Reads); Assert.Equal("RetainedExactRefusal", restored.npcInteriorPending.Refusal); Assert.Null(restored.npcInteriorPending.Witness); Assert.Equal((ushort)6, restored.itemSlots[6783]);
        Assert.Equal(before, restored.CaptureWithNpcCatalog(47, Array.Empty<Mob>(), new[] { site }).NpcInterior.GetRawText());
    }
    [Fact] public void PersistedWitnessRevalidatesSavedEntryAndRouteWithoutNewEntryCalculation()
    {
        var (p, site, provider, pending) = Prepared(); p.npcInteriorPending = pending; p.itemSlots[6783] = 6;
        var saved = p.CaptureWithNpcCatalog(47, Array.Empty<Mob>(), new[] { site }); int calculations = provider.EntryCalls;
        var restored = MissionProgress.RestoreWithNpcCatalog(p.mission, saved, new[] { site }, provider);
        Assert.Equal(calculations, provider.EntryCalls); Assert.NotNull(restored.npcInteriorPending.Witness); Assert.Equal(pending.Witness.Floor.Position, restored.npcInteriorPending.Witness.Floor.Position);
        Assert.Equal(pending.Selection.Actors[0].RawMarkerJson, restored.npcInteriorPending.Selection.Actors[0].RawMarkerJson); Assert.Equal((ushort)6, restored.itemSlots[6783]);
    }
    [Theory] [InlineData("primitive")] [InlineData("null")] [InlineData("missing")] [InlineData("wrongVersion")] [InlineData("wrongKind")] [InlineData("truncatedMarker")] [InlineData("duplicate")]
    public void RecognizedMalformedSubtreeQuarantinesBeforeEvidenceAndPreservesIdentityItemsAndInput(string kind)
    {
        var (p, site, provider, pending) = Prepared(); p.npcInteriorPending = pending; p.itemSlots[6783] = 6; var saved = p.CaptureWithNpcCatalog(47, Array.Empty<Mob>(), new[] { site });
        JsonNode node = JsonNode.Parse(saved.NpcInterior.GetRawText())!; string raw;
        if (kind == "primitive") raw = "7";
        else if (kind == "null") raw = "null";
        else if (kind == "missing") { node.AsObject().Remove("Witnesses"); raw = node.ToJsonString(); }
        else if (kind == "wrongVersion") { node["Version"] = 1; raw = node.ToJsonString(); }
        else if (kind == "wrongKind") { node["Kind"] = "Object"; raw = node.ToJsonString(); }
        else if (kind == "truncatedMarker") { node["Pending"]!["Selection"]!["Actors"]![0]!["RawMarkerJson"] = "{"; raw = node.ToJsonString(); }
        else raw = node.ToJsonString().Insert(1, "\"Version\":2,");
        saved.NpcInterior = JsonSerializer.Deserialize<JsonElement>(raw); string original = JsonSerializer.Serialize(saved, MissionProgress.SaveJson); int reads = provider.Reads;
        saved = JsonSerializer.Deserialize<MissionProgress.SavedState>(original, MissionProgress.SaveJson)!;
        if (kind == "null") Assert.Equal(JsonValueKind.Null, saved.NpcInterior.ValueKind);
        var ex = Assert.Throws<MissionPlacementRestoreException>(() => MissionProgress.RestoreWithNpcCatalog(p.mission, saved, new[] { site }, provider));
        Assert.Equal(reads, provider.Reads); Assert.Equal(p.persistenceId, ex.Progress.persistenceId); Assert.Equal((ushort)6, ex.Progress.itemSlots[6783]); Assert.Equal(original, JsonSerializer.Serialize(saved, MissionProgress.SaveJson));
        var quarantined = MissionPersistence.RestoreForWorld(p.mission, saved, new[] { site }); Assert.NotNull(quarantined.restoreRefusal); Assert.Equal((ushort)6, quarantined.itemSlots[6783]);
    }
    [Fact] public void NewFieldsRequireExplicitVersion2AndDoNotMigrateLegacyState()
    {
        var p = new MissionProgress(Definition()) { phaseIndex = 1 }; var legacy = p.Capture(47, Array.Empty<Mob>()); string raw = JsonSerializer.Serialize(legacy, MissionProgress.SaveJson);
        Assert.Equal(1, legacy.Version); Assert.Equal(JsonValueKind.Undefined, legacy.NpcInterior.ValueKind); Assert.DoesNotContain("NpcInterior", raw);
        var restored = MissionProgress.RestoreFromCatalog(p.mission, legacy, Array.Empty<MissionSite>()); Assert.Null(restored.CaptureNpcInterior());
        legacy.Items[6783] = 6; legacy.NpcInterior = JsonSerializer.Deserialize<JsonElement>("null");
        legacy = JsonSerializer.Deserialize<MissionProgress.SavedState>(JsonSerializer.Serialize(legacy, MissionProgress.SaveJson), MissionProgress.SaveJson)!;
        Assert.Equal(JsonValueKind.Null, legacy.NpcInterior.ValueKind);
        var quarantine = MissionPersistence.RestoreForWorld(p.mission, legacy, Array.Empty<MissionSite>()); Assert.NotNull(quarantine.restoreRefusal); Assert.Equal((ushort)6, quarantine.itemSlots[6783]); Assert.Equal(1, legacy.Version);
    }
    [Fact] public void OrdinaryVersion2AbsentShapeAndNpcSnapshotRollbackBodyRemainExact()
    {
        var p = new MissionProgress(Definition()) { phaseIndex = 1 }; var ordinary = p.Capture(47, Array.Empty<Mob>());
        ordinary.Version = 2; ordinary.OpenFloorSites = Array.Empty<MissionOpenFloorSite>(); ordinary.OpenFloorCurrentSiteIndex = -1;
        string oldShape = JsonSerializer.Serialize(ordinary, MissionProgress.SaveJson);
        var oldRestored = MissionProgress.RestoreFromCatalog(p.mission, JsonSerializer.Deserialize<MissionProgress.SavedState>(oldShape, MissionProgress.SaveJson)!, Array.Empty<MissionSite>());
        Assert.Equal(JsonValueKind.Undefined, oldRestored.Capture(47, Array.Empty<Mob>()).NpcInterior.ValueKind);
        Assert.Equal(oldShape, JsonSerializer.Serialize(oldRestored.Capture(47, Array.Empty<Mob>()), MissionProgress.SaveJson));
        var prepared = Prepared(); prepared.Progress.npcInteriorPending = prepared.Pending; prepared.Progress.itemSlots[6783] = 6;
        string snapshot = JsonSerializer.Serialize(prepared.Progress.CaptureWithNpcCatalog(47, Array.Empty<Mob>(), new[] { prepared.Site }), MissionProgress.SaveJson);
        // Exercise the actual capture/restore body used by rollback, without claiming a live SQL transaction.
        prepared.Progress.npcInteriorPending = null!; prepared.Progress.itemSlots.Clear();
        var rollback = MissionProgress.RestoreWithNpcCatalog(prepared.Progress.mission, JsonSerializer.Deserialize<MissionProgress.SavedState>(snapshot, MissionProgress.SaveJson)!, new[] { prepared.Site }, prepared.Provider);
        Assert.Equal(snapshot, JsonSerializer.Serialize(rollback.CaptureWithNpcCatalog(47, Array.Empty<Mob>(), new[] { prepared.Site }), MissionProgress.SaveJson)); Assert.Equal((ushort)6, rollback.itemSlots[6783]);
    }
    [Fact] public void AlreadyCommittedPendingIsReturnedExactlyWithoutProviderReadsOrRngSelection()
    {
        var (p, site, provider, pending) = Prepared(); p.npcInteriorPending = pending.WithRefusal("exact retry refusal"); int reads = provider.Reads; string raw = JsonSerializer.Serialize(p.npcInteriorPending);
        Assert.False(MissionNpcInteriorPlanning.Prepare(Client(p), p, 2, site, provider, out _, out var selected, out var why));
        Assert.Equal(reads, provider.Reads); Assert.Same(p.npcInteriorPending, selected); Assert.Equal("exact retry refusal", why); Assert.Equal(raw, JsonSerializer.Serialize(selected));
    }
    [Fact] public void OwnerBeforeStateRejectsPhasePendingOwnerLifeWorldAndTransportChanges()
    {
        var p = new MissionProgress(Definition()) { phaseIndex = 1 }; var c = Client(p); var stamp = new MissionNpcInteriorBefore(c, p, 2); Assert.True(stamp.Current(c));
        p.phaseIndex++; Assert.False(stamp.Current(c)); p.phaseIndex--; c.playerData.lifeGeneration++; Assert.False(stamp.Current(c)); c.playerData.lifeGeneration--;
        c.playerData.worldGeneration++; Assert.False(stamp.Current(c)); c.playerData.worldGeneration--;
        p.npcInteriorPending = new MissionNpcInteriorPending(2, "NpcInterior", null!, null!, "pending changed"); Assert.False(stamp.Current(c)); p.npcInteriorPending = null!;
        var owner = c.playerData; c.playerData = new ClientData(); Assert.False(stamp.Current(c)); c.playerData = owner;
        c.TransportFence.PublishIncoming(true, () => { }); Assert.False(stamp.Current(c)); c.TransportFence.Resume(c.TransportFence.Capture()); Assert.False(stamp.Current(c));
    }
    [Fact] public void SourceWorkCannotReturnAPlanOrPendingAfterItsCapturedLifeChanges()
    {
        var p = new MissionProgress(Definition()) { phaseIndex = 1 }; var c = Client(p); var provider = new Provider { OnEntry = () => c.playerData.lifeGeneration++ };
        Assert.False(MissionNpcInteriorPlanning.Prepare(c, p, 2, Site(), provider, out var plan, out var pending, out var why)); Assert.Null(plan); Assert.Null(pending); Assert.StartsWith("StaleNpcBefore", why); Assert.Null(p.npcInteriorPending);
    }
    [Fact] public void RoleOrderMatchesAuthoredNpcThenDefaultWaveAndRefusesUncertifiedSyntheticLap()
    {
        var p = new MissionProgress(Definition()) { phaseIndex = 1 }; p.mission.phases[2].enemy = "Machines"; p.mission.phases[2].enemies = 2;
        Assert.True(MissionNpcInteriorPlanning.Roles(p.mission, 2, out var roles, out var why), why); Assert.Equal(new[] { "Npc", "Wave", "Wave" }, roles.Select(r => r.Kind)); Assert.Equal(new[] { 0, 0, 1 }, roles.Select(r => r.Index));
        Assert.False(MissionNpcInteriorPlanning.Select(p, 2, Site(), new[] { Marker() }, out _, out why)); Assert.Equal("UncertifiedNpcSyntheticLap", why);
        p.mission.phases[2].enemiesAt = "player"; Assert.False(MissionNpcInteriorPlanning.Roles(p.mission, 2, out _, out why)); Assert.Equal("UnsupportedNpcWaveRelativeArrival", why);
    }
    [Fact] public void SavedRawMarkerCatalogLossQuarantinesWithoutChangingTheFixedProofOrItems()
    {
        var (p, site, provider, pending) = Prepared(); p.npcInteriorPending = pending; p.itemSlots[6783] = 6; var saved = p.CaptureWithNpcCatalog(47, Array.Empty<Mob>(), new[] { site }); string raw = saved.NpcInterior.GetRawText(); provider.MissingMarker = true;
        var ex = Assert.Throws<MissionPlacementRestoreException>(() => MissionProgress.RestoreWithNpcCatalog(p.mission, saved, new[] { site }, provider)); Assert.Contains("ChangedNpcRawMarkerCatalog", ex.Message); Assert.Equal((ushort)6, ex.Progress.itemSlots[6783]); Assert.Equal(raw, saved.NpcInterior.GetRawText());
    }
}
