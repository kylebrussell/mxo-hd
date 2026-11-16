using System.Text.Json;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;
public class ZionBluepillLobbyCheckpointTests
{
    [Fact] public void VerbatimCapturePreservesAcceptedIdentityAndOnlyEarlierReceipts()
    {
        var s=ZionBluepillLobbyCheckpoint.Verify(ZionBluepillLobbyCheckpoint.Payload);
        Assert.Equal(101210,s.Mission);Assert.Equal(1,s.Phase);Assert.Equal(49,s.Instance);Assert.Empty(s.Items);Assert.Empty(s.Done);Assert.Equal(new[]{1,2},s.EverDone);
        Assert.Equal(30,ZionBluepillLobbyCheckpoint.EarlierReceipts.Length);Assert.Contains(986,ZionBluepillLobbyCheckpoint.EarlierReceipts);Assert.Contains(101209,ZionBluepillLobbyCheckpoint.EarlierReceipts);Assert.DoesNotContain(101210,ZionBluepillLobbyCheckpoint.EarlierReceipts);
        var current=MervFollowupStreetEntry.ForSite(s)!;Assert.Equal(921699406u,current.Lobby.Key);Assert.Equal(new[]{-2800d,145,-8300},current.Lobby.Position);Assert.Equal(new[]{-2600d,95,-8300},current.Lobby.Standing);
    }
    [Fact] public void CorruptCaptureAndMissingActualContextRefuseBeforeSql()
    {
        var b=ZionBluepillLobbyCheckpoint.Payload;b[^1]^=1;Assert.Throws<InvalidDataException>(()=>ZionBluepillLobbyCheckpoint.Verify(b));
        var context=new NativeQaBootstrapContext("unused",Array.Empty<string>(),new());
        Assert.Throws<InvalidDataException>(()=>ZionBluepillLobbyCheckpoint.RequireContext(context));
        Assert.Throws<InvalidDataException>(()=>ZionBluepillLobbyCheckpoint.SeedSql(context,new[]{0d,95,0}));
    }
    [Fact] public void IncidentalGoalHealthItemOrSiteChangesAreNotRepaired()
    {
        var original=ZionBluepillLobbyCheckpoint.Verify(ZionBluepillLobbyCheckpoint.Payload);
        void Refuses(Action<hds.MissionProgress.SavedState> change){var s=ZionBluepillLobbyCheckpoint.Verify(ZionBluepillLobbyCheckpoint.Payload);change(s);Assert.Throws<InvalidDataException>(()=>ZionBluepillLobbyCheckpoint.RequireUnchanged(original,s));}
        Refuses(s=>s.Done=new[]{3});Refuses(s=>s.Phase=2);Refuses(s=>s.Items[6796]=1);Refuses(s=>s.Mobs[0].Health--);Refuses(s=>s.OpenFloorCurrentSiteIndex=0);
        ZionBluepillLobbyCheckpoint.RequireUnchanged(original,ZionBluepillLobbyCheckpoint.Verify(ZionBluepillLobbyCheckpoint.Payload));
    }
    [Fact] public void CapturedPublicGeometryAndCurrentPhaseAreIndependentlyRead()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"data","sector_properties.csv")))root=Directory.GetParent(root)?.FullName??throw new InvalidDataException("Repository missing");
        hds.SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));hds.NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        var s=ZionBluepillLobbyCheckpoint.Verify(ZionBluepillLobbyCheckpoint.Payload);var p=MissionPublicStreetProof.ReadCurrent(root,s);
        Assert.Equal(921698318u,p.Door.Key);Assert.Equal(new[]{-2600d,95,-8300},p.Lobby);Assert.NotEmpty(p.Outside);Assert.NotEmpty(p.Inside);
        s.Phase=0;Assert.Throws<InvalidDataException>(()=>MissionPublicStreetProof.ReadCurrent(root,s));
    }
    [Fact] public void ExactSourcePinCannotBeMissingOrWrong()
    {
        string pin=new('A',64);ZionBluepillLobbyCheckpoint.RequireSource(pin,pin.ToLowerInvariant());
        Assert.Throws<InvalidDataException>(()=>ZionBluepillLobbyCheckpoint.RequireSource(null,pin));Assert.Throws<InvalidDataException>(()=>ZionBluepillLobbyCheckpoint.RequireSource(new('B',64),pin));
    }
    [Fact] public void LiteralActualLobbyWireRejectsOldFloorKeyAndPose()
    {
        byte[] exact=Convert.FromHexString("809E000000000000E0A5C00000000000206240000000000036C0C0010000004E04F036");
        Assert.True(ZionBluepillLobbyCheckpoint.LobbyWaypoint(exact));Assert.False(ZionBluepillLobbyCheckpoint.LobbyWaypoint(exact[..^1]));
        var floor=exact.ToArray();BitConverter.GetBytes(2945d).CopyTo(floor,11);BitConverter.GetBytes(921699392u).CopyTo(floor,31);Assert.False(ZionBluepillLobbyCheckpoint.LobbyWaypoint(floor));
    }
    [Fact] public void KnownModeHasCustomPreparationAndImmutableScalarMetadata()
    {
        var mode=NativeQaModes.RequireKnown(ZionBluepillLobbyCheckpoint.Mode);Assert.IsType<NativeQa.Modes.ZionBluepillLobbyCheckpointMode>(mode);Assert.Empty(mode.Plan.AfterLoadout);Assert.Empty(mode.Plan.AfterHelpers);
        Assert.True(mode.Plan.DevCommands);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
        Assert.True(mode.Plan.ExtraQaMetadata["CustomGameplayPreparation"].GetBoolean());Assert.All(mode.Plan.ExtraQaMetadata.Values,v=>Assert.DoesNotContain(v.ValueKind,new[]{JsonValueKind.Array,JsonValueKind.Object}));
    }
}
