using System;
using System.Data;
using System.Linq;
using hds.databases.Entities;
using hds.databases.interfaces;
using hds.shared;

namespace hds.databases
{
    public class MyAuthDBAccess : IAuthDBHandler
    {
        private MatrixDbContext CreateDbContext()
        {
            return new MatrixDbContext(Store.config.dbParams);
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
                    uint userId = (uint) worldList.getUserID();
                    var player = dbContext.Users.Single(u => u.UserId == userId);

                    byte[] publicModulus = new byte[96];
                    byte[] privateExponent = new byte[96];

                    ArrayUtils.copy(player.PublicModulus, 0, publicModulus, 0, 96);
                    ArrayUtils.copy(player.PrivateExponent, 0, privateExponent, 0, 96);
                    var dateTimeCreated = new DateTimeOffset(player.TimeCreated).ToUniversalTime().ToUnixTimeMilliseconds();

                    wl.setPublicModulus(publicModulus);
                    wl.setPrivateExponent(privateExponent);
                    wl.setExistance(true);
                    wl.setUserID((int) player.UserId);
                    wl.setTimeCreated((int) dateTimeCreated);
                }
            }
            catch (Exception)
            {
                // Never log the password or its hash here.
                Output.WriteLine("Auth: account " + wl.getUserID() + " has no usable row or key");
                return false;
            }

            // Prepare to read characters
            using (MatrixDbContext dbContext = CreateDbContext())
            {
                var userId = wl.getUserID();
                var characters = dbContext.Characters.Where(c => c.UserId == userId).Where(c => c.IsDeleted == 0)
                    .ToList();

                wl.getCharPack().setTotalChars(characters.Count);

                foreach (var character in characters)
                {
                    wl.getCharPack().addCharacter(character.Handle, (int) character.CharId, character.Status,
                        character.WorldId);
                }

                var worlds = dbContext.Worlds.OrderBy(w => w.WorldId).ToList();
                foreach (var world in worlds)
                {
                    wl.getWorldPack().AddWorld(world.Name, world.WorldId, world.Status, world.Type,
                        (UInt16) world.NumPlayers);
                }
            }

            return true;
        }
    }
}
