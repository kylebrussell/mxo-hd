using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using hds.shared;

namespace hds
{
    public class ChatCommandsHelper
    {
        public void parseCommand(string data)
        {
            string[] commands = data.Split(' ');

            string command = commands[0].ToLower();
            if (ClientActionsHandler.Dev(Store.currentClient, commands)) return;

            try
            {
                if (command.Equals("?qarevive"))
                {
                    WorldClient caller = Store.currentClient, target = caller;
                    if (commands.Length == 2)
                    {
                        lock (WorldServer.Clients)
                            target = WorldServer.Clients.Values.FirstOrDefault(c =>
                                string.Equals(TimedEffects.PlayerName(c), commands[1], StringComparison.OrdinalIgnoreCase));
                    }
                    bool restored = commands.Length <= 2 && AbilityResurrection.QaRevive(caller, target);
                    new ServerPackets().sendSystemChatMessage(caller, restored ? "QA revive completed in place." :
                        "QA revive refused: requires MXO_DEV_COMMANDS=1, mxotest_ database and a dead online fixture in this world; no pending reconstruction. Usage: ?qarevive [handle]", "BROADCAST");
                    caller.FlushQueue();
                    return;
                }

                if (command.Equals("?fix") && commands.Length > 1)
                {
                    int maxRPC = int.Parse(commands[1]);
                    for (int i = 0; i < maxRPC; i++)
                    {
                        Store.currentClient.playerData.setRPCCounter((UInt16) i);

                        ServerPackets pak = new ServerPackets();
                        pak.sendSystemChatMessage(Store.currentClient, "Trying to fix!", "BROADCAST");
                    }
                }


                if (command.Equals("?spawnobject") && commands.Length == 2)
                {
                    // This works ! 
                    // parse the coord parameters parameters as int
                    UInt16 GoId = UInt16.Parse(commands[1]);
                    GameObjectDefinitions def = new GameObjectDefinitions();
                    FieldInfo info = typeof(GameObjectDefinitions).GetField("Object" + GoId);
                    var instance = (GameObject) info.GetValue(def);
                    instance.DisableAllAttributes();

                    bool hasFieldsEnabled = false;
                    foreach (var propertyInfo in instance.GetType().GetFields())
                    {
                        Attribute theAttribute = (Attribute) propertyInfo.GetValue(instance);
                        switch (theAttribute.getName())
                        {
                            case "Position":
                                hasFieldsEnabled = true;
                                theAttribute.enable();
                                theAttribute.setValue(Store.currentClient.playerInstance.Position.getValue());
                                break;

                            case "Orientation":
                                // A quaternion (x, y, z, w): the player's yaw byte left it all zeros, and the client drew
                                // nothing. Unrotated, as the mission objects are.
                                hasFieldsEnabled = true;
                                theAttribute.enable();
                                theAttribute.setValue(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x80, 0x3f });
                                break;
                        }
                    }

                    ServerPackets packets = new ServerPackets();

                    if (hasFieldsEnabled)
                    {
                        UInt64 currentEntityId = WorldServer.entityIdCounter;
                        WorldServer.entityIdCounter++;
                        WorldServer.gameServerEntities.Add(instance);
                        packets.SendSpawnGameObject(Store.currentClient, instance, currentEntityId);
                    }
                }

                if (command.Equals("?org") && commands.Length == 2)
                {
                    int orgId = 0;
                    try
                    {
                        orgId = Int16.Parse(commands[1].ToString());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    if (orgId >= 0 && orgId < 6)
                    {
                        Store.dbManager.WorldDbHandler.SetOrgId(Store.currentClient.playerData.getCharID(), orgId);
                        // The field is one byte. A signed int selects setValue(float), whose low byte is 0 for 1..5.
                        Store.currentClient.playerInstance.OrganizationID.setValue((byte)orgId);
                        Store.currentClient.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf(
                            (PlayerViewAttributes.SelfOrganizationId, PlayerViewAttributes.U8((byte)orgId))), false);
                        // Other players' clients decide by it whether this one can be fought (OrganizationID, other-view 4)
                        PvpFlag.ToWatchers(Store.currentClient, PlayerViewAttributes.Build((PlayerViewAttributes.OtherOrganizationId, PlayerViewAttributes.U8((byte)orgId))));
                        Store.currentClient.FlushQueue();
                    }
                }

                if (command.Equals("?rep") && commands.Length == 3 && short.TryParse(commands[2], out short reputation))
                {
                    WorldClient client = Store.currentClient;
                    var player = client.playerInstance;
                    (byte org, string column, Attribute attribute) = commands[1] switch
                    {
                        "zion" => (MissionContacts.Zion, "repZion", player.ReputationZionMilitary),
                        "machine" => (MissionContacts.Machines, "repMachine", player.ReputationMachines),
                        "mero" => (MissionContacts.Merovingian, "repMero", player.ReputationMerovingian),
                        _ => (0, null, null),
                    };
                    if (attribute != null)
                    {
                        Store.dbManager.WorldDbHandler.SetReputation(client.playerData.getCharID(), column, reputation);
                        attribute.setValue(unchecked((UInt16)reputation));
                        client.messageQueue.addObjectMessage(player.GetSelfUpdateAttributes(new List<Attribute> { attribute }, true), false);
                        client.messageQueue.addRpcMessage(ServerPackets.BuildOrganizationReputation(org, reputation));
                    }
                }

                if (command.Equals("?contact") && commands.Length <= 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: ?contact goes to the nearest neighbourhood contact in this district, ?contact <id> to that one
                    double px = 0, py = 0, pz = 0;
                    NumericalUtils.LtVector3dToDoubles(Store.currentClient.playerInstance.Position.getValue(), ref px, ref py, ref pz);
                    ushort district = (ushort)Store.currentClient.playerData.getDistrictId();
                    MissionContactSpot spot = commands.Length == 2
                        ? (uint.TryParse(commands[1], out uint contactId) ? MissionContactNpcs.SpotOf(contactId) : null)
                        : MissionContactNpcs.Nearest(district, px, pz);
                    string reply = spot == null ? "No such contact" : spot.metrId != district
                        ? spot.Contact?.name + " is in district " + spot.metrId + " (" + spot.neighbourhood + ")"
                        : spot.Contact?.name + ", " + spot.neighbourhood;
                    if (spot != null && spot.metrId == district)
                    {
                        var at = MissionContactNpcs.InFrontOf(spot);
                        // World units: PlayerHelper.teleport takes ?gotopos units (x100)
                        Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(at.x, at.y, at.z));
                        Store.currentClient.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket((float)at.x, (float)at.y, (float)at.z), false);
                    }
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, reply, "BROADCAST");
                }

                if (command.Equals("?missionobject") && commands.Length == 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: ?missionobject <id>: the current phase's object, spawned again at the player's feet
                    bool moved = MissionStaging.MoveObjectToPlayer(Store.currentClient, int.Parse(commands[1]));
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, moved ? "Object moved" : "No such object", "BROADCAST");
                }

                if (command.Equals("?objstate") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: an object in a state from the client's data, and what the data says it offers (docs/OBJECT-STATES.md)
                    ObjectStates.DevCommand(Store.currentClient, commands);
                }

                if (command.Equals("?trapobj") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: ?trapobj <goid> [hidden]: a trapped object at the player's feet, for Disarm Traps (UtilityAbilities)
                    UtilityAbilities.DevTrapObject(Store.currentClient, commands);
                }

                if (command.Equals("?objtest") && commands.Length >= 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: ?objtest <goid> [key element hex] [use count] [state hex], each "-" to leave it out. A mission-style
                    // object at the player's feet, after the player's own MissionKey again, to find what makes one usable.
                    WorldClient client = Store.currentClient;
                    UInt16 goid = UInt16.Parse(commands[1]);
                    FieldInfo field = typeof(GameObjectDefinitions).GetField("Object" + goid);
                    if (field != null)
                    {
                        GameObject obj = (GameObject)Activator.CreateInstance(field.FieldType, field.Name, goid);
                        obj.UnsetRelatedStaticObjId();
                        obj.DisableAllAttributes();
                        Action<string, byte[]> set = (name, value) =>
                        {
                            if (obj.GetType().GetField(name)?.GetValue(obj) is Attribute attribute)
                            {
                                attribute.enable();
                                attribute.setValue(value);
                            }
                        };
                        ushort instance = client.playerData.missionInstanceCounter;
                        set("Position", client.playerInstance.Position.getValue());
                        set("Orientation", new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x80, 0x3f });
                        if (commands.Length > 2 && commands[2] != "-")
                        {
                            set("MissionKey", new byte[] { Convert.ToByte(commands[2], 16), (byte)instance, (byte)(instance >> 8), 2 });
                        }
                        if (commands.Length > 3 && commands[3] != "-")
                        {
                            set("UseCount", new byte[] { byte.Parse(commands[3]) });
                        }
                        if (commands.Length > 4 && commands[4] != "-")
                        {
                            set("CurrentState", NumericalUtils.uint32ToByteArray(Convert.ToUInt32(commands[4], 16), 1));
                        }

                        client.messageQueue.addObjectMessage(ServerPackets.BuildMissionKeyUpdate(instance), false);
                        UInt64 entity = WorldServer.entityIdCounter++;
                        new ServerPackets().SendSpawnGameObject(client, obj, entity);
                        new ServerPackets().sendSystemChatMessage(client, "Object " + goid + ", instance " + instance, "BROADCAST");
                    }
                }

                if (command.Equals("?missionfloor") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: the elevator ride a player gets from the current mission site's elevator, without finding its door
                    MissionSite site = Store.currentClient.playerData.missionProgress?.site;
                    if (site?.elevatorDoor != null)
                    {
                        MissionStaging.OnElevator(Store.currentClient, site.elevatorDoor.pos_x, site.elevatorDoor.pos_z);
                    }
                }

                if (command.Equals("?modal") && commands.Length == 4 &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: ?modal <message hex> <string id hex> <image hex>, with Yes/No buttons
                    Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildModalMessage(Convert.ToUInt32(commands[3], 16),
                        Convert.ToUInt32(commands[2], 16), Convert.ToUInt32(commands[1], 16), MissionStaging.StringYes, MissionStaging.StringNo));
                    Store.currentClient.FlushQueue();
                }

                if (command.Equals("?ask") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: a Yes/No prompt whose answer comes back through ClientCallbacks (?ask [string id hex])
                    WorldClient asker = Store.currentClient;
                    UInt32 askString = commands.Length > 1 ? Convert.ToUInt32(commands[1], 16) : 0x88000034; // "Spawn another opponent for practice?"
                    ClientCallbacks.ShowModal(asker, 0, askString, MissionStaging.StringYes, MissionStaging.StringNo,
                        left => new ServerPackets().sendSystemChatMessage(asker, "You answered " + (left ? "Yes." : "No."), "BROADCAST"));
                }

                if (command.Equals("?fxdone") && commands.Length == 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: ?fxdone <fx id hex> plays an FX and says when the client reports it ended (81 91)
                    WorldClient watcher = Store.currentClient;
                    ClientCallbacks.PlayFx(watcher, Convert.ToUInt32(commands[1], 16),
                        () => new ServerPackets().sendSystemChatMessage(watcher, "The FX ended.", "BROADCAST"));
                }

                if (command.Equals("?elevator") && commands.Length == 2 &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: the region value the next elevator panel sends (retail's isn't derived yet)
                    Store.currentClient.playerData.elevatorRegionOverride =
                        commands[1] == "off" ? (UInt16?)null : Convert.ToUInt16(commands[1], 16);
                    new ServerPackets().sendSystemChatMessage(Store.currentClient,
                        "Elevator region: " + commands[1], "BROADCAST");
                }

                if (command.Equals("?sector") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: the client's sector properties where the player stands, and the nearest hardline
                    double sx = 0, sy = 0, sz = 0;
                    NumericalUtils.LtVector3dToDoubles(Store.currentClient.playerInstance.Position.getValue(), ref sx, ref sy, ref sz);
                    UInt16 metr = (UInt16)Store.currentClient.playerData.getDistrictId();
                    SectorInfo here = SectorProperties.At(metr, sx, sz);
                    UInt16 hl = TeleportHandler.NearestHardline(metr, sx, sz);
                    Hardline line = HardlineNetwork.Get(metr, hl);
                    string text = here == null
                        ? "No sector at " + (int)sx + "," + (int)sz + " in district " + metr
                        : "Sector " + metr + "/" + here.sector + " " + here.file + ": " + here.NeighbourhoodOrName + ", level " + here.level +
                          (here.contactId > 0 ? ", contact " + here.contact + " (" + here.contactId + ")" : "") +
                          (here.gangType != 0 ? ", gang " + here.gangType : "") + (here.pvpZone ? ", PvP" : "") +
                          (here.missionAreas ? ", missions" : ", no missions") + ", music " + here.musicGroup;
                    if (hl != 0)
                    {
                        text += ". Hardline " + hl + (line != null ? " " + line.name + " at " + (int)line.spotX + "," + (int)line.spotY + "," + (int)line.spotZ : "");
                    }

                    text += NavMesh.Locate(metr, sx, sy, sz, out NavLocation nav)
                        ? ". Nav: " + (nav.indoors ? "indoor" : "outdoor") + " region " + nav.region + " polygon " + nav.polygon + " floor " + (int)nav.floorY + " (record " +
                          BitConverter.ToString(nav.RecordBytes()).Replace("-", " ").ToLowerInvariant() + ")"
                        : ". Nav: no polygon";
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, text, "BROADCAST");
                }

                if (command.Equals("?gotopos") && commands.Length == 4)
                {
                    // parse the coord parameters parameters as int
                    Store.currentClient.messageQueue.addObjectMessage(
                        new PlayerHelper().teleport(int.Parse(commands[1]), int.Parse(commands[2]),
                            int.Parse(commands[3])), false);

                    ServerPackets pak = new ServerPackets();
                    pak.sendSystemChatMessage(Store.currentClient, "Teleported!", "BROADCAST");
                }

                if (command.Equals("?tp") && commands.Length >= 7 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev: ?tp <x> <y> <z> <sector hex> <region hex> <polygon hex> [teleport id hex]: a teleport naming the
                    // place it lands in, as retail's did (world units, not /100)
                    float tx = float.Parse(commands[1], System.Globalization.CultureInfo.InvariantCulture);
                    float ty = float.Parse(commands[2], System.Globalization.CultureInfo.InvariantCulture);
                    float tz = float.Parse(commands[3], System.Globalization.CultureInfo.InvariantCulture);
                    Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(tx, ty, tz));
                    Store.currentClient.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket(tx, ty, tz,
                        Convert.ToUInt16(commands[4], 16), Convert.ToUInt16(commands[5], 16), Convert.ToUInt16(commands[6], 16),
                        commands.Length > 7 ? Convert.ToByte(commands[7], 16) : (byte)(Store.currentClient.playerData.teleportCounter = (byte)(Store.currentClient.playerData.teleportCounter % 255 + 1)),
                        Store.currentClient.playerInstance.YawInterval.getValue()[0]), false);
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, "Teleported to " + commands[4] + "/" + commands[5], "BROADCAST");
                }

                if (command.Equals("?tactic") && commands.Length == 2 &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1" &&
                    Enum.TryParse(commands[1], true, out CombatTactic devTactic))
                {
                    // Dev command: what the client's RPC 42 does, for testing without its tactic buttons
                    byte[] request = { (byte)devTactic, 0, 0, 0, 0, 0 };
                    CombatManager.GetHandler(Store.currentClient).ProcessChangeTactic(ref request);
                }

                if (command.Equals("?additem") && commands.Length == 2 &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1")
                {
                    // Creating items from nothing, sellable on the Marketplace: only on dev servers
                    new ServerPackets().sendSystemChatMessage(Store.currentClient,
                        "?additem is a dev command (start the server with MXO_DEV_COMMANDS=1).", "BROADCAST");
                }
                else if (command.Equals("?additem") && commands.Length == 2)
                {
                    // Dev command: put an item (by goid) into the first free bag slot with the same item-add
                    // RPC (5e) that looting and buying use. A gun added this way becomes the client's Free Fire
                    // weapon if none is chosen yet, as when the retail tutorial's looted Redpill Special arrived.
                    new InventoryHandler().processItemAdd(UInt32.Parse(commands[1]), 0x10);
                    Store.currentClient.FlushQueue();
                }

                if (command.Equals("?addexp") && commands.Length == 2 &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev command: experience with no kill, through the normal path (levels, database).
                    PlayerRewards.GrantExperience(Store.currentClient, UInt32.Parse(commands[1]));
                    Store.currentClient.FlushQueue();
                }

                if (command.Equals("?weaken"))
                {
                    // Dev command: lower the health of nearby NPCs, so a test fight runs real rounds without taking minutes
                    if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                    {
                        ushort health = commands.Length > 1 ? UInt16.Parse(commands[1]) : (ushort)100;
                        double px = 0, py = 0, pz = 0;
                        NumericalUtils.LtVector3dToDoubles(Store.currentClient.playerInstance.Position.getValue(), ref px, ref py, ref pz);
                        int weakened = 0;
                        lock (WorldServer.mobs.SyncRoot)
                        {
                            foreach (Mob mob in WorldServer.mobs)
                            {
                                double dx = mob.getXPos() - px, dz = mob.getZPos() - pz;
                                if (!mob.getIsDead() && dx * dx + dz * dz < 5000 * 5000 && mob.getHealthC() > health)
                                {
                                    mob.setHealthC(health);
                                    weakened++;
                                }
                            }
                        }
                        Output.WriteDebugLog("[DEV] Weakened " + weakened + " NPCs to " + health + " health");
                    }
                    else
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient,
                            "?weaken is a dev command (start the server with MXO_DEV_COMMANDS=1).", "BROADCAST");
                    }
                }

                if (command.Equals("?tradebot"))
                {
                    // Dev command: trade with a partner played by the server, to test the trade window with one client
                    if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                    {
                        TradeHandler.StartBotTrade(Store.currentClient);
                    }
                    else
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient,
                            "?tradebot is a dev command (start the server with MXO_DEV_COMMANDS=1).", "BROADCAST");
                    }
                }

                if (command.Equals("?subway") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev command: the subway map of the district's nearest station, as if a subway car was used
                    if (!SubwayHandler.OpenMap(Store.currentClient, true))
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient, "This district has no subway.", "BROADCAST");
                    }
                }

                if (command.Equals("?mailbot"))
                {
                    // Dev command: a mail from MailBot with a Code Bit 1 attached; mail sent to MailBot comes back
                    // (docs/CLIENT-ACTIONS.md, "Email")
                    if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                    {
                        MailHandler.SendBotMail(Store.currentClient, string.Join(" ", commands.Skip(1)));
                    }
                    else
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient,
                            "?mailbot is a dev command (start the server with MXO_DEV_COMMANDS=1).", "BROADCAST");
                    }
                }

                if (command.Equals("?duelbot") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev command: a player played by the server to duel or fight, so one client can see both sides
                    // (docs/PVP.md). ?duelbot [wait|attack|gun|challenge|pvp], ?duelbot off
                    string arg = commands.Length > 1 ? commands[1] : "";
                    if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient,
                            DuelBot.Dismiss(Store.currentClient) ? "DuelBot left." : "You have no DuelBot.", "BROADCAST");
                    }
                    else if (DuelBot.TryParseMode(arg, out DuelBot.Mode botMode))
                    {
                        DuelBot.Summon(Store.currentClient, botMode);
                        new ServerPackets().sendSystemChatMessage(Store.currentClient, "DuelBot (" + botMode + ") is next to you.", "BROADCAST");
                    }
                    else
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient,
                            "?duelbot [wait|attack|gun|challenge|pvp|off]", "BROADCAST");
                    }
                }
                else if (command.Equals("?duelbot"))
                {
                    new ServerPackets().sendSystemChatMessage(Store.currentClient,
                        "?duelbot is a dev command (start the server with MXO_DEV_COMMANDS=1).", "BROADCAST");
                }

                if (command.Equals("?combatdepth"))
                    new ServerPackets().sendSystemChatMessage(Store.currentClient,
                        CombatDepthDev.Run(Store.currentClient, commands.Skip(1).ToArray()), "BROADCAST");

                if ((command.Equals("?stealthbot") || command.Equals("?stealth")) && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev commands (docs/STEALTH.md): ?stealth <sneak|conceal|evasion|invis|imparted|shadow|off> puts a stealth
                    // state on the caller; ?stealthbot <state> [pvp] | off puts it on a DuelBot next to them (pvp: an enemy).
                    new ServerPackets().sendSystemChatMessage(Store.currentClient,
                        StealthDev.Run(Store.currentClient, command.Equals("?stealthbot"), commands.Skip(1).ToArray()), "BROADCAST");
                }

                if (command.Equals("?pet"))
                {
                    // Dev command: coder simulacra (docs/CODER-PETS.md). ?pet cast <ability>, ?pet summon <ability>, ?pet dummy,
                    // ?pet follow|stay|attack|dismiss|defensive|aggressive|passive, ?pet abilities
                    new ServerPackets().sendSystemChatMessage(Store.currentClient,
                        Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1"
                            ? CoderPets.DevCommand(Store.currentClient, commands)
                            : "?pet is a dev command (start the server with MXO_DEV_COMMANDS=1).", "BROADCAST");
                }

                if (command.Equals("?socialbot"))
                {
                    // Dev command: a player played by the server for rooms, teams, crews, paired emotes and stances, so
                    // one client can see the other side (docs/CLIENT-ACTIONS.md). ?socialbot off sends it away.
                    string reply = Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1"
                        ? SocialBot.Command(Store.currentClient, commands.Skip(1).ToArray())
                        : "?socialbot is a dev command (start the server with MXO_DEV_COMMANDS=1).";
                    if (reply != null)
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient, reply, "BROADCAST");
                    }
                }

                if (command.Equals("?pvpflag") && commands.Length == 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev command: the PvP flag itself, whatever the server type allows /pvp to do
                    PvpFlag.Set(Store.currentClient, commands[1].Equals("on", StringComparison.OrdinalIgnoreCase));
                }

                if (command.Equals("?pvpability") && commands.Length == 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1" &&
                    UInt16.TryParse(commands[1], out UInt16 flagAbility))
                {
                    // Dev command: one of the flag abilities (PvpFlag.Abilities: a PvP pulse, a pacification zone) lands on you
                    if (!PvpFlag.ApplyAbility(Store.currentClient, flagAbility))
                    {
                        new ServerPackets().sendSystemChatMessage(Store.currentClient, "Not a PvP flag ability.", "BROADCAST");
                    }
                }

                if (command.Equals("?exileweakness") && commands.Length >= 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Dev command: the nearest live NPC (within 20 m, optionally of that name) gets an Exile weakness
                    // (CombatSpecials: wood, silver, holywater or none), so the Wooden Stake / Silver Bullet / Holy Water
                    // attacks can be checked on test NPCs. ?exileweakness wood [NPC name]
                    ushort? weakness = CombatSpecials.WeaknessNamed(commands[1]);
                    string wanted = commands.Length > 2 ? string.Join(" ", commands.Skip(2)) : null;
                    Mob nearest = null;
                    double best = 20;
                    lock (WorldServer.mobs.SyncRoot)
                        foreach (Mob mob in WorldServer.mobs)
                        {
                            if (mob.getIsDead() || mob.getDistrict() != Store.currentClient.playerData.getDistrictId() ||
                                wanted != null && !string.Equals(mob.getName(), wanted, StringComparison.OrdinalIgnoreCase)) continue;
                            double metres = CombatCalculator.CalculateDistanceMetres(Store.currentClient.playerInstance.Position.getValue(),
                                mob.getXPos(), mob.getYPos(), mob.getZPos());
                            if (metres < best) { best = metres; nearest = mob; }
                        }
                    if (weakness.HasValue && nearest != null) CombatSpecials.SetWeakness(nearest, weakness.Value);
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, !weakness.HasValue ? "Use wood, silver, holywater or none." :
                        nearest == null ? "No NPC within 20 m." : nearest.getName() + " now has Exile weakness " + weakness.Value + ".", "BROADCAST");
                }

                if (command.Equals("?health") && commands.Length == 1 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, "Current health: " + PvpCombat.HealthOf(Store.currentClient), "BROADCAST");

                if (command.Equals("?health") && commands.Length == 2 && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1" &&
                    UInt16.TryParse(commands[1], out UInt16 devHealth) && devHealth > 0)
                {
                    // Dev command: your own health, so a test fight between players ends in rounds, not minutes. It is a
                    // health change like any other for the health callbacks (Sneak's OnHealthReduceDeactivateAbility, pacify).
                    UInt16 devBefore = NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.Health.getValue(), 1);
                    UInt16 devMax = NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.MaxHealth.getValue(), 1);
                    Store.currentClient.playerInstance.Health.setValue(devHealth);
                    if (devBefore != devHealth) TimedEffects.HealthChanged(TimedEffects.ForPlayer(Store.currentClient), devBefore - devHealth, devHealth < devMax);
                    UInt16 devAfter=PvpCombat.HealthOf(Store.currentClient); // a proc may have changed health during the callback
                    Store.currentClient.messageQueue.addObjectMessage(ServerPackets.BuildSelfHealthUpdate(devAfter), false);
                    PvpFlag.ToWatchers(Store.currentClient, PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(devAfter))));
                    Store.currentClient.FlushQueue();
                }

                if (command.Equals("?dismissbuff") && commands.Length == 2 &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1" &&
                    ushort.TryParse(commands[1], out ushort dismissAbility))
                {
                    // Exercise the same parser, category/prohibition gates and removal path as native80bb.
                    AbilityCancellation.Process(Store.currentClient, BitConverter.GetBytes(dismissAbility));
                    new ServerPackets().sendSystemChatMessage(Store.currentClient,
                        AbilityCancellation.CanDismiss(dismissAbility) ? "Buff dismissal requested: " + dismissAbility :
                        "Buff dismissal refused: " + dismissAbility, "BROADCAST");
                }

                if (command.Equals("?die"))
                {
                    // Dev command: run the player death path without needing a mob strong enough to win.
                    CombatHandler.HandlePlayerDeath(Store.currentClient);
                }

                if (command.Equals("?codeupload") && commands.Length == 1 &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    // Same upload as the hardline, available to native QA without a right-click menu.
                    CodeStorageHandler.ProcessUploadRequest(Store.currentClient);
                }

                if (command.Equals("?compilesession"))
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, CompileSession.Dev(Store.currentClient, commands), "SYSTEM");

                if (command.Equals("?compile") && commands.Length == 2)
                {
                    // Dev-only deterministic compile path, including code storage; native 81 2e keeps its checks.
                    string result = Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1"
                        ? new CraftingHandler().Compile(UInt32.Parse(commands[1]))
                        : "?compile is a dev command (start the server with MXO_DEV_COMMANDS=1).";
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, result, "BROADCAST");
                }

                if (command.Equals("?rsi") && commands.Length == 3)
                {
                    //parse the rsi part and value
                    Store.currentClient.messageQueue.addObjectMessage(
                        new PlayerHelper().changeRsi(commands[1], int.Parse(commands[2])), false);

                    ServerPackets pak = new ServerPackets();
                    pak.sendSystemChatMessage(Store.currentClient, "Rsi changed!", "BROADCAST");
                }

                if (command.Contains("?updatersi"))
                {
                    int[] current = Store.currentClient.playerData.getRsiValues();
                    byte[] rsiData = PacketsUtils.getRSIBytes(current);

                    ServerPackets packets = new ServerPackets();
                    packets.sendAppeareanceUpdate(Store.currentClient, rsiData);
                }

                if (command.Equals("?utilitylocator") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                    DataMining.DevLocator(Store.currentClient,commands);
                if (command.Equals("?utilitystatus") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                    DataMining.DevStatus(Store.currentClient);
                if (command.Equals("?utilitymap") && Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                    UtilityAbilities.DevMissionMap(Store.currentClient,commands);

                if ((command.Equals("?utilityobject") || command.Equals("?spawndatanode")) &&
                    Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1")
                {
                    DataMining.Dev(Store.currentClient, commands);
                }

                if (command.StartsWith("?message"))
                {
                    byte[] theMessage = PacketsUtils.createSystemMessageWithoutRPC(commands[1]);
                    Store.world.sendRPCToAllPlayers(theMessage);
                }

                if (command.Contains("?moa"))
                {
                    string hexMoa = commands[1];
                    byte[] moaRSI = StringUtils.hexStringToBytes(hexMoa);
                    Array.Reverse(moaRSI, 0, moaRSI.Length);

                    ServerPackets pak = new ServerPackets();
                    pak.sendSystemChatMessage(Store.currentClient, "Changed MOA to hexMoa d!", "BROADCAST");
                    pak.sendChangeChangeMoaRSI(Store.currentClient, moaRSI);
                }

                if (command.Equals("?playanim"))
                {
                    string animId = commands[1];
                    if (animId.Length == 4)
                    {
                        ServerPackets pak = new ServerPackets();
                        pak.sendPlayerAnimation(Store.currentClient, animId);
                    }
                }

                if (command.Equals("?playmove"))
                {
                    string animIdString = commands[1];

                    // Should just one byte
                    if (animIdString.Length == 2)
                    {
                        byte animId = Byte.Parse(animIdString);
                        ServerPackets pak = new ServerPackets();
                        pak.sendPlayerMoveAnim(Store.currentClient, animId);
                    }
                }

                if (command.StartsWith("?playfx"))
                {
                    string fxHEDID = commands[1];
                    DynamicArray din = new DynamicArray();


                    byte[] animationId = StringUtils.hexStringToBytes(fxHEDID);
                    byte[] viewID = {0x02, 0x00};

                    Random rand = new Random();
                    ushort updateViewCounter = (ushort) rand.Next(3, 200);
                    byte[] updateCount = NumericalUtils.uint16ToByteArrayShort(updateViewCounter);


                    din.append(viewID);
                    din.append(0x02);
                    din.append(0x80);
                    din.append(0x80);
                    din.append(0x80);
                    din.append(0x90);
                    din.append(0xed);
                    din.append(0x00);
                    din.append(0x30);
                    din.append(animationId);
                    din.append(updateCount);

                    Store.currentClient.messageQueue.addObjectMessage(din.getBytes(), false);
                }

                if (command.Contains("?send"))
                {
                    // Sends a packet from a file
                    string filename = "packet.txt";
                    TextReader tr = new StreamReader(filename);
                    string hexContent = tr.ReadToEnd();
                    hexContent = hexContent.Replace(" ", string.Empty);
                    hexContent = hexContent.Replace(" ", Environment.NewLine);
                    tr.Close();

                    if (hexContent.Length > 0)
                    {
                        Store.currentClient.messageQueue.addObjectMessage(StringUtils.hexStringToBytes(hexContent),
                            false);
                        Output.writeToLogForConsole("[SENDPACK FROM FILE] Content : " + hexContent);
                    }
                }

                if (command.Contains("?mob"))
                {
                    UInt32[] rsiIDs = new UInt32[10];
                    rsiIDs[0] = 0xB7010058;
                    rsiIDs[1] = 0x89090058;
                    rsiIDs[2] = 0xB5010058;
                    rsiIDs[3] = 0x3A030008;
                    rsiIDs[4] = 0x32030008;
                    rsiIDs[5] = 0xD0010058;
                    rsiIDs[6] = 0xD4010058;
                    rsiIDs[7] = 0xB8040004; // Smith
                    rsiIDs[8] = 0x92010058; // Seraph
                    rsiIDs[9] = 0x56050004;
                    Random rand = new Random();
                    int index = rand.Next(0, 9);

                    double x = 0;
                    double y = 0;
                    double z = 0;
                    byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
                    NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);

                    byte[] xPos = NumericalUtils.floatToByteArray((float) x, 1);
                    byte[] yPos = NumericalUtils.floatToByteArray((float) y, 1);
                    byte[] zPos = NumericalUtils.floatToByteArray((float) z, 1);

                    UInt64 currentEntityId = WorldServer.entityIdCounter;
                    WorldServer.entityIdCounter++;
                    uint rotation = 0;

                    Mob theMob = new Mob();
                    theMob.setEntityId(currentEntityId);
                    theMob.setDistrict(Convert.ToUInt16(data[0].ToString()));
                    theMob.setDistrictName(Store.currentClient.playerData.getDistrict());
                    theMob.setName("HD Protector");
                    theMob.setLevel(255);
                    theMob.setHealthM(UInt16.Parse(data[4].ToString()));
                    theMob.setHealthC(UInt16.Parse(data[5].ToString()));
                    theMob.setMobId((ushort) rsiIDs[index]);
                    theMob.setRsiHex(StringUtils.bytesToString_NS(NumericalUtils.uint32ToByteArray(rsiIDs[index], 1)));
                    theMob.setXPos(x);
                    theMob.setYPos(y);
                    theMob.setZPos(z);
                    theMob.xBase = x;
                    theMob.yBase = y;
                    theMob.zBase = z;
                    theMob.setRotation(rotation);
                    theMob.setIsDead(false);
                    theMob.setIsLootable(false);
                    lock (WorldServer.mobs)
                    {
                        WorldServer.mobs.Add(theMob);
                    }

                    lock (WorldServer.gameServerEntities)
                    {
                        WorldServer.gameServerEntities.Add(theMob);
                    }

                    // we use this for a test to see if we can spawn mobs and how we can handle them 
                    // We refactor this 
                }


                if (command.Contains("?sendrpc"))
                {
                    // sends a RPC Packet from a File
                    string filename = "rpcpacket.txt";
                    TextReader tr = new StreamReader(filename);
                    string hexContent = tr.ReadToEnd();
                    hexContent = hexContent.Replace(" ", string.Empty);
                    hexContent = hexContent.Replace(" ", Environment.NewLine);
                    tr.Close();

                    if (hexContent.Length > 0)
                    {
                        Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes(hexContent));
                        Output.writeToLogForConsole("[SENDRPC FROM FILE] Content : " + hexContent);
                    }
                }

                if (command.Contains("?checkrpc"))
                {
                    DynamicArray din = new DynamicArray();
                    din.append(StringUtils.hexStringToBytes(
                        "2E1000FF7D020024000000310000000000000000000000000000000000000000000000000B0053796E61707A65373737001D004F6E2079656168204920646F2072656D656D62657220796F75203A2900"));
                    Store.currentClient.messageQueue.addRpcMessage(din.getBytes());
                }

                if (command.Contains("?testrpc"))
                {
                    UInt16 maxRPC = 255;
                    // Just to reference 
                    if (Store.currentClient.playerData.currentTestRPC <= maxRPC)
                    {
                        // Only if it is below we send it - we test with a 5 size packet
                        DynamicArray din = new DynamicArray();
                        if (Store.currentClient.playerData.currentTestRPC < 256)
                        {
                            din.append(0x80);
                            din.append(NumericalUtils.uint16ToByteArrayShort(Store.currentClient.playerData
                                .currentTestRPC));
                        }
                        else
                        {
                            din.append(0x81);
                            din.append(NumericalUtils.uint16ToByteArray(Store.currentClient.playerData.currentTestRPC,
                                0));
                        }

                        // Test Faction Invites
                        din.append(StringUtils.hexStringToBytes(
                            "0C001C000000000000000E00416674657257686F72754E656F001D00416674657257686F72754E656F2773204D697373696F6E205465616D00"));

                        Store.currentClient.messageQueue.addRpcMessage(din.getBytes());

#if DEBUG
                        ServerPackets pak = new ServerPackets();
                        pak.sendSystemChatMessage(Store.currentClient,
                            "Test RPC Header : " + Store.currentClient.playerData.currentTestRPC.ToString(), "MODAL");
#endif

                        Store.currentClient.playerData.currentTestRPC++;
                    }
                }

                if (command.Equals("?save"))
                {
                    new PlayerHelper().SavePlayerInfo(Store.currentClient);

                    ServerPackets pak = new ServerPackets();
                    pak.sendSaveCharDataMessage(Store.currentClient,
                        StringUtils.charBytesToString_NZ(Store.currentClient.playerInstance.CharacterName.getValue()));
                }
            }
            catch (Exception e)
            {
                Store.currentClient.messageQueue.addRpcMessage(
                    PacketsUtils.createSystemMessage("Error parsing command!", Store.currentClient));
                Output.WriteLine("[CHAT COMMAND PARSER] Error parsing request: " + e.Message);
#if DEBUG
                Output.WriteLine("[CHAT COMMAND PARSER] Error parsing request: " + data);
                Output.WriteLine("[CHAT COMMAND PARSER] DEBUG: " + e.Message + "\n" + e.StackTrace);
#endif
            }
        }
    }
}
