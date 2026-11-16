using hds;
namespace HeadlessClient;
// QA-only durable acceptance continuity across ordinary Quit/new own-view life.
public static class ZionCarryRelogFixture
{
    public static void Require(MissionProgress.SavedState before,MissionProgress.SavedState after,OwnViewLifeSnapshot currentLife)
    {
        if(!currentLife.Current||!currentLife.OwnViewReady)throw new InvalidDataException("Restored carry requires the actual fresh current own life");
        if(before.Version is not (1 or 2)||after.Version!=before.Version||before.Mission!=after.Mission||before.Identity!=after.Identity||before.Started!=after.Started||before.Instance!=after.Instance||before.Phase!=after.Phase)
            throw new InvalidDataException("Restored carry replaced its accepted mission/version/instance");
        if(!before.Following.Order().SequenceEqual(after.Following.Order()))throw new InvalidDataException("Restored carry changed following membership");
        foreach(ushort id in before.Following)
        {
            var old=before.Mobs.Where(m=>m.Npc==id).ToArray();var now=after.Mobs.Where(m=>m.Npc==id).ToArray();
            if(old.Length!=1||now.Length!=1||!Guid.TryParseExact(old[0].Identity,"N",out _)||old[0].Identity!=now[0].Identity||old[0].Name!=now[0].Name||old[0].Friendly!=now[0].Friendly||old[0].Dead||now[0].Dead||old[0].Health==0||now[0].Health!=old[0].Health)
                throw new InvalidDataException("Restored carry replaced or lost the living owned follower UUID");
        }
    }
}
