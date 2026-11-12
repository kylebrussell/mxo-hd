using System;
using System.Collections.Generic;
using MySqlConnector;
using hds.shared;

namespace hds.databases
{
    /// <summary>Ground items and inventory transfer commit together. This table belongs to the selected server DB.</summary>
    public static class WorldLuggablesDb
    {
        private static MySqlConnection Open()
        {
            var p = Store.config.dbParams;
            var c = new MySqlConnection($"Server={p.Host};Port={p.Port};Database={p.DatabaseName};User ID={p.Username};Password={p.Password};Pooling=true;");
            c.Open(); return c;
        }
        private static MySqlCommand Cmd(MySqlConnection c, MySqlTransaction t, string sql, params object[] values)
        {
            var q = new MySqlCommand(sql, c, t);
            for (int i = 0; i < values.Length; i += 2) q.Parameters.AddWithValue((string)values[i], values[i + 1]);
            return q;
        }
        public static List<DataMining.Ground> Load()
        {
            using var c = Open();
            using (var q = Cmd(c, null, "CREATE TABLE IF NOT EXISTS world_luggables (id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY, goid SMALLINT UNSIGNED NOT NULL, district SMALLINT UNSIGNED NOT NULL, x DOUBLE NOT NULL,y DOUBLE NOT NULL,z DOUBLE NOT NULL) ENGINE=InnoDB")) q.ExecuteNonQuery();
            using var select = Cmd(c, null, "SELECT id,goid,district,x,y,z FROM world_luggables");
            using var r = select.ExecuteReader(); var items = new List<DataMining.Ground>();
            while (r.Read()) items.Add(new DataMining.Ground { Id = r.GetUInt64(0), Goid = r.GetUInt16(1), District = r.GetUInt16(2), X = r.GetDouble(3), Y = r.GetDouble(4), Z = r.GetDouble(5) });
            return items;
        }
        public static ulong Insert(DataMining.Ground g)
        {
            using var c = Open(); return Insert(c, null, g);
        }
        private static ulong Insert(MySqlConnection c, MySqlTransaction t, DataMining.Ground g)
        {
            using var q = Cmd(c, t, "INSERT INTO world_luggables(goid,district,x,y,z) VALUES(@g,@d,@x,@y,@z)", "@g", g.Goid, "@d", g.District, "@x", g.X, "@y", g.Y, "@z", g.Z);
            q.ExecuteNonQuery(); return (ulong)q.LastInsertedId;
        }
        public static bool Drop(uint character, ushort slot, DataMining.Ground g)
        {
            using var c = Open(); using var t = c.BeginTransaction();
            using (var q = Cmd(c, t, "DELETE FROM inventory WHERE charId=@c AND slot=@s AND goid=@g AND count=1", "@c", character, "@s", slot, "@g", g.Goid))
                if (q.ExecuteNonQuery() != 1) return false;
            g.Id = Insert(c, t, g); t.Commit(); return true;
        }
        public static ushort Pickup(uint character, DataMining.Ground g)
        {
            using var c = Open(); using var t = c.BeginTransaction();
            // Lock the ground row before finding space: another player cannot take the same object.
            using (var q = Cmd(c, t, "SELECT goid FROM world_luggables WHERE id=@id FOR UPDATE", "@id", g.Id))
                if (Convert.ToUInt32(q.ExecuteScalar() ?? 0) != g.Goid) return 0;
            var used = new HashSet<ushort>();
            using (var q = Cmd(c, t, "SELECT slot FROM inventory WHERE charId=@c FOR UPDATE", "@c", character))
            using (var r = q.ExecuteReader()) while (r.Read()) used.Add(r.GetUInt16(0));
            ushort slot = 1; while (slot <= 96 && used.Contains(slot)) slot++;
            if (slot > 96) return 0;
            using (var q = Cmd(c, t, "INSERT INTO inventory(charId,slot,goid,count) VALUES(@c,@s,@g,1)", "@c", character, "@s", slot, "@g", g.Goid)) q.ExecuteNonQuery();
            using (var q = Cmd(c, t, "DELETE FROM world_luggables WHERE id=@id", "@id", g.Id)) q.ExecuteNonQuery();
            t.Commit(); return slot;
        }
    }
}
