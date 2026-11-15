using hds;
namespace HeadlessClient;
public static class MervNpcViewFixture
{
    public static bool AliveMatches(MissionProgress.SavedMob actor,ViewCreation view)=>!actor.Dead&&view.Goid==599&&view.Name==actor.Name&&
        view.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&Enumerable.Range(0,3).All(i=>Math.Abs(BitConverter.ToDouble(p,i*8)-new[]{actor.X,actor.Y,actor.Z}[i])<0.01)&&
        (!actor.Friendly||view.Attributes.TryGetValue("DontAttack",out var safe)&&safe.Length==1&&safe[0]==1)&&
        ((view.Attributes.TryGetValue("IsEnemy",out var enemy)&&enemy[0]!=0)==!actor.Friendly)&&
        (!view.Attributes.TryGetValue("IsDead",out var dead)||dead[0]==0);
    // Captured NPC death movement shape (docs/DEATH-PRESENTATION.md), independently decoded.
    public static bool CorpseMatches(MissionProgress.SavedMob actor,ViewCreation bound,IEnumerable<byte[]> sections)
    {
        if(!actor.Dead||bound.Name!=actor.Name)return false;
        foreach(var b in sections)for(int at=0;at+27<=b.Length;at++)
            if(BitConverter.ToUInt16(b,at)==bound.ViewId&&b[at+2]==7&&b[at+3]==1&&b[at+4]==0x1d&&
                (BitConverter.ToUInt16(b,at+5)==13||BitConverter.ToUInt16(b,at+5)==5)&&
                Enumerable.Range(0,3).All(i=>BitConverter.ToSingle(b,at+15+i*4)==(float)new[]{actor.X,actor.Y,actor.Z}[i]))return true;
        return false;
    }
    public static bool RequiresWitness(MissionProgress.SavedObject o)=>o.Goid is 3 or 548 or 552&&MissionSites.Goid(o.Spot)!=o.Goid;
}
