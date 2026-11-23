using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;

[Collection("DelayedActions")]
public class MultifightingInterlockTests : IDisposable
{
    private readonly Dictionary<string,WorldClient> previous=WorldServer.Clients;
    private readonly List<WorldClient> clients=new();
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    public MultifightingInterlockTests(){WorldServer.Clients=new();}
    private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Fields)!.SetValue(owner,value);
    private WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerData.setCharID((uint)(clients.Count+1));c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        c.Alive=true;Set(c,"lastUsedTime",TimeUtils.getUnixTimeUint32());Set(c,"lastFlushTick",long.MaxValue);
        Set(c,"<TransportFence>k__BackingField",new WorldSendFence());
        string key="multifighting"+clients.Count;c.playerData.setUniqueKey(key);WorldServer.Clients[key]=c;clients.Add(c);return c;
    }
    private (PvpFight Fight,PvpFight.Side[] Sides) Group()
    {
        var fight=(PvpFight)RuntimeHelpers.GetUninitializedObject(typeof(PvpFight));
        Set(fight,"sync",new object());Set(fight,"<IsActive>k__BackingField",true);
        var sides=Enumerable.Range(0,4).Select(_=>{var c=Client();return new PvpFight.Side{Client=c,MultifightingOwner=new MultifightingInterlock.Owner(c)};}).ToArray();
        // A<->B, C->A; D->B does not oppose A. Three links to B are irrelevant to A's count.
        sides[0].Other=sides[1];sides[1].Other=sides[0];sides[2].Other=sides[0];sides[3].Other=sides[1];
        Set(fight,"members",sides.ToList());foreach(var s in sides)CombatManager.GetHandler(s.Client).pvpFight=fight;
        return(fight,sides);
    }
    private static int Input(PvpFight f,PvpFight.Side s)=>f.MultifightingInput(s,100,"test",f.CaptureMultifighting());
    [Theory]
    [InlineData(0,0,0)] [InlineData(1,0,0)] [InlineData(2,-10,0)] [InlineData(3,-15,-5)] [InlineData(4,-20,-10)]
    public void LiteralRecoveredMathWithExplicitOursThreshold(int count,int off,int on)
    {Assert.Equal(off,MultifightingInterlock.Penalty(count,false));Assert.Equal(on,MultifightingInterlock.Penalty(count,true));}
    [Fact]
    public void CurrentDirectLinksAndActualLoadedMembershipApplyOnceToBothInputRoles()
    {
        var(f,s)=Group();Assert.Equal(90,Input(f,s[0]));
        s[0].Client.playerData.passives.Loaded[140]=0;Assert.Equal(100,Input(f,s[0]));
        s[3].Other=s[0];Assert.Equal(95,Input(f,s[0]));
        s[0].Client.playerData.passives.Loaded.Remove(140);Assert.Equal(85,Input(f,s[0]));
        var round=f.CaptureMultifighting();Assert.Equal(85,f.MultifightingInput(s[0],100,"defence",round));
        Assert.Equal(185,f.MultifightingInput(s[0],200,"queued-attack",round));
    }
    [Theory]
    [InlineData("dead")] [InlineData("offworld")] [InlineData("life")] [InlineData("world")] [InlineData("character")]
    [InlineData("district")] [InlineData("player")] [InlineData("data")] [InlineData("endpoint")] [InlineData("handler")] [InlineData("removed")]
    public void InvalidIncomingLinkDoesNotCount(string change)
    {
        var(f,s)=Group();var c=s[2].Client;
        switch(change){case "dead":c.playerData.isDead=true;break;case "offworld":c.playerData.setOnWorld(false);break;
        case "life":c.playerData.lifeGeneration++;break;case "world":c.playerData.worldGeneration++;break;
        case "character":c.playerData.setCharID(90);break;case "district":c.playerData.setDistrictId(2);break;
        case "player":c.playerInstance=new PlayerCharacter();break;case "data":c.playerData=new ClientData();break;
        case "endpoint":WorldServer.Clients.Remove(c.playerData.getUniqueKey(null));break;
        case "handler":CombatManager.GetHandler(c).pvpFight=null;break;case "removed":s[2].Present=false;break;}
        Assert.Equal(100,Input(f,s[0]));
    }
    [Fact]
    public void SnapshotCannotAdoptRetargetResetUnloadOrNewMembership()
    {
        var(f,s)=Group();var round=f.CaptureMultifighting();s[2].Other=s[1];
        Assert.Equal(100,f.MultifightingInput(s[0],100,"test",round));
        s[2].Other=s[0];round=f.CaptureMultifighting();s[2].Client.TransportFence.PublishIncoming(true,()=>{});
        Assert.Equal(100,f.MultifightingInput(s[0],100,"test",round));
        s[2].Client.TransportFence.Resume(s[2].Client.TransportFence.Capture());round=f.CaptureMultifighting();
        Set(f,"membershipVersion",1L);Assert.Equal(100,f.MultifightingInput(s[0],100,"test",round));
    }
    [Fact]
    public void LoadedChangeAfterSnapshotCannotGrantBenefitAndEndedFightCannotApply()
    {
        var(f,s)=Group();s[0].Client.playerData.passives.Loaded[140]=50;var round=f.CaptureMultifighting();
        s[0].Client.playerData.passives.Loaded.Clear();Assert.Equal(90,f.MultifightingInput(s[0],100,"test",round));
        s[2].Client.playerData.passives.Loaded[140]=50;Assert.Equal(90,f.MultifightingInput(s[0],100,"test",round));
        s[0].Client.playerData.passives.Loaded[140]=0;Assert.Equal(100,f.MultifightingInput(s[0],100,"test",round));
        Set(f,"<IsActive>k__BackingField",false);Assert.Equal(100,Input(f,s[0]));
    }
    [Fact]
    public void DuplicateOpponentClientCountsOnceAndOtherDistrictCannotJoinTheCount()
    {
        var(f,s)=Group();s[3].Client=s[2].Client;s[3].MultifightingOwner=s[2].MultifightingOwner;s[3].Other=s[0];
        Assert.Equal(90,Input(f,s[0]));
        s[2].Client.playerData.setDistrictId(2);Assert.Equal(100,Input(f,s[0]));
    }
    [Theory]
    [InlineData(249, "Sliding Volley")]
    [InlineData(242, "Pistol Evasive")]
    public void SelectedSpecialCannotTransferToANewOpponentOrRespawn(ushort id, string name)
    {
        var (fight, sides) = Group();
        Set(fight, "rng", new Random(1));
        var (shooter, opponent, other) = (sides[0], sides[1], sides[2]);
        shooter.Client.viewMan = new ViewManager();
        shooter.Move = new CombatInterlock.Move();
        var original = AddView(opponent, 4001, 9);
        var replacement = AddView(other, 4002, 10);
        uint handle = DuelPackets.Handle(original.ViewID, original.spawnId);
        shooter.OpponentHandle = handle;
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName(name);
        Assert.False(fight.QueueAbility(shooter.Client, ability, original.ViewID));

        Assert.True(fight.QueueAbility(shooter.Client, ability, original.ViewID, requiredOpponent: handle));
        Assert.Same(ability, PvpFight.QueuedAbilityForRound(shooter, opponent));
        Assert.Null(PvpFight.QueuedAbilityForRound(shooter, other));
        original.spawnId++;
        Assert.Null(PvpFight.QueuedAbilityForRound(shooter, opponent));
        original.spawnId--;
        opponent.Present = false;
        Assert.Null(PvpFight.QueuedAbilityForRound(shooter, opponent));
        opponent.Present = true;

        Assert.True(fight.ChangeTarget(shooter.Client, other.Client,
            Retarget(DuelPackets.Handle(replacement.ViewID, replacement.spawnId))));
        Assert.Null(shooter.QueuedAbility);
        Assert.True(fight.ChangeTarget(shooter.Client, opponent.Client, Retarget(handle)));
        Assert.Null(PvpFight.QueuedAbilityForRound(shooter, opponent));
        Assert.True(fight.QueueAbility(shooter.Client, ability, original.ViewID, requiredOpponent: handle));
        Assert.Same(ability, PvpFight.QueuedAbilityForRound(shooter, opponent));

        ClientView AddView(PvpFight.Side side, uint entity, ushort spawn)
        {
            side.Client.playerData.setEntityId(entity);
            var view = shooter.Client.viewMan.addView(entity, 1);
            view.viewCreated = true;
            view.spawnId = spawn;
            return view;
        }

        static byte[] Retarget(uint target) => BitConverter.GetBytes(target).Concat(new byte[] { 1, 0x6b }).ToArray();
    }

    public void Dispose()
    {
        foreach(var c in clients){CombatManager.GetHandler(c).pvpFight=null;CombatManager.RemoveClient(c);c.Alive=false;}
        WorldServer.Clients=previous;
    }
}
