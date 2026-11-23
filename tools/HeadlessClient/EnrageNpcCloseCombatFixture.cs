using hds;

namespace HeadlessClient;

public static class EnrageNpcCloseCombatFixture
{
    public const ushort Ability = 588, Level = 25, HelperLevel = 5;
    public const string NpcName = "Blackwood Mushfake", Release = "44 00";
    public const uint Rifle = 2568;
    public const ushort FullHealth = 262, NativeStartingHealth = 242;
    public const int NpcX = 16304, Y = 95, NpcZ = -4284;
    public const int CasterX = 15960, CasterZ = -5115;
    // Offset the helper from the caster's sightline while preserving the seven-metre pursuit.
    public const int HelperX = 16167, HelperZ = -4971;

    public static readonly ushort[] Abilities = new ushort[] { 2, 17, 1008, Ability }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly byte[] Rsi = Convert.FromHexString("b6010058");

    public static void CheckGeometry(string root)
    {
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (NpcX, NpcZ), (CasterX, CasterZ), (HelperX, HelperZ) })
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Enrage stand ({x},{Y},{z}) is grounded on outdoor NAV");
            Check.That(NavMesh.HasSightPath(1, NpcX, Y, NpcZ, x, Y, z) &&
                NavMesh.HasSightPath(1, x, Y, z, NpcX, Y, NpcZ), $"Enrage stand ({x},{Y},{z}) has clear two-way sight");
        }
        double near = Distance(HelperX, HelperZ), far = Distance(CasterX, CasterZ);
        Check.That(near is > 699 and < 701 && far is > 899 and < 901 &&
            near > MobAggro.MeleeDistanceUnits && MobAggro.OpensFire(Rifle, near),
            "The helper is the closer candidate, outside melee reach and inside rifle range");
    }

    public static async Task<ViewCreation> FindNpcAsync(HeadlessPlayer player, ushort expectedHealth = FullHealth)
    {
        ViewCreation? found = null;
        await player.WaitUntilAsync(() => (found = player.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == 599 && view.Name == NpcName && view.Attributes["Level"][0] == HelperLevel &&
            view.Attributes["MoreInfoID"].SequenceEqual(Rsi) && At(view, NpcX, NpcZ))) != null,
            $"Birth position of the armed {NpcName}", HeadlessPlayer.DefaultTimeout);
        var npc = found!;
        Check.That(BitConverter.ToUInt32(npc.Attributes["EquippedItemID"]) == Rifle &&
            npc.Attributes["IsEnemy"][0] == 1 && BitConverter.ToUInt16(npc.Attributes["Health"]) == expectedHealth &&
            BitConverter.ToUInt16(npc.Attributes["MaxHealth"]) == FullHealth,
            $"The NPC is the live, attackable rifle carrier at {expectedHealth}/{FullHealth} HP");
        return npc;
    }

    public static bool At(ViewCreation view, int x, int z) =>
        EngageFoesFixture.Position(view).SequenceEqual(new double[] { x, Y, z });

    public static bool CombatFlag(RpcRecord record, ViewCreation view, byte flag) =>
        record.Bytes.Length == 6 && record.Bytes[0] == 0x46 && record.Bytes[5] == flag &&
        BitConverter.ToUInt32(record.Bytes, 1) == DuelPackets.Handle(view.ViewId, view.SpawnId);

    private static double Distance(int x, int z) =>
        Math.Sqrt(Math.Pow(x - NpcX, 2) + Math.Pow(z - NpcZ, 2));
}
