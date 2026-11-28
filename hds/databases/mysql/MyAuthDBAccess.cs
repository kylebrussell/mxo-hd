using System;
using System.Linq;
using hds.databases.Entities;
using hds.databases.interfaces;
using hds.shared;
using Microsoft.EntityFrameworkCore;

namespace hds.databases
{
    public class MyAuthDBAccess : IAuthDBHandler
    {
        private MatrixDbContext CreateDbContext()
        {
            return MatrixDbContext.Create(Store.config.dbParams);
        }

        public bool FetchWorldList(ref WorldList wl)
        {
            // Doesnt exist by default
            wl.setExistance(false);

            var worldList = wl;

            try
            {
                using (MatrixDbContext dbContext = CreateDbContext())
                {
                    // The auth handler has already checked the password (AccountLogin) and set the user id.
                    uint userId = (uint)worldList.getUserID();
                    var player = dbContext.Users.AsNoTracking().Single(u => u.UserId == userId);

                    byte[] publicModulus = new byte[96];
                    byte[] privateExponent = new byte[96];

                    ArrayUtils.copy(player.PublicModulus, 0, publicModulus, 0, 96);
                    ArrayUtils.copy(player.PrivateExponent, 0, privateExponent, 0, 96);
                    var dateTimeCreated = new DateTimeOffset(player.TimeCreated).ToUniversalTime().ToUnixTimeMilliseconds();

                    wl.setPublicModulus(publicModulus);
                    wl.setPrivateExponent(privateExponent);
                    wl.setExistance(true);
                    wl.setUserID((int)player.UserId);
                    wl.setTimeCreated((int)dateTimeCreated);

                    var characters = dbContext.Characters.AsNoTracking()
                        .Where(c => c.UserId == userId && c.IsDeleted == 0)
                        .ToList();

                    wl.getCharPack().setTotalChars(characters.Count);

                    foreach (var character in characters)
                    {
                        wl.getCharPack().addCharacter(character.Handle, (int)character.CharId, character.Status,
                            character.WorldId);
                    }

                    var worlds = dbContext.Worlds.AsNoTracking().OrderBy(w => w.WorldId).ToList();
                    foreach (var world in worlds)
                    {
                        wl.getWorldPack().AddWorld(world.Name, world.WorldId, world.Status, world.Type,
                            (UInt16)world.NumPlayers);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never log the password or its hash here.
                Output.WriteLine("Auth: failed to load world list for account " + wl.getUserID() + " (" + ex.GetType().Name + ")");
                return false;
            }

            return true;
        }
    }
}
