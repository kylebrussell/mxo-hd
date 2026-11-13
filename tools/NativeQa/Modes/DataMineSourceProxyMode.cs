namespace NativeQa.Modes;
/// <summary>Diagnostic source placement only: no synthetic city origin or claim of walking from the archive arrival.</summary>
public sealed class DataMineSourceProxyMode : NativeQaMode
{
    public override string Name=>"datamine-source-proxy";
    public override NativeQaModePlan Plan{get;}=new(new ushort[]{2,17},-31200,16450,Array.Empty<string>(),
        new[]{"UPDATE characters SET district='archive_datamine',districtId=7,y=1495,alignment=3 WHERE charId=110"},Array.Empty<string>(),
        environmentOverrides:new(){["MXO_MOBS"]=""},extraQaMetadata:new(){["DataMinePartial"]=true,["DiagnosticSourceProxy"]=true,["NoCityOriginSession"]=true,["WorldMap"]=7,["RetailConstruct"]=17},
        readyMessages:new[]{"DIAGNOSTIC DataMine source8/1: check actual47267 ArchivistProxy appearance/radialUse -> existing OURS no-origin Mara fallback. This spawn is NOT a native walk, normal book arrival or retail return proof. Generated fallback remains. Main datamine profile separately checks normal book/cycle/origin."});
}
