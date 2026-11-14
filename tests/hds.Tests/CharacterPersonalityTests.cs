using hds;
using HeadlessClient;
namespace hds.Tests;
public class CharacterPersonalityTests
{
    private static string Root {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static bool ResolveAssets(CharacterCreation.Request request,out CharacterCreation.InitialState state)=>CreationAssets.Load(Root).TryResolve(request,out state);

    [Theory]
    [InlineData(299,8,11,8,5,8)][InlineData(300,8,5,8,11,8)][InlineData(301,5,8,11,8,8)][InlineData(302,11,8,5,8,8)]
    [InlineData(303,8,8,8,5,11)][InlineData(304,8,8,8,11,5)][InlineData(305,8,8,5,11,8)][InlineData(306,8,8,11,5,8)]
    [InlineData(307,11,5,8,8,8)][InlineData(308,5,11,8,8,8)]
    public void TenLiteralNativeVectors(uint trait,int f,int p,int r,int b,int v)
    {
        Assert.Equal(new[]{f,p,r,b,v},CharacterAttributes.All.Select(a=>(int)CharacterAttributes.BaseValue(a,trait)));
        Assert.Equal(40,CharacterAttributes.All.Sum(a=>CharacterAttributes.BaseValue(a,trait)));
        var values=CharacterAttributes.All.ToDictionary(a=>a,a=>CharacterAttributes.BaseValue(a,trait));
        Assert.Equal(new byte[]{0},CharacterAttributes.BuildSpentPoints(values,trait));
        foreach(byte a in CharacterAttributes.All)Assert.Equal(9,CharacterAttributes.NewLevelOf(a,(ushort)(values[a]+1),trait));
    }
    [Fact] public void NullLegacyIsExact299AndNoPointMigration()
    {
        foreach(byte a in CharacterAttributes.All)Assert.Equal(CharacterAttributes.BaseValue(a),CharacterAttributes.BaseValue(a,299));
        Assert.Equal(90,CharacterAttributes.All.Sum(a=>CharacterAttributes.SpentPoints(a,CharacterAttributes.LegacyValue(a))));
        Assert.Equal(Convert.FromHexString("03510000000400520000000B00540000000100"),CharacterAttributes.BuildSpentPoints(new()));
        Assert.Equal(299u,BitConverter.ToUInt32(CharacterPersonality.BackgroundPrefix(false,null)));
        Assert.Equal(36,CharacterPersonality.BackgroundPrefix(true,null).Length);
        Assert.Equal(299u,BitConverter.ToUInt32(CharacterPersonality.BackgroundPrefix(true,null),32));
    }
    [Theory][InlineData(0u)][InlineData(9u)][InlineData(298u)][InlineData(309u)][InlineData(4096u)][InlineData(0x8004d000u)][InlineData(0x1000134u)]
    public void AssignedInvalidIsNotSilentlyLegacy(uint trait)
    {
        Assert.Throws<InvalidOperationException>(()=>CharacterPersonality.Resolve(trait));
        Assert.False(CharacterCreation.TryParse(CharacterCreationWire.Create(trait,"First","Last","", ""),out _));
    }
    [Theory][InlineData(308u,78,5)][InlineData(304u,82,11)]
    public void AllocationUsesLockedTraitForPointBudgetAndNativeNormalization(uint trait,byte attribute,ushort initial)
    {
        var rows=CharacterAttributes.All.Select((a,i)=>new AttributeAllocation.Row((uint)(i+1),CharacterAttributes.SlotOf(a),CharacterAttributes.RowValueOf(a),CharacterAttributes.BaseValue(a,trait))).ToArray();
        var raises=new[]{new CharacterAttributes.Raise{Attribute=attribute,Points=1}};
        Assert.False(AttributeAllocation.TryPlan(rows,raises,0,out _,trait));
        Assert.True(AttributeAllocation.TryPlan(rows,raises,PlayerProgression.ExperienceForLevel(2),out var values,trait));
        Assert.Equal(initial+1,values[attribute]);
        byte[] reply=AttributeAllocation.Reply(new(true,"",values,trait),raises);Assert.Equal(9,reply[^1]);
        Assert.Equal(new byte[]{1,attribute,0,0,0,1,0},CharacterAttributes.BuildSpentPoints(values,trait));
    }
    [Fact] public void ExactNativeOffsetsAndMalformedBodiesFailBeforePersistence()
    {
        byte[] valid=CharacterCreationWire.Create(308,"First","Last","Description");
        Assert.Equal(103u,BitConverter.ToUInt32(valid,9));Assert.Equal(0u,BitConverter.ToUInt32(valid,25));Assert.Equal(0u,BitConverter.ToUInt32(valid,29));
        Assert.True(CharacterCreation.TryParse(valid,out var request));Assert.Equal(103u,request.Appearance[2]);Assert.Equal(308u,request.Trait);
        for(int size=0;size<valid.Length;size++)Assert.False(CharacterCreation.TryParse(valid[..size],out _));
        Assert.False(CharacterCreation.TryParse(valid.Concat(new byte[]{0}).ToArray(),out _));
        foreach(int offset in new[]{69,71,73,75}){var bad=(byte[])valid.Clone();bad[offset]--;Assert.False(CharacterCreation.TryParse(bad,out _));}
        var high=(byte[])valid.Clone();high[8]=1;Assert.False(CharacterCreation.TryParse(high,out _));
        var nul=(byte[])valid.Clone();nul[79]=0;Assert.False(CharacterCreation.TryParse(nul,out _));
    }
    [Fact] public void OnlyActualStartingClothesCanBeGrantedAndOptionalZerosRemainValid()
    {
        byte[] Valid()=>CharacterCreationWire.Create(308,"First","Last","");
        bool Resolve(byte[] b)=>CharacterCreation.TryParse(b,out var r)&&ResolveAssets(r,out _);
        Assert.True(Resolve(Valid()));
        foreach(uint invalid in new uint[]{737,6386,1026})
        {var b=Valid();BitConverter.GetBytes(invalid).CopyTo(b,33);Assert.False(Resolve(b));} // Wrong gender, wrong slot, known non-creation hat.
        var optional=Valid();foreach(int index in new[]{8,9,11,12})BitConverter.GetBytes(0u).CopyTo(optional,1+4*index);
        Assert.True(Resolve(optional));
        var required=Valid();BitConverter.GetBytes(0u).CopyTo(required,41);Assert.False(Resolve(required));
        var maleTights=Valid();BitConverter.GetBytes(743u).CopyTo(maleTights,57);Assert.False(Resolve(maleTights));
    }
    [Fact] public void FemaleStartingTightsSurviveRequestResolutionAndActualRsiEncoding()
    {
        byte[] b=CharacterCreationWire.Create(304,"First","Last","");
        uint[] values={0,102,100,0,0,0,0,0,737,671,842,726,5621,748,743,6734};
        for(int i=0;i<values.Length;i++)BitConverter.GetBytes(values[i]).CopyTo(b,1+i*4);
        Assert.True(CharacterCreation.TryParse(b,out var request));Assert.True(ResolveAssets(request,out var state));
        Assert.Equal(743u,state.Clothing[103]);Assert.Equal(2,state.Rsi["leggins"]);
        string[] keys={"sex","body","hat","face","shirt","coat","pants","shoes","gloves","glasses","hair","facialdetail","shirtcolor","pantscolor","coatcolor","shoecolor","glassescolor","haircolor","skintone","tattoo","facialdetailcolor","leggins"};
        byte[] encoded=PacketsUtils.getRSIBytes(keys.Select(k=>state.Rsi.TryGetValue(k,out int v)?v:0).ToArray());
        string bits=string.Concat(encoded.Select(v=>Convert.ToString(v,2).PadLeft(8,'0')));
        Assert.Equal(2,Convert.ToInt32(bits.Substring(51,4),2));
        BitConverter.GetBytes(0u).CopyTo(b,57);Assert.True(CharacterCreation.TryParse(b,out request));Assert.True(ResolveAssets(request,out state));
        Assert.False(state.Clothing.ContainsKey(103));Assert.Equal(0,state.Rsi["leggins"]);
        Assert.Equal(0,state.Rsi["face"]);Assert.Equal(0,state.Rsi["hair"]);
        BitConverter.GetBytes(112u).CopyTo(b,9);Assert.True(CharacterCreation.TryParse(b,out request));Assert.True(ResolveAssets(request,out state));Assert.Equal(5,state.Rsi["face"]); // Native allocation:100->0,112->5.
    }
    [Fact] public void NativeCloneNamesEveryLegacyColumnAndDeliberatelyLeavesNullableTraitUnassigned()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        string sql=File.ReadAllText(Path.Combine(root,"SQL","reality_hd.sql"));
        string fields=System.Text.RegularExpressions.Regex.Match(sql,@"INSERT INTO `characters` \(([^\r\n]+)\) VALUES").Groups[1].Value.Replace("`","");
        Assert.NotEmpty(fields);Assert.DoesNotContain("trait_id",fields);
        string program=File.ReadAllText(Path.Combine(root,"tools","NativeQa","Program.cs"));
        Assert.Contains("INSERT INTO characters ("+fields+") SELECT "+fields+" FROM reality_hd.characters WHERE charId=110;",program);
        Assert.DoesNotContain("INSERT INTO characters SELECT * FROM reality_hd.characters",program);
    }
    [Fact] public void ReservationSuppliesEveryRequiredColumnAndKeepsExistingEfAndClothingDefaults()
    {
        string schema=File.ReadAllText(Path.Combine(Root,"SQL","reality_hd.sql"));
        string table=System.Text.RegularExpressions.Regex.Match(schema,@"CREATE TABLE `characters` \(([\s\S]+?)\) ENGINE=").Groups[1].Value;
        string[] required=table.Split('\n').Where(l=>l.Contains("NOT NULL")&&!l.Contains("DEFAULT")).Select(l=>System.Text.RegularExpressions.Regex.Match(l,"`([^`]+)`").Groups[1].Value).ToArray();
        Assert.Equal(new[]{"charId","userId","worldId","status","handle","created"},required);
        string sql=hds.databases.MyMarginDBAccess.ReservationInsert;
        string[] inserted=System.Text.RegularExpressions.Regex.Match(sql,@"characters\(([^)]+)\)").Groups[1].Value.Split(',');
        Assert.Equal(required.Where(c=>c!="charId").Order(),inserted.Order()); // charId is the existing AUTO_INCREMENT key.
        Assert.Equal(0,new hds.databases.Entities.Character().Status);
        Assert.Contains("VALUES(@handle,@account,@world,0,NOW())",sql);
        Assert.Contains("`rotation` mediumint(11) NOT NULL DEFAULT '139'",table);
        Assert.DoesNotContain("rotation",sql);Assert.DoesNotContain("profession",sql);
        Assert.Equal("INSERT INTO inventory(charId,slot,goid,count,purity) VALUES(@character,@slot,@goid,0,0)",hds.databases.MyMarginDBAccess.InitialClothingInsert);
    }
    [Fact] public void NativeConsumerProfilesAreOnly110AndHaveCoherentBaseRows()
    {
        foreach(uint trait in new uint[]{308,304})
        {
            var p=NativeQa.NativeQaModes.Find("character-personality-"+trait).Plan;
            Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
            Assert.Contains(p.AfterHelpers,s=>s.Contains("trait_id="+trait)&&s.Contains("charId=110"));
            Assert.Contains(p.AfterHelpers,s=>s.Contains("DELETE FROM char_abilities WHERE char_id=110"));
        }
    }
}
