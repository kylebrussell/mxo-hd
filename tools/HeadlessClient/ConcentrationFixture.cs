namespace HeadlessClient;
public static class ConcentrationFixture
{
    public static ushort[] Abilities(bool loaded)=>loaded?new ushort[]{2,17,360,133,261,628}:new ushort[]{2,17,360,133,261};
    public static async Task PrepareEnemyAsync(HeadlessPlayer enemy)
    {
        enemy.SendChat("?org 2");enemy.SendChat("?pvpflag off");enemy.SendChat("?pvpflag on");
        await enemy.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 01 00 00 00 01");
    }
}
