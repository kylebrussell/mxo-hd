using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using hds.databases.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

using hds.databases.interfaces;
using hds.shared;

namespace hds.databases{

    public partial class MyMarginDBAccess : IMarginDBHandler{

        private static readonly HashSet<string> AllowedRsiColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sex", "body", "hat", "face", "shirt", "coat", "pants", "shoes",
            "gloves", "glasses", "hair", "facialdetail", "shirtcolor", "pantscolor",
            "coatcolor", "shoecolor", "glassescolor", "haircolor", "skintone",
            "tattoo", "facialdetailcolor", "leggins"
        };

        internal static bool IsAllowedRsiColumn(string field)
        {
            return !string.IsNullOrEmpty(field) && AllowedRsiColumns.Contains(field);
        }

		private readonly string connectionString;
		private DbParams dbParams;
		
		public MyMarginDBAccess(){

            var config = Store.config;
            dbParams = config.dbParams;
			/* Params: Host, port, database, user, password */
            connectionString = ("Server=" + config.dbParams.Host + ";" +
                "Port=" + config.dbParams.Port + ";" +
                "Database=" + config.dbParams.DatabaseName + ";" +
                "User ID=" + config.dbParams.Username + ";" +
                "Password=" + config.dbParams.Password + ";" +
                "Pooling=true;");
			
		}

		private MatrixDbContext CreateDbContext()
		{
			return MatrixDbContext.Create(dbParams);
		}

        public Character GetCharInfo(int charId)
        {
	        using (MatrixDbContext dbContext = CreateDbContext())
	        {
		        return dbContext.Characters.AsNoTracking().SingleOrDefault(c => c.CharId == (ulong) charId);
	        }
        }

        public Rsivalue LoadAppearance(int charId)
        {
            using var dbContext = CreateDbContext();
            return dbContext.Rsivalues.AsNoTracking().SingleOrDefault(rsi => rsi.Charid == charId);
        }

        public List<MarginInventoryItem> LoadInventory(int charId)
        {
	        using (MatrixDbContext dbContext = CreateDbContext())
	        {
		        var items = dbContext.Inventories.AsNoTracking().Where(i => i.CharId == (ulong) charId);
		        List<MarginInventoryItem> Inventory = new List<MarginInventoryItem>();
		        foreach (var inventoryItem in items)
		        {
			        MarginInventoryItem item = new MarginInventoryItem();
			        item.setItemID((uint) inventoryItem.Goid);
			        item.setPurity((UInt16)inventoryItem.Purity);
			        item.setAmount((UInt16)inventoryItem.Count);
			        item.setSlot(inventoryItem.Slot);
			        Inventory.Add(item);
		        }

		        return Inventory;
	        }
        }

        public string LoadAllHardlines()
        {
	        using (MatrixDbContext dbContext = CreateDbContext())
	        {
		        var hardLines = dbContext.DataHardlines.AsNoTracking().ToList();

		        StringBuilder hexpacket = new StringBuilder(hardLines.Count * 12);
		        foreach (var hardline in hardLines)
		        {
			        string districtHex =  StringUtils.bytesToString_NS(NumericalUtils.uint16ToByteArrayShort((UInt16)hardline.DistrictId));
			        string hardlineHex = StringUtils.bytesToString_NS(NumericalUtils.uint16ToByteArray((UInt16)hardline.HardLineId,1));
			        hexpacket.Append(districtHex).Append(hardlineHex).Append("0000");
		        }

		        return hexpacket.ToString();
	        }
        }

        public List<MarginAbilityItem> LoadAbilities(int charId)
        {
	        using (MatrixDbContext dbContext = CreateDbContext())
	        {
		        List<CharAbility> charAbilities =
			        dbContext.CharAbilities.AsNoTracking()
				        .Where(ca => ca.CharId == charId && ca.Slot < CharacterAttributes.FirstSlot)
				        .OrderBy(ca => ca.Slot).ToList();

		        List<MarginAbilityItem> abilities = new List<MarginAbilityItem>();
		        foreach (var charAbility in charAbilities)
		        {
			        MarginAbilityItem ability = new MarginAbilityItem();
			        ability.setSlot((ushort) charAbility.Slot);
			        ability.setAbilityID((Int32)charAbility.AbilityId);
			        ability.setLevel((UInt16)charAbility.Level);
			        ability.setLoaded(charAbility.IsLoaded);
			        abilities.Add(ability);
		        }

		        return abilities;
	        }
        }


        public UInt32 getNewCharnameID(string handle, UInt32 userId)
        {
	        using (MatrixDbContext dbContext = CreateDbContext())
	        {
		        var character =  dbContext.Characters.AsNoTracking().FirstOrDefault(c => c.Handle == handle && c.IsDeleted == 0);

				int number=0;
				if (character != null)
				{
					number = (int) character.CharId;
				}

				// It does exist... hence... invent a new one
				if(number!=0){
					return 0;
				}
            
	            UInt32 charId = CreateNewCharacter(handle, userId,1);
	            return charId;
	        }
			
		}
		
