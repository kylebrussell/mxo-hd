using hds;

namespace HeadlessClient;

public static class AdvancedSimulacraNoticeFixture
{
    public const ushort Level = 38, Proxy = 594, Nuker = 616;
    public const int X = 17000, Y = 495, Z = 6400, ObserverX = 17400;
    // Each literal was retained independently from the stock client in PR312 or PR603.
    public const string CoderStyle = "80b9f0030000000000000000000000000000000000000000000000000090767241086b0000000000";
    public const string Proxy1 = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    public const string Nuker1 = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";
    public const string Healer1 = "80b95a02000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
    public const string Proxy5 = "80b95202000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    public const string Deactivate = "81 7a fe 01";
    public static readonly ushort[] Abilities = new ushort[] { 2, 17, 1008, 27, 22, 602, Proxy, Nuker }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public static bool IsNotice(RpcRecord rpc) => rpc.Bytes.Length > 2 &&
        rpc.Bytes[0] == 0x2e && rpc.Bytes[1] == 0xc7 &&
        System.Text.Encoding.ASCII.GetString(rpc.Bytes).Contains("You summoned a level ", StringComparison.Ordinal);

    public static async Task<ViewCreation> PetViewAsync(HeadlessPlayer observer, uint owner, byte id)
    {
        ViewCreation? pet = null;
        await observer.WaitUntilAsync(() => (pet = observer.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
            BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == owner)) != null,
            $"Owned pet {id} is visible to {observer.Handle}", HeadlessPlayer.DefaultTimeout);
        return pet!;
    }
}
