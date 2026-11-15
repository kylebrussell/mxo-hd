using System.Globalization;
using System.Text.Json;
using hds;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    [Fact] public void ActualDataLoaderFirstLobbyAliasResolvesOnlySavedContextWithoutChangingWitness()
    {
        var prepared=Prepared();var p=prepared.Progress;p.NextPhase();p.site=prepared.Site;p.sites.Add(prepared.Site);p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=prepared.Pending.Witness;
        var index=new StaticObjectIndex();int rows=0;
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data","staticObjects_slums_1.csv")).Skip(1))
        {
            var d=line.Split(',');if(d.Length<11)continue;if(d[2]!="040f6022")continue;
            var raw=new StaticWorldObject { metrId=ushort.Parse(d[0]),sectorID=ushort.Parse(d[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(d[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(d[3])),type=Convert.FromHexString(d[4][..4]),exterior=bool.Parse(d[5]),pos_x=double.Parse(d[6],CultureInfo.InvariantCulture),pos_y=double.Parse(d[7],CultureInfo.InvariantCulture),pos_z=double.Parse(d[8],CultureInfo.InvariantCulture),rot=double.Parse(d[9],CultureInfo.InvariantCulture),quat=d[10] };
            index.Add(raw);rows++;
        }
        Assert.True(rows>1);var first=index.Find(0x22600f04,1);Assert.Equal(39700,first.pos_x);Assert.Equal(-1,first.rot);Assert.Equal(40300,prepared.Site.lobbyDoor.pos_x);
        string before=JsonSerializer.Serialize(prepared.Pending,MissionProgress.SaveJson);int calls=prepared.Provider.EntryCalls;
        var overlay=MissionNpcInteriorAdmission.Overlay(p,first,prepared.Provider)!;
        Assert.Equal(MissionElevatorPoses.OverlayKind.Resolved,overlay.Kind);Assert.Equal(40000,overlay.Position.X);Assert.Equal(25500,overlay.Position.Z);Assert.Equal(prepared.Pending.Witness.Lobby.Quaternion,overlay.Quaternion);
        Assert.Equal(before,JsonSerializer.Serialize(prepared.Pending,MissionProgress.SaveJson));Assert.Equal(calls,prepared.Provider.EntryCalls);
    }
    [Theory] [InlineData("position")] [InlineData("rotation")] [InlineData("quaternion")] [InlineData("goid")] [InlineData("sector")] [InlineData("staticId")] [InlineData("exterior")] [InlineData("woodPosition")] [InlineData("woodPairedGoid")]
    public void SameKeyDoesNotAuthorizeUnknownSignatureOrBorrowAnotherGoid(string change)
    {
        var prepared=Prepared();var p=prepared.Progress;p.NextPhase();p.site=prepared.Site;p.sites.Add(prepared.Site);p.npcInteriorResolvedPhase=2;p.npcInteriorWitnesses[2]=prepared.Pending.Witness;
        var raw=change.StartsWith("wood")?Wood():MissionNpcInteriorPlanning.ReadRaw(prepared.Pending.Witness.Lobby.RawDoorJson);
        switch(change)
        {
            case "position":case "woodPosition":raw.pos_x++;break;
            case "rotation":raw.rot++;break;
            case "quaternion":raw.quat="000000000000000000000000000080bf";break;
            case "goid":raw.type=BitConverter.GetBytes(417u);break;
            case "woodPairedGoid":raw.type=BitConverter.GetBytes(343u);break;
            case "sector":raw.sectorID++;break;
            case "staticId":raw.staticId++;break;
            case "exterior":raw.exterior=true;break;
        }
        string before=JsonSerializer.Serialize(prepared.Pending,MissionProgress.SaveJson);
        Assert.Equal(MissionElevatorPoses.OverlayKind.Refused,MissionNpcInteriorAdmission.Overlay(p,raw,prepared.Provider)!.Kind);
        Assert.Equal(before,JsonSerializer.Serialize(prepared.Pending,MissionProgress.SaveJson));
    }
    [Fact] public void AnotherWorldOrUnownedKeyCannotResolveThroughSavedAliasContext()
    {
        var prepared=Prepared();var p=prepared.Progress;p.npcInteriorWitnesses[2]=prepared.Pending.Witness;
        var raw=MissionNpcInteriorPlanning.ReadRaw(prepared.Pending.Witness.Lobby.RawDoorJson);raw.metrId=2;Assert.Null(MissionNpcInteriorAdmission.Overlay(p,raw,prepared.Provider));
        raw.metrId=1;raw.mxoStaticId++;Assert.Null(MissionNpcInteriorAdmission.Overlay(p,raw,prepared.Provider));
    }
}