        public UInt32 CreateNewCharacter(string handle,UInt32 userid,UInt32 worldId)=>ReserveCharacter(handle,userid,worldId);

        public void updateCharacter(string firstName, string lastName, string background, UInt32 charID)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE characters SET firstName = @firstName, lastName = @lastName, background = @background WHERE charid = @charId";
                    cmd.Parameters.AddWithValue("@firstName", firstName ?? "");
                    cmd.Parameters.AddWithValue("@lastName", lastName ?? "");
                    cmd.Parameters.AddWithValue("@background", background ?? "");
                    cmd.Parameters.AddWithValue("@charId", charID);
                    cmd.ExecuteNonQuery();
                }
            }
        }


        public void updateRSIValue(string field, string value, UInt32 charID)
        {
            if (!IsAllowedRsiColumn(field))
            {
                throw new ArgumentException("Unknown RSI column: " + field, nameof(field));
            }
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE rsivalues SET `" + field + "` = @value WHERE charid = @charId";
                    cmd.Parameters.AddWithValue("@value", value);
                    cmd.Parameters.AddWithValue("@charId", charID);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>A new character's starting health, inner strength and attributes (retail level 1 values).</summary>
        public void SetStartingStats(UInt32 charID)
        {
            UInt16 health = PlayerProgression.MaxHealthAtLevel(1);
            UInt16 innerStrength = PlayerProgression.MaxInnerStrengthAtLevel(1);
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE characters SET healthC = @health, healthM = @health, innerStrC = @innerStrength, innerStrM = @innerStrength WHERE charId = @charId";
                    cmd.Parameters.AddWithValue("@health", health);
                    cmd.Parameters.AddWithValue("@innerStrength", innerStrength);
                    cmd.Parameters.AddWithValue("@charId", charID);
                    cmd.ExecuteNonQuery();
                }
            }

            SetStartingAttributes(charID);
        }

        public void SetStartingAttributes(UInt32 charID)
        {
            uint? trait=GetCharInfo(checked((int)charID))?.TraitId;
            CharacterPersonality.Resolve(trait);
            foreach (byte attribute in CharacterAttributes.All)
            {
                addAbility(CharacterAttributes.RowValueOf(attribute), CharacterAttributes.SlotOf(attribute), charID, CharacterAttributes.BaseValue(attribute,trait), 0);
            }
        }

        public Dictionary<byte, UInt16> LoadCharacterAttributes(int charId)
        {
            using (MatrixDbContext dbContext = CreateDbContext())
            {
                var slots=CharacterAttributes.All.Select(a=>(int)CharacterAttributes.SlotOf(a)).ToArray();
                var rows=dbContext.CharAbilities.AsNoTracking().Where(ca=>ca.CharId==charId && slots.Contains(ca.Slot)).OrderBy(ca=>ca.Id).ToArray();
                return AttributeAllocation.ReadValues(rows.Select(row=>new AttributeAllocation.Row(row.Id,row.Slot,row.AbilityId,row.Level)));
            }
        }

        public void addAbility(Int32 abilityID, UInt16 slotID, UInt32 charID, UInt16 level, UInt16 is_loaded)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO char_abilities SET char_id = @charId, slot = @slotId, ability_id = @abilityId, level = @level, is_loaded = @isLoaded, added = NOW()";
                    cmd.Parameters.AddWithValue("@charId", charID);
                    cmd.Parameters.AddWithValue("@slotId", slotID);
                    cmd.Parameters.AddWithValue("@abilityId", abilityID);
                    cmd.Parameters.AddWithValue("@level", level);
                    cmd.Parameters.AddWithValue("@isLoaded", is_loaded);
                    cmd.ExecuteNonQuery();
                }
            }
        }


	    public void AddItemToSlot(UInt32 itemGoId, ushort slotId, UInt32 charId)
	    {

		    // Faster way instead of checking on every item type 
		    if (itemGoId != 0 && slotId != 0 && charId != 0)
		    {
			    using (var connection = new MySqlConnection(connectionString))
			    {
				    connection.Open();
				    using (var cmd = connection.CreateCommand())
				    {
					    cmd.CommandText = "INSERT INTO inventory SET charId = @charId, goid = @goid, slot = @slot, created = NOW()";
					    cmd.Parameters.AddWithValue("@charId", charId);
					    cmd.Parameters.AddWithValue("@goid", itemGoId);
					    cmd.Parameters.AddWithValue("@slot", slotId);
					    cmd.ExecuteNonQuery();
				    }
			    }
		    }
	    }

	    public void deleteCharacter(UInt64 charId)
        {
            // This is a "soft-delete" Method. The Data wouldnt be deleted but will be invisible to the users.
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE characters SET is_deleted = 1 WHERE charId = @charId";
                    cmd.Parameters.AddWithValue("@charId", charId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
	}
}
