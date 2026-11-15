using HeadlessClient;
using hds;
using System.Text.Json;

namespace NativeQa;

public sealed record NativeQaSeededLoadout(ushort[] Abilities, ushort[] DisciplineAncestors);

/// <summary>Shared pre-client seed sequence. Writes only the harness-owned disposable schema.</summary>
public static class NativeQaFixtureSeeder
{
    public static string[] HelperNames => new[] { "QAFriend", "QAFar", "QAOther", "QAEnemy" };
    public static ushort[] DisciplineAncestors(JsonElement catalog, IEnumerable<ushort> requested)
    {
        var rows = catalog.GetProperty("abilities").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetUInt16());
        var selected = requested.ToHashSet(); var known = new HashSet<ushort>(selected); var pending = new Queue<ushort>(selected);
        while (pending.Count > 0)
        {
            if (!rows.TryGetValue(pending.Dequeue(), out var row) ||
                !row.GetProperty("fields").TryGetProperty("Ability/DisciplineBelongsTo", out var belongs)) continue;
            ushort parent = AbilityCatalog.IdOf(belongs.GetUInt32());
            if (parent == 0 || !rows.TryGetValue(parent, out var p) ||
                !p.GetProperty("fields").TryGetProperty("Ability/IsDiscipline", out var discipline) || discipline.GetInt32() == 0) continue;
            if (known.Add(parent)) pending.Enqueue(parent);
        }
        return known.Except(selected).Order().ToArray();
    }

    public static async Task<NativeQaSeededLoadout> PrepareAsync(string root, ThrowawayDatabase database,
        NativeQaModePlan fixturePlan, ushort[]? requested = null)
    {
        if (!database.Name.StartsWith(ThrowawayDatabase.Prefix, StringComparison.Ordinal))
            throw new ArgumentException("Native QA seed requires an owned disposable database.", nameof(database));
        ushort[] requestedAbilities = requested ?? Array.Empty<ushort>();
        string[] helperNames = HelperNames;
        int nativeX = fixturePlan.X, nativeZ = fixturePlan.Z;
    await database.ExecuteAsync(@"
INSERT INTO users (userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
SELECT 990110,'CSR_AbilityQA',publicExponent,publicModulus,privateExponent,0,passwordmd5
FROM reality_hd.users WHERE userId=(SELECT userId FROM reality_hd.characters WHERE charId=110);
DELETE FROM characters WHERE charId=110;
INSERT INTO characters (charId, userId, worldId, status, handle, firstName, lastName, background, x, y, z, rotation, healthC, healthM, innerStrC, innerStrM, level, profession, alignment, pvpflag, conquest_points, exp, cash, repMero, repMachine, repNiobe, repEPN, repCYPH, repGM, repZion, district, districtId, factionId, crewId, is_deleted, is_online, created) SELECT charId, userId, worldId, status, handle, firstName, lastName, background, x, y, z, rotation, healthC, healthM, innerStrC, innerStrM, level, profession, alignment, pvpflag, conquest_points, exp, cash, repMero, repMachine, repNiobe, repEPN, repCYPH, repGM, repZion, district, districtId, factionId, crewId, is_deleted, is_online, created FROM reality_hd.characters WHERE charId=110;
UPDATE characters SET userId=990110,level=50,healthC=100,healthM=500,innerStrC=200,innerStrM=200,
 x=23828,y=495,z=13984,rotation=126,district='slums',districtId=1,alignment=1,repMero=100,
 crewId=0,factionId=0,is_online=0,pvpflag=0 WHERE charId=110;
DELETE FROM rsivalues WHERE charid=110;
INSERT INTO rsivalues SELECT * FROM reality_hd.rsivalues WHERE charid=110;
DELETE FROM inventory WHERE charId=110;
INSERT INTO inventory (charId,goid,slot,count,purity)
 SELECT 110,goid,slot,count,purity FROM reality_hd.inventory WHERE charId=110;
CREATE TABLE IF NOT EXISTS char_missions
 (charId INT UNSIGNED NOT NULL,missionId INT NOT NULL,completions INT NOT NULL DEFAULT 1,PRIMARY KEY(charId,missionId));
DELETE FROM char_missions WHERE charId=110;
INSERT INTO char_missions (charId,missionId) VALUES (110,986);
DELETE FROM char_abilities WHERE char_id=110;
");
    ushort[] abilities=fixturePlan.Abilities.ToArray();
    foreach(string sql in fixturePlan.BeforeLoadout) await database.ExecuteAsync(sql);
    using var catalog = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "data", "ability_catalog.json")));
    var names = catalog.RootElement.GetProperty("abilities").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetUInt16(), e => e.GetProperty("name").GetString()!);
    var levels = catalog.RootElement.GetProperty("abilities").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetUInt16(),
        e => e.GetProperty("fields").TryGetProperty("Ability/AbilityLevelCap", out var cap) ? Math.Min(50, cap.GetInt32()) : 50);
    if (requestedAbilities.Length > 0)
    {
        var playerAbilities = catalog.RootElement.GetProperty("abilities").EnumerateArray().Where(e =>
            !e.GetProperty("fields").TryGetProperty("Ability/AbilityClass", out var kind) || kind.GetInt32() == 0)
            .Select(e => e.GetProperty("id").GetUInt16()).ToHashSet();
        if (requestedAbilities.Any(id => !playerAbilities.Contains(id) || names[id].StartsWith("NPC", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Custom native loadouts require known player-ability templates.");
        abilities = requestedAbilities;
    }
    var disciplineAncestors = DisciplineAncestors(catalog.RootElement,abilities);
    var loadedIds = abilities.ToHashSet();
    string loadout = string.Join("\n", abilities.Distinct().Concat(disciplineAncestors).Select((id, slot) =>
        $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(110,{slot},{0x80000000L | ((long)id << 10)},{levels.GetValueOrDefault(id,50)},{(loadedIds.Contains(id)?1:0)},'{names.GetValueOrDefault(id, "QA").Replace("'", "''")}');"));
    await database.ExecuteAsync(loadout);
    foreach(string sql in fixturePlan.AfterLoadout) await database.ExecuteAsync(sql);
    // The native client's login level is derived from XP, independently of the server's stored Level.
    await database.ExecuteAsync($"UPDATE characters SET exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId=110");
    await database.ExecuteAsync($"UPDATE characters SET x={nativeX},z={nativeZ} WHERE charId=110");
    // Helpers are cloned solely from the disposable CSR record, never from another main-DB character.
    for (int i = 0; i < helperNames.Length; i++)
    {
        int id = 6111 + i; // rsivalues.charid is narrower than characters.charId in the legacy schema
        int x = nativeX + (i == 1 ? -1200 : i == 2 ? 0 : i == 3 ? -226 : -278);
        int z = nativeZ + (i == 2 ? 400 : i == 3 ? -107 : 0);
        await database.ExecuteAsync($@"
INSERT INTO users(userId,username,publicExponent,publicModulus,privateExponent,account_status,passwordmd5)
 SELECT {id},'{helperNames[i]}',publicExponent,publicModulus,privateExponent,0,passwordmd5 FROM users WHERE userId=990110;
CREATE TEMPORARY TABLE qa_char AS SELECT * FROM characters WHERE charId=110;
UPDATE qa_char SET charId={id},userId={id},handle='{helperNames[i]}',x={x},z={z},healthC={(i==3?5000:100)},healthM={(i==3?5000:500)},alignment={(i==3?2:1)},pvpflag={(i==3?1:0)};
INSERT INTO characters SELECT * FROM qa_char;
DROP TEMPORARY TABLE qa_char;
CREATE TEMPORARY TABLE qa_rsi AS SELECT * FROM rsivalues WHERE charid=110;
UPDATE qa_rsi SET charid={id};
INSERT INTO rsivalues SELECT * FROM qa_rsi;
DROP TEMPORARY TABLE qa_rsi;
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
 SELECT {id},slot,ability_id,level,is_loaded,ability_name FROM char_abilities WHERE char_id=110;
");
    }
    foreach(string sql in fixturePlan.AfterHelpers) await database.ExecuteAsync(sql);

        return new NativeQaSeededLoadout(abilities, disciplineAncestors);
    }
}
