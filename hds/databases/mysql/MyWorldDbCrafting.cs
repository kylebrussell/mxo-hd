using System;
using System.Collections.Generic;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private void EnsureRecipes()
        {
            ExecuteNonResultQuery("CREATE TABLE IF NOT EXISTS char_recipes (charId INT UNSIGNED NOT NULL, " +
                "goid BIGINT UNSIGNED NOT NULL, PRIMARY KEY (charId,goid)) ENGINE=InnoDB");
        }

        public HashSet<UInt32> GetLearnedRecipes(UInt32 charId)
        {
            EnsureRecipes();
            OpenConnection();
            try
            {
                using var command = conn.CreateCommand();
                command.CommandText = "SELECT goid FROM char_recipes WHERE charId=" + charId;
                using var reader = command.ExecuteReader();
                var result = new HashSet<UInt32>();
                while (reader.Read()) result.Add(Convert.ToUInt32(reader.GetValue(0)));
                return result;
            }
            finally { CloseConnection(); }
        }

        public void LearnRecipe(UInt32 charId, UInt32 goid)
        {
            EnsureRecipes();
            ExecuteNonResultQuery("INSERT IGNORE INTO char_recipes (charId,goid) VALUES (" + charId + "," + goid + ")");
        }
    }
}
