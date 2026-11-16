using System.Text.Json;
using System.Text.Json.Nodes;
namespace HeadlessClient;

// Package-only projection for the canonical reader's single-actor API. Never changes saved state.
public static class ZionAfter1212ActorRoute
{
    public static MissionNpcInteriorProof.Plan Select(MissionNpcInteriorProof.Plan plan,int actorIndex)
    {
        var root=plan.Witness;
        var proofs=root.GetProperty("Actors");var roles=root.GetProperty("Selection").GetProperty("Actors");
        if(proofs.ValueKind!=JsonValueKind.Array||roles.ValueKind!=JsonValueKind.Array||proofs.GetArrayLength()!=roles.GetArrayLength()||actorIndex<0||actorIndex>=proofs.GetArrayLength())
            throw new InvalidDataException("NPC route actor index/count");
        var actor=roles[actorIndex];string role=$"{actor.GetProperty("Kind").GetString()}:{actor.GetProperty("Id").GetInt32()}:{actor.GetProperty("Index").GetInt32()}";
        if(proofs[actorIndex].GetProperty("Role").GetString()!=role)throw new InvalidDataException("NPC route exact actor role association");
        var copy=JsonNode.Parse(root.GetRawText())!;
        copy["Actors"]=new JsonArray(JsonNode.Parse(proofs[actorIndex].GetRawText()));
        copy["Selection"]!["Actors"]=new JsonArray(JsonNode.Parse(actor.GetRawText()));
        using var doc=JsonDocument.Parse(copy.ToJsonString());
        return plan with { Witness=doc.RootElement.Clone() };
    }
    public static MissionNpcInteriorProof.RouteStep[] Route(string repository,MissionNpcInteriorProof.Plan plan,int actorIndex)=>
        MissionNpcInteriorProof.Route(repository,Select(plan,actorIndex));
}
