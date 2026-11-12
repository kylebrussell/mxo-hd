using System;
using System.Globalization;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private void EnsureDeathPenalties() => ExecuteNonResultQuery("CREATE TABLE IF NOT EXISTS char_death_penalty " +
            "(charId INT UNSIGNED PRIMARY KEY, factor FLOAT NOT NULL, updatedAt DOUBLE NOT NULL) ENGINE=InnoDB");

        public (float Factor, double At) GetDeathPenalty(UInt32 charId)
        {
            EnsureDeathPenalties();
            OpenConnection();
            try
            {
                using var command = conn.CreateCommand();
                command.CommandText = "SELECT factor,updatedAt FROM char_death_penalty WHERE charId=" + charId;
                using var reader = command.ExecuteReader();
                return reader.Read() ? (Convert.ToSingle(reader.GetValue(0)), Convert.ToDouble(reader.GetValue(1))) : (1f, 0d);
            }
            finally { CloseConnection(); }
        }

        public void SaveDeathPenalty(UInt32 charId, float factor, double at)
        {
            EnsureDeathPenalties();
            ExecuteNonResultQuery("INSERT INTO char_death_penalty VALUES (" + charId + "," +
                factor.ToString("R", CultureInfo.InvariantCulture) + "," + at.ToString("R", CultureInfo.InvariantCulture) +
                ") ON DUPLICATE KEY UPDATE factor=VALUES(factor),updatedAt=VALUES(updatedAt)");
        }
    }
}
