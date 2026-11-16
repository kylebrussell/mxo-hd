using System.Text.Json;
using System.Text.Json.Nodes;
using hds;

namespace hds.Tests;

public class NativeBoundFurnitureEnvelopeReviewTests
{
    static MissionProgress Progress()=>new(new Mission{id=301210,district=1});
    // Framing-only control: this does not certify a source, NAV path, owner or door.
    static JsonObject Envelope()=>new()
    {
        ["Version"]=1,["Kind"]="BoundFurnitureRoutes",["EvidenceDigest"]=new string('A',64),
        ["FurnitureManifest"]=MissionBoundFurniturePoses.AuditedManifestSha256,
        ["Choices"]=new JsonArray(new JsonObject{["ObjectId"]=1,["Goid"]=1450,["RawSpotJson"]="{}"}),
        ["Routes"]=new JsonArray(new JsonObject
        {
            ["ObjectId"]=1,["Goid"]=1450,["ProfileManifestSha256"]=MissionBoundFurniturePoses.AuditedManifestSha256,
            ["RawSourceJson"]="{}",["Region"]=114,
            ["NativePosition"]=JsonSerializer.SerializeToNode(new double[]{41445,1049.98193359375,-27900}),
            ["NativeQuaternion"]="00000000F304353F00000000F304353F",
            ["Standing"]=JsonSerializer.SerializeToNode(new double[]{41545,1095,-27900}),
            ["Target"]=new JsonObject{["Region"]=114,["Polygon"]=86},
            ["Nodes"]=new JsonArray(new JsonObject{["Region"]=99,["Polygon"]=1},new JsonObject{["Region"]=114,["Polygon"]=86}),
            ["Doors"]=new JsonArray()
        })
    };
    static JsonElement Json(JsonNode n)=>JsonSerializer.SerializeToElement(n);
    static JsonObject Row(JsonObject e)=>e["Routes"]![0]!.AsObject();

    [Fact]public void WellFramedMetadataAloneDoesNotPublishOrAcquireAnOwnedRoute()
    {
        var progress=Progress();var json=Json(Envelope());
        MissionStreetFurnitureRoutes.Shape(progress,json);
        Assert.Empty(progress.streetWitnesses);
        Assert.Empty(MissionStreetFurnitureRoutes.CurrentDoors(progress));
        var noContext=new MissionStreetSelection(3,progress.mission.id,progress.persistenceId,progress.startedAt,0,null!,"Objects",null!,null!,furnitureRoutes:json);
        Assert.Throws<MissionPlacementRestoreException>(()=>MissionStreetFurnitureRoutes.ValidateShapeOwner(progress,noContext));
        Assert.Empty(progress.streetWitnesses);
    }

    [Theory]
    [InlineData("unknown-field")][InlineData("missing-field")][InlineData("version")][InlineData("kind")]
    [InlineData("manifest")][InlineData("empty-choices")][InlineData("duplicate-choice")][InlineData("null-row")]
    [InlineData("null-target")][InlineData("empty-nodes")][InlineData("duplicate-node")][InlineData("wrong-target")]
    [InlineData("phone")][InlineData("computer")][InlineData("wrong-profile")][InlineData("all326")]
    [InlineData("bad-quaternion")][InlineData("bad-point")][InlineData("duplicate-route")]
    public void RecognizedMalformedEnvelopeRefusesWithoutMutatingMissionAuthority(string change)
    {
        var e=Envelope();var row=Row(e);
        switch(change)
        {
            case "unknown-field":e["Unexpected"]=true;break;case "missing-field":e.Remove("Choices");break;
            case "version":e["Version"]=2;break;case "kind":e["Kind"]="Bound326Routes";break;
            case "manifest":e["FurnitureManifest"]=new string('0',64);break;case "empty-choices":e["Choices"]=new JsonArray();break;
            case "duplicate-choice":e["Choices"]!.AsArray().Add(e["Choices"]![0]!.DeepClone());break;
            case "null-row":e["Routes"]=new JsonArray((JsonNode?)null);break;case "null-target":row["Target"]=null;break;
            case "empty-nodes":row["Nodes"]=new JsonArray();break;case "duplicate-node":row["Nodes"]!.AsArray().Add(row["Nodes"]![1]!.DeepClone());break;
            case "wrong-target":row["Target"]!["Region"]=115;break;case "phone":row["Goid"]=1401;break;case "computer":row["Goid"]=3;break;
            case "wrong-profile":row["ProfileManifestSha256"]=MissionBoundObjectPoses.AuditedManifestSha256;break;
            case "all326":row["Goid"]=326;row["ProfileManifestSha256"]=MissionBoundObjectPoses.AuditedManifestSha256;e["Choices"]![0]!["Goid"]=326;break;
            case "bad-quaternion":row["NativeQuaternion"]="not-a-quaternion";break;case "bad-point":row["Standing"]=new JsonArray(1,2);break;
            case "duplicate-route":e["Routes"]!.AsArray().Add(row.DeepClone());break;
        }
        var progress=Progress();
        var error=Assert.Throws<MissionPlacementRestoreException>(()=>MissionStreetFurnitureRoutes.Shape(progress,Json(e)));
        Assert.Same(progress,error.Progress);
        Assert.Empty(progress.streetWitnesses);
    }

    [Theory][InlineData(1)][InlineData(2)]
    public void OldStreetVersionCannotAcquireNewFurnitureAuthority(int version)
    {
        var progress=Progress();
        var old=new MissionStreetSelection(version,progress.mission.id,progress.persistenceId,progress.startedAt,0,null!,"Objects",null!,null!,furnitureRoutes:Json(Envelope()));
        Assert.Throws<MissionPlacementRestoreException>(()=>MissionStreetFurnitureRoutes.ValidateShapeOwner(progress,old));
        Assert.Empty(progress.streetWitnesses);
    }
}
