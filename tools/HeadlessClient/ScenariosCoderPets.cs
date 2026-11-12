namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// Coder simulacra (docs/CODER-PETS.md): Logic Daemon 4.0 summons an owned NPC_BASE_SIMULACRA that a second player
    /// sees too; the client's 81 7c / 81 7a orders change its mode and dismiss it; Drain Code destroys a resummoned one.
    /// </summary>
    private static async Task CoderPetLifecycle(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(NearNpc("PetOwner") with { Level = 50 }, NearNpc("PetWatcher", 100));
        var owner = players[0]; var watcher = players[1];

        Cast(owner, 616); // Logic Daemon 4.0: 8 s cast, then the summon
        var pet = await owner.WaitForViewNamedAsync("Nuker Simulacrum", TimeSpan.FromSeconds(20));
        Check.Equal((ushort)40376, pet.Goid, "The simulacrum is an NPC_BASE_SIMULACRA view");
        Check.Equal(owner.CharId, BitConverter.ToUInt32(pet.Attributes["OwnerCharacterID"]), "Owned by the caster");
        Check.Equal(owner.CharId, BitConverter.ToUInt32(pet.Attributes["MasterCharacterID"]), "Master is the caster");
        Check.Equal((byte)50, pet.Attributes["Level"][0], "Logic Daemon 4.0 clamps level 50 to 35-50");
        Check.Equal((ushort)1950, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Retail level 50 simulacrum health");
        Check.Equal("13-05-00-58", BitConverter.ToString(pet.Attributes["Description"]), "Retail L50 Nuker Description, in wire byte order");
        var registration = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(10));
        Check.Equal(46, registration.Bytes.Length, "Native registration size");
        Check.Equal(pet.Attributes["SimulacraID"][0], registration.Bytes[3], "Window tracks spawned ID");
        var seen = await watcher.WaitForViewNamedAsync("Nuker Simulacrum", TimeSpan.FromSeconds(10));
        Check.Equal(owner.CharId, BitConverter.ToUInt32(seen.Attributes["OwnerCharacterID"]), "Others see whose simulacrum it is");

        owner.SendChat("?pet follow");
        await owner.WaitForTextAsync("Following");
        owner.SendRpc("81 7c fe 05 02"); // Simulacra window: Passive
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("Passive");
        owner.SendRpc("81 7a fe 00"); // Stay
        await owner.WaitForRpcAsync("81 78");
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("Stay");
        owner.SendRpc("81 7a fe 01");
        await owner.WaitForRpcAsync("81 77"); // Deactivate
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("No simulacrum");

        owner.SendChat("?pet summon 27");
        var second = await owner.WaitForRpcAsync("81 76");
        await owner.WaitForTextAsync("is next to you");
        Check.Equal((byte)(registration.Bytes[3]+1), second.Bytes[3], "Per-owner IDs advance");
        Cast(owner, 19); // Drain Code: the simulacrum is consumed
        await owner.WaitForTextAsync("Your simulacrum has been destroyed", TimeSpan.FromSeconds(15));
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("No simulacrum");
        Cast(owner, 34); // Detonate Proxy without a simulacrum is refused
        await owner.WaitForTextAsync("You need an active simulacrum");
        Check.Equal(0, owner.World.Problems.Count, "Owner protocol problems");
        Check.Equal(0, watcher.World.Problems.Count, "Watcher protocol problems");
    }
    private static async Task CoderPetResources(ScenarioContext context)
    {
        var owner = (await context.AddPlayersInWorldAsync(NearNpc("PetResource") with { Level = 50 }))[0];
        owner.SendChat("?pet summon 616");
        await owner.WaitForRpcAsync("81 76");
        await owner.WaitForTextAsync("is next to you");
        var pet = await owner.WaitForViewNamedAsync("Nuker Simulacrum");
        Cast(owner,20);
        await owner.WaitForViewUpdateAsync(pet.ViewId,"04 80 80 84 8c 0a 40 9e 07",TimeSpan.FromSeconds(10));
        owner.SendChat("?pet"); await owner.WaitForTextAsync("resistance 100");
        Cast(owner,603);
        await Task.Delay(4500);
        owner.SendChat("?pet"); await owner.WaitForTextAsync("cost 8");
        Cast(owner,619);
        await Task.Delay(3000);
        owner.SendChat("?pet"); await owner.WaitForTextAsync("accuracy 64");
        owner.SendChat("?pet exhaust"); await owner.WaitForTextAsync("IS exhausted");
        owner.SendChat("?pet dummy"); await owner.WaitForTextAsync("stands 8 m away");
        var dummy = await owner.WaitForViewNamedAsync("Simulacra Dummy");
        owner.SendRpc($"81 7b fe 02 {dummy.ViewId & 255:x2} {dummy.ViewId >> 8:x2} 00 00");
        await owner.WaitForViewUpdateAsync(dummy.ViewId,"04 80 80 80",TimeSpan.FromSeconds(18));
        owner.SendChat("?pet cast 619"); await Task.Delay(1000);
        owner.SendChat("?pet"); await owner.WaitForTextAsync("accuracy 0");
        Cast(owner,19);
        await owner.WaitForRpcAsync("81 77");
        await owner.WaitForViewUpdateAsync(pet.ViewId,"07 01 1d 0d 00",TimeSpan.FromSeconds(12));
        owner.SendChat("?pet summon 27");
        var replacement = await owner.WaitForRpcAsync("81 76");
        await Task.Delay(5500); // old corpse teardown must not remove the new window/owned pet
        owner.SendChat("?pet"); await owner.WaitForTextAsync("cost 10");
        Check.That(!owner.Received("81 77").Any(r => r.Bytes[2] == replacement.Bytes[3]),"Old corpse never removes replacement ID");
        Check.Equal(0,owner.World.Problems.Count,"Pet resource gameplay protocol");
    }
    private static async Task CoderPetPowerBoost(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(NearNpc("PowerOwner") with { InnerStrength=1000,MaxInnerStrength=1000 }, NearNpc("PowerWatcher",100));
        var owner=players[0]; var watcher=players[1];
        owner.SendChat("?pet summon 594"); await owner.WaitForRpcAsync("81 76"); await owner.WaitForTextAsync("is next to you");
        var friend=await owner.WaitForViewNamedAsync("PowerWatcher");
        Cast(owner,25,friend.ViewId); await owner.WaitForTextAsync("That is not your simulacrum");
        Cast(watcher,25); await watcher.WaitForTextAsync("You need an active simulacrum");
        watcher.SendChat("?pet summon 594"); await watcher.WaitForRpcAsync("81 76");
        ViewCreation? foreign=null;
        await owner.WaitUntilAsync(()=> (foreign=owner.World.CreationSnapshot().LastOrDefault(v=>v.Goid==40376 && BitConverter.ToUInt32(v.Attributes["OwnerCharacterID"])==watcher.CharId))!=null,"foreign owned simulacrum view",TimeSpan.FromSeconds(10));
        Cast(owner,25,foreign!.ViewId); await owner.WaitForTextAsync("That is not your simulacrum");
        Cast(owner,25);
        owner.SendChat("?pet summon 594"); await owner.WaitForRpcAsync("81 76"); await owner.WaitForTextAsync("is next to you");
        await Task.Delay(4500);
        owner.SendChat("?pet"); await owner.WaitForTextAsync("power melee/ballistic 0/0");
        owner.SendRpc("81 7a fe 00"); // stay, then test the sourced10m range before cost/cooldown
        owner.SendChat($"?tp {NpcX+2200} {NpcY} {NpcZ} 0 0 0"); await owner.WaitForTextAsync("Teleported to 0/0");
        Cast(owner,25); await owner.WaitForTextAsync("Your simulacrum is out of range");
        owner.SendChat($"?tp {NpcX+200} {NpcY} {NpcZ} 0 0 0"); await owner.WaitForTextAsync("Teleported to 0/0");
        await Task.Delay(8000);
        Cast(owner,25); await Task.Delay(4500);
        owner.SendChat("?pet"); await owner.WaitForTextAsync("power melee/ballistic 25/25");
        Cast(owner,609); await Task.Delay(2500);
        owner.SendChat("?pet"); await owner.WaitForTextAsync("aura viral/heal 25/25");
        owner.SendChat("?pet dummy"); await owner.WaitForTextAsync("stands 8 m away");
        var dummy=await owner.WaitForViewNamedAsync("Simulacra Dummy");
        owner.SendRpc($"81 7b fe 02 {dummy.ViewId & 255:x2} {dummy.ViewId >> 8:x2} 00 00");
        await owner.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[PET] typed influence Proxy"),"actual typed PowerBoost proxy hit",TimeSpan.FromSeconds(18));
        var hit=System.Text.RegularExpressions.Regex.Match(context.Server.DebugLog(),@"typed influence Proxy base (\d+) final (\d+)");
        Check.Equal((int)(int.Parse(hit.Groups[1].Value)*1.25),int.Parse(hit.Groups[2].Value),"25% proxy influence, no699viral bonus on proxy");
        owner.SendRpc("3d 01");
        owner.SendRpc($"41 {Le(dummy.ViewId)} {Le((ushort)dummy.SpawnId)}");
        await Task.Delay(500);
        Cast(owner,25); await owner.WaitForTextAsync("You cannot use Power Boost in combat");
        Cast(owner,19); await owner.WaitForTextAsync("Your simulacrum has been destroyed");
        owner.SendChat("?pet summon 594"); await owner.WaitForRpcAsync("81 76"); await owner.WaitForTextAsync("is next to you");
        owner.SendChat("?pet"); await owner.WaitForTextAsync("power melee/ballistic 0/0");
        Check.Equal(0,owner.World.Problems.Count,"PowerBoost protocol");
        Check.Equal(0,watcher.World.Problems.Count,"PowerBoost observer protocol");
    }

    private static async Task CoderPetDaemonTyped(ScenarioContext context)
    {
        var owner=(await context.AddPlayersInWorldAsync(NearNpc("TypedDaemon") with { InnerStrength=1000,MaxInnerStrength=1000,Health=1,MaxHealth=5000 }))[0];
        owner.SendChat("?pet summon 616"); await owner.WaitForRpcAsync("81 76"); await owner.WaitForTextAsync("is next to you");
        Cast(owner,609); await Task.Delay(2500);
        owner.SendChat("?pet dummy"); await owner.WaitForTextAsync("stands 8 m away");
        var dummy=await owner.WaitForViewNamedAsync("Simulacra Dummy");
        owner.SendRpc($"81 7b fe 02 {dummy.ViewId & 255:x2} {dummy.ViewId >> 8:x2} 00 00");
        await owner.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[PET] typed influence Nuker"),"actual699Nuker viral hit",TimeSpan.FromSeconds(18));
        var hit=System.Text.RegularExpressions.Regex.Match(context.Server.DebugLog(),@"typed influence Nuker base (\d+) final (\d+)");
        Check.Equal((int)(int.Parse(hit.Groups[1].Value)*1.25),int.Parse(hit.Groups[2].Value),"699viral percentage");
        owner.SendChat("?pet summon 617"); await owner.WaitForRpcAsync("81 76"); await owner.WaitForTextAsync("is next to you");
        await owner.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[PET] typed healing base"),"actual699healer healing",TimeSpan.FromSeconds(14));
        var heal=System.Text.RegularExpressions.Regex.Match(context.Server.DebugLog(),@"typed healing base (\d+) final (\d+)");
        Check.Equal((int)(int.Parse(heal.Groups[1].Value)*1.25),int.Parse(heal.Groups[2].Value),"699healing percentage");
        Check.Equal(0,owner.World.Problems.Count,"Typed daemon protocol");
    }
}
