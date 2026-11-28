using System;
using System.Globalization;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private readonly object deathPenaltiesTableLock = new object();
        private volatile bool deathPenaltiesTableEnsured;

        private void EnsureDeathPenalties()
        {
            if (deathPenaltiesTableEnsured) return;
            lock (deathPenaltiesTableLock)
            {
                if (deathPenaltiesTableEnsured) return;
                NonQuery("CREATE TABLE IF NOT EXISTS char_death_penalty " +
                    "(charId INT UNSIGNED PRIMARY KEY, factor FLOAT NOT NULL, updatedAt DOUBLE NOT NULL) ENGINE=InnoDB");
                deathPenaltiesTableEnsured = true;
            }
        }

        public (float Factor, double At) GetDeathPenalty(UInt32 charId)
        {
            EnsureDeathPenalties();
            (float Factor, double At) penalty = (1f, 0d);
            QueryRows("SELECT factor, updatedAt FROM char_death_penalty WHERE charId = @charId LIMIT 1",
                reader => penalty = (Convert.ToSingle(reader.GetValue(0)), Convert.ToDouble(reader.GetValue(1))),
                P("@charId", charId));
            return penalty;
        }

        public void SaveDeathPenalty(UInt32 charId, float factor, double at)
        {
            EnsureDeathPenalties();
            NonQuery("INSERT INTO char_death_penalty (charId, factor, updatedAt) VALUES (@charId, @factor, @updatedAt) " +
                "ON DUPLICATE KEY UPDATE factor = VALUES(factor), updatedAt = VALUES(updatedAt)",
                P("@charId", charId), P("@factor", factor), P("@updatedAt", at));
        }
    }
}
