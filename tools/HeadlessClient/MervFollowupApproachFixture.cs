using hds;
using System.Globalization;
namespace HeadlessClient;
public static class MervFollowupApproachFixture
{
    public static byte[] ClientPosition(IReadOnlyList<double> root)
    {
        if(root.Count!=3||root.Any(v=>!double.IsFinite(v)||!float.IsFinite((float)v)))throw new ArgumentException("Invalid published root");
        return new byte[]{2,0,1,8}.Concat(root.SelectMany(v=>BitConverter.GetBytes((float)v))).Concat(new byte[]{0,0}).ToArray();
    }
    public static bool OwnsWaitingPhase(MissionProgress.SavedState? current,int mission,int phase)=>current?.Mission==mission&&current.Phase==phase;
    public static bool CompletionReady(MissionProgress.SavedState? current,long receipts,bool currentCompletionWire)=>current==null&&receipts==1&&currentCompletionWire;
    public static bool Transitioned(MissionProgress.SavedState? current,int mission,int phase)=>!OwnsWaitingPhase(current,mission,phase);
    public static bool CanDepart(MissionProgress.SavedState? current,int mission,int phase,Mission? authored=null,int? exitEscortGoal=null)
    {
        if(!OwnsWaitingPhase(current,mission,phase))return false;
        if(!exitEscortGoal.HasValue)return current!.Flags.TryGetValue("waitingForExit",out bool waiting)&&waiting;
        if(authored?.id!=mission||phase<0||phase>=authored.phases.Count)return false;
        var goals=authored.phases[phase].missionObjectives.Where(o=>o.goal==exitEscortGoal.Value).ToArray();
        if(goals.Length!=1)return false;var goal=goals[0];
        if(goal.type!=MissionGoalType.Escort||goal.escortTo!="exit"||current!.Done.Contains(goal.goal)||
            (goal.afterGoal>=0&&!current.Done.Contains(goal.afterGoal))||
            !current.Flags.TryGetValue("entered",out bool entered)||!entered||current.Flags.TryGetValue("failed",out bool failed)&&failed)return false;
        var npcs=goal.Npcs;
        return npcs.Count>0&&npcs.All(id=>id!=0)&&npcs.Distinct().Count()==npcs.Count&&
            npcs.All(id=>current.Following.Contains(id)&&current.Mobs.Count(m=>m.Npc==id&&!m.Dead)==1);
    }
    public static bool FreshGoal(IReadOnlyList<byte[]> rows,int cursor,int goal)=>cursor>=0&&cursor<=rows.Count&&goal>0&&goal<=255&&rows.Skip(cursor).Any(r=>BytePattern.Parse($"80 a0 {goal:x2} 06 00 01").IsMatch(r));
    public static bool EscortExitAdvanced(MissionProgress.SavedState before,MissionProgress.SavedState? current,Mission authored,int goal,IReadOnlyList<byte[]> rows,int cursor,long receipts)
    {
        if(before==null||authored?.id!=before.Mission||string.IsNullOrWhiteSpace(before.Identity)||before.Phase<0||before.Phase>=authored.phases.Count||before.Done.Contains(goal)||!FreshGoal(rows,cursor,goal))return false;
        var objective=authored.phases[before.Phase].missionObjectives.SingleOrDefault(o=>o.goal==goal);
        if(objective?.type!=MissionGoalType.Escort||objective.escortTo!="exit"||objective.afterGoal>=0&&!before.Done.Contains(objective.afterGoal))return false;
        if(current!=null)return current.Mission==before.Mission&&current.Identity==before.Identity&&current.Started==before.Started&&current.Phase==before.Phase+1&&current.Phase<authored.phases.Count&&current.EverDone.Contains(goal);
        return before.Phase==authored.phases.Count-1&&receipts==1&&rows.Skip(cursor).Any(r=>BytePattern.Parse("80 a2 00").IsMatch(r));
    }
    public static IEnumerable<ViewCreation> CurrentGenerations(IReadOnlyList<ViewCreation> rows,int cursor)=>
        rows.Skip(cursor).GroupBy(v=>v.ViewId).Select(g=>g.Last());
    public static ViewCreation? CurrentBody(IReadOnlyList<ViewCreation> rows,ushort goid,IReadOnlyList<double> centre,string quaternion,ushort instance)
    {
        int lifeStart=0;for(int i=0;i<rows.Count;i++)if(rows[i].ViewId==2)lifeStart=i+1;
        return CurrentGenerations(rows,lifeStart).LastOrDefault(v=>Matches(v,goid,centre,quaternion)&&
            v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&BitConverter.ToUInt16(key,1)==instance&&key[3]==2);
    }
    public static uint InitialState(ViewCreation view)=>view.Attributes.TryGetValue("CurrentState",out var state)?BitConverter.ToUInt32(state):ObjectStates.DefaultState(view.Goid);
    public static bool NpcMatches(ViewCreation v,MissionProgress.SavedMob mob,ushort instance)
    {
        bool hasEnemy=v.Attributes.TryGetValue("IsEnemy",out var side);
        bool friendly=(!hasEnemy||(side!.Length==1&&side[0]==0))&&v.Attributes.TryGetValue("DontAttack",out var safe)&&safe.Length==1&&safe[0]==1&&
            v.Attributes.TryGetValue("MissionKey",out var key)&&key.Length==4&&BitConverter.ToUInt16(key,1)==instance&&key[3]==2;
        return v.Goid==599&&v.Name==mob.Name&&(mob.Friendly?friendly:hasEnemy&&side!.Length==1&&side[0]==1)&&
            v.Attributes.TryGetValue("Position",out var at)&&at.Length==24&&
            Math.Abs(BitConverter.ToDouble(at)-mob.X)<1&&Math.Abs(BitConverter.ToDouble(at,8)-mob.Y)<1&&Math.Abs(BitConverter.ToDouble(at,16)-mob.Z)<1;
    }
    public static bool RequiresWitness(MissionProgress.SavedObject item)=>
        item.Goid is 3 or 548 or 552 && MissionSites.Goid(item.Spot)!=item.Goid;
    public static uint DoorAt(IEnumerable<string> rows,double[] pose)
    {
        if(pose.Length!=3||pose.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("Invalid saved lobby pose");
        var keys=rows.Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])).Select(l=>l.Split(','))
            .Where(f=>f.Length>=11&&f[0]=="1"&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(Convert.FromHexString(f[4]),0))&&
                Enumerable.Range(0,3).All(i=>double.Parse(f[6+i],CultureInfo.InvariantCulture)==pose[i]))
            .Select(f=>BitConverter.ToUInt32(Convert.FromHexString(f[2]),0)).Distinct().Take(2).ToArray();
        if(keys.Length!=1)throw new InvalidDataException("Current saved lobby door is missing or ambiguous");return keys[0];
    }
    public static bool Matches(ViewCreation view,ushort goid,IReadOnlyList<double> centre,string quaternion)=>
        view.Goid==goid&&view.Attributes.TryGetValue("Position",out var at)&&at.Length==24&&
        Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(at,i*8)==centre[i])&&
        view.Attributes.TryGetValue("Orientation",out var q)&&q.SequenceEqual(Convert.FromHexString(quaternion));
}
