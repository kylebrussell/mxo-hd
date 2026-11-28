using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using hds.world.Structures;

namespace hds
{
    class DataLoader
    {
        private static volatile DataLoader Instance;
        private static readonly string[] StaticObjectCsvFiles =
        {
            "staticObjects_slums_1.csv",
            "staticObjects_slums_2.csv",
            "staticObjects_it.csv",
            "staticObjects_dt_1.csv",
            "staticObjects_dt_2.csv",
            "staticObjects_small03.csv",
            "staticObjects_large01.csv",
            "staticObjects_large02.csv",
            "staticObjects_widowsmoor.csv",
            "staticObjects_medium01.csv",
            "staticObjects_medium02.csv",
            "staticObjects_medium03.csv",
            "staticObjects_archive01.csv",
            "staticObjects_yuki.csv",
            "staticObjects_archive02.csv",
            "staticObjects_archive_ashencourte.csv",
            "staticObjects_datamine.csv",
            "staticObjects_archive_sakura.csv",
            "staticObjects_archive_sati.csv"
        };

        private readonly Maths mathUtils = new Maths();
        private readonly Dictionary<UInt16, byte[]> staticObjectTypeCache = new Dictionary<UInt16, byte[]>(1024);
        private StringSpanInterner activeQuatInterner;

        // Lock-free published O(1) lookup tables preserving exact first-match List<T>.Find semantics.
        // Rebuilt automatically if tests mutate the underlying public lists after load.
        private static readonly object CatalogIndexLock = new object();
        private volatile Dictionary<UInt16, AbilityItem> abilityById;
        private int abilityIndexCount = -1;
        private volatile Dictionary<int, GameObjectItem> gameObjectById;
        private int gameObjectIndexCount = -1;
        private volatile Dictionary<UInt32, ClothingItem> clothingByGoid;
        private int clothingIndexCount = -1;
        private volatile Dictionary<UInt32, byte> emoteByLongId;
        private int emoteIndexCount = -1;

        private static readonly CultureInfo BuffTimeCulture = CreateBuffTimeCulture();

        private static CultureInfo CreateBuffTimeCulture()
        {
            CultureInfo ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            ci.NumberFormat.CurrencyDecimalSeparator = ".";
            return ci;
        }

        // Data Storage - maybe we need to change this to something better (like a AbilityQuery Object)
        public List<AbilityItem> AbilityDB = new List<AbilityItem>(); // holds our Ability DB
        public List<GameObjectItem> GODB = new List<GameObjectItem>(); // Holds the GameObjects
        public List<ClothingItem> ClothingRSIDB = new List<ClothingItem>();
        public List<StaticWorldObject> WorldObjectsDB = new List<StaticWorldObject>();
        public StaticObjectIndex WorldObjectsById;
        public List<EmoteItem> Emotes = new List<EmoteItem>();
        public List<NewRSIItem> newRSIItemsDb = new List<NewRSIItem>();
        public List<Vendor> Vendors = new List<Vendor>();
        public List<Subway> Subways = new List<Subway>();
        public List<NPC_Singpost> Signposts = new List<NPC_Singpost>();
        public List<Mission> missions = new List<Mission>();


        public bool useSQLiteDatabase = false;

        internal static DataLoader CreateEmptyForTesting()
        {
            return new DataLoader(loadFiles: false);
        }

        private DataLoader(bool loadFiles)
        {
            WorldObjectsById = new StaticObjectIndex(WorldObjectsDB);
            if (!loadFiles)
            {
                return;
            }
        }


        private DataLoader()
        {
            WorldObjectsById = new StaticObjectIndex(WorldObjectsDB);
            loadEmotes();
            LoadMissions();
            loadNewRSIIDs(DataPath("newrsiIDs.csv"));
            loadGODB(DataPath("gameobjects.csv"));
            // The whole world unless MXO_MOBS=test (this used to depend on the build: Debug got only the test list)
            if (MobSpawnFilter.UseTestList(Environment.GetEnvironmentVariable("MXO_MOBS")))
            {
                loadMobs(DataPath("mob_just_some_and_winter.csv"));
            }
            else
            {
                loadMobs(DataPath("mob.csv"), true);
                foreach (NpcSpawnScope.Scope scope in Enum.GetValues(typeof(NpcSpawnScope.Scope)))
                    if (NpcSpawnScope.LoadAmbient(scope)) loadMobs(DataPath("npc_spawns", scope + ".csv"), true);
            }


            loadAbilityDB(DataPath("abilityIDs.csv"));
            loadClothingDB(DataPath("mxoClothing.csv"));

            loadVendorItems(DataPath("vendor_items.csv"));
            loadNPCSignPosts(DataPath("npcsign_parsed.csv"));

            long estimatedStaticBytes = 0;
            for (int i = 0; i < StaticObjectCsvFiles.Length; i++)
            {
                string fullPath = DataPath(StaticObjectCsvFiles[i]);
                if (File.Exists(fullPath))
                {
                    estimatedStaticBytes += new FileInfo(fullPath).Length;
                }
            }
            int estimatedRows = (int)Math.Min(int.MaxValue, estimatedStaticBytes / 100);
            if (estimatedRows > 0)
            {
                WorldObjectsDB.EnsureCapacity(estimatedRows);
                WorldObjectsById.EnsureCapacity(estimatedRows);
            }

            activeQuatInterner = new StringSpanInterner(4096);
            try
            {
                for (int i = 0; i < StaticObjectCsvFiles.Length; i++)
                {
                    loadWorldObjectsDb(DataPath(StaticObjectCsvFiles[i]));
                }
            }
            finally
            {
                activeQuatInterner = null;
            }
        }

        public void LoadMissions()
        {
            string missionsPath = DataPath("missions");
            if (!Directory.Exists(missionsPath))
            {
                Output.WriteLine("Mission data folder not found: " + missionsPath);
                return;
            }

            foreach (string file in Directory.GetFiles(missionsPath, "*.xml"))
            {
                XmlDocument missionDocument = new XmlDocument();
                missionDocument.Load(file);
                Mission mission = MissionXml.Parse(missionDocument.DocumentElement);
                if (mission != null)
                {
                    missions.Add(mission);
                }
            }

            missions.Sort((x, y) => x.order.CompareTo(y.order));
            Output.WriteLine("Loaded " + missions.Count + " missions");
        }

        /// <summary>What a contact offers: the first unfinished story mission, or all of them to replay once they're done.
        /// Only Zion's early missions exist so far, so every organisation is offered them.</summary>
        public List<Mission> FindMissions(MissionContact contact, HashSet<int> completed)
        {
            return ContactOfferWithStandards(missions.FindAll(m => MissionContacts.Offers(contact, m)), completed);
        }

        // Stored standard missions are independently repeatable; they never enter the ordered story selection.
        public static List<Mission> ContactOfferWithStandards(List<Mission> missions, HashSet<int> completed)
        {
            List<Mission> result = ContactOffer(missions.FindAll(m => m.plot), completed);
            result.AddRange(missions.Where(m => !m.plot).OrderBy(m => m.id));
            return result;
        }

        public static List<Mission> ContactOffer(List<Mission> missions, HashSet<int> completed)
        {
            foreach (Mission mission in missions)
            {
                if (!completed.Contains(mission.id))
                {
                    return new List<Mission> { mission };
                }
            }

            return new List<Mission>(missions);
        }

        private void loadNPCSignPosts(string path)
        {
            if (!File.Exists(path))
            {
                Output.WriteLine("NPC signpost data not found, skipping: " + path);
                return;
            }

            int linecount = 1;
            foreach (string[] data in StreamCsvRows(path, ';'))
            {
                if (linecount > 0)
                {
                    NPC_Singpost singpost = new NPC_Singpost();
                    singpost.mxoStaticId = UInt32.Parse(data[0]);
                    singpost.districtId = ushort.Parse(data[1]);
                    singpost.xPos = float.Parse(data[3]);
                    singpost.yPos = float.Parse(data[4]);
                    singpost.zPos = float.Parse(data[5]);
                    singpost.SingpostNameString = data[6];
                    singpost.AnimationID0 = StringUtils.hexStringToBytes(data[7]);
                    singpost.SingpostReqReputation = ushort.Parse(data[8]);
                    singpost.DescriptionRez_ID = StringUtils.hexStringToBytes(data[9]);

                    if (data.ElementAtOrDefault(10) != null)
                    {
                        singpost.wQuad = float.Parse(data[10]);
                    }


                    if (data.ElementAtOrDefault(11) != null)
                    {
                        if (data[11].Length > 0)
                        {
                            singpost.xQuad = float.Parse(data[11]);
                        }
                    }

                    if (data.ElementAtOrDefault(12) != null)
                    {
                        if (data[12].Length > 0)
                        {
                            singpost.yQuad = float.Parse(data[12]);
                        }
                    }

                    if (data.ElementAtOrDefault(13) != null)
                    {
                        if (data[13].Length > 0)
                        {
                            singpost.zQuad = float.Parse(data[13]);
                        }
                    }

                    Signposts.Add(singpost);
                }

                linecount++;
            }
        }

        public void loadNewRSIIDs(string path)
        {
            Output.Write("Loading RSI IDs for Character Creation ..");
            int linecount = 1;
            foreach (string[] data in StreamCsvRows(path, ','))
            {
                if (linecount > 1)
                {
                    NewRSIItem item = new NewRSIItem();
                    item.name = data[0];
                    item.newRSIID = UInt16.Parse(data[1]);
                    item.internalId = UInt16.Parse(data[2]);
                    item.type = data[3];
                    item.gender = ushort.Parse(data[4]);
                    newRSIItemsDb.Add(item);
                }

                linecount++;
            }
        }

        public NewRSIItem getNewRSIItemByTypeAndID(string type, UInt16 newRSIID)
        {
            NewRSIItem newrsiItemTemp = newRSIItemsDb.Find(delegate(NewRSIItem temp)
            {
                return temp.newRSIID == newRSIID && temp.type == type;
            });
            if (newrsiItemTemp == null)
            {
                NewRSIItem emptyItem = new NewRSIItem();
                return emptyItem;
            }

            return newrsiItemTemp;
        }

        public void loadMobs(string path, bool skipRepeats = false)
        {
            // This is just an example of loading CSV    
            //return dataTable;
            Output.Write("Loading Hostile Mobs ..");
            int linecount = 1;
            int repeats = 0;
            foreach (string[] data in StreamCsvRows(path, ';'))
            {
                if (skipRepeats && linecount > 1 &&
                    mobsSeen.IsRepeat(Convert.ToUInt16(data[0]), data[2], double.Parse(data[7]), double.Parse(data[9]), double.Parse(data[8])))
                {
                    repeats++;
                    linecount++;
                    continue;
                }

                //Output.WriteLine("Show Colums for Line : " + linecount.ToString() + " GOID:  " + data[1].ToString() + " Name " + data[0].ToString());
                if (linecount > 1)
                {
                    UInt64 currentEntityId = WorldServer.entityIdCounter;
                    WorldServer.entityIdCounter++;
                    uint rotation = 0;
                    if (data[10].Length > 0)
                    {
                        rotation = uint.Parse(data[10]);
                    }


                    if (data[4].Length > 0 && data[5].Length > 0)
                    {
                        bool error = false;
                        Mob theMob = new Mob();
                        theMob.setEntityId(currentEntityId);
                        theMob.setDistrict(Convert.ToUInt16(data[0]));
                        theMob.setDistrictName(data[1]);
                        theMob.setName(data[2]);
                        theMob.setLevel(ushort.Parse(data[3]));
                        theMob.setHealthM(UInt16.Parse(data[4]));
                        theMob.setHealthC(UInt16.Parse(data[4])); // As max Health should be the current onload
                        if (UInt16.Parse(data[4]) == 0)
                        {
                            error = true;
                        }

                        theMob.setMobId((ushort) linecount);
                        theMob.setRsiHex(data[6]);
                        if (!isNumberEven(data[6].Length))
                        {
                            error = true;
                        }

                        theMob.setXPos(double.Parse(data[7]));
                        theMob.setYPos(double.Parse(data[8]));
                        theMob.setZPos(double.Parse(data[9]));
                        theMob.xBase = double.Parse(data[7]);
                        theMob.yBase = double.Parse(data[8]);
                        theMob.zBase = double.Parse(data[9]);
                        theMob.setRotation(rotation);
                        theMob.setIsDead(bool.Parse(data[11]));
                        theMob.setIsLootable(bool.Parse(data[12]));
                        // mob_parsed.csv column 21: the NPC's EquippedItemID from retail spawns (693 = AutomaticRifle2,
                        // 2503 = DualSubmachinegun2, ...; see docs/PACKET-RESEARCH.md, Object599 / NPC_BASE)
                        if (data.Length > 20 && uint.TryParse(data[20], out uint weaponGoid))
                        {
                            theMob.setWeapon(weaponGoid);
                        }
                        if (data.Length > 21 && byte.TryParse(data[21], out byte awareness))
                            theMob.CapturedStealthAwareness = awareness;
                        theMob.CapturedEvasion = NpcEvasion.FromSpawnRow(data, theMob);
                        if (skipRepeats)
                        {
                            // The full world, not the test list: who attacks on sight
                            theMob.aggroKind = MobBehaviour.Classify(data[2], data.Length > 21 ? data[21] : "");
                        }

                        // Init the Mob Update
                        if (!error)
                        {
                            theMob.DoMobUpdate(theMob);
                            WorldServer.mobs.Add(theMob);
                            WorldServer.gameServerEntities.Add(theMob);
                        }
                    }
                }

                linecount++;
            }

            Output.WriteDebugLog("[MOBS] " + Path.GetFileName(path) + ": " + repeats + " repeat rows skipped, " + WorldServer.mobs.Count + " NPCs now");
        }

        private readonly MobSpawnFilter.Seen mobsSeen = new MobSpawnFilter.Seen();

        private bool isNumberEven(int value)
        {
            return value % 2 == 0;
        }

        private static IEnumerable<string[]> StreamCsvRows(string path, char separator)
        {
            string resolved = ResolveDataPath(path);
            int i = 0;
            using (StreamReader sr = new StreamReader(resolved, Encoding.UTF8, true, 65536))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    i++;
                    yield return line.Split(separator);
                }
            }
            Output.Write(i + " Entrys added !\n");
        }

        public ArrayList loadCSV(string path, char seperator)
        {
            path = ResolveDataPath(path);
            ArrayList rows = new ArrayList();
            using (StreamReader sr = new StreamReader(path))
            {
                string line;
                int i = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    string[] Colum = line.Split(seperator);
                    rows.Add(Colum);
                    //Console.Write("\r{0}%   ", i);
                    i++;
                }

                Output.Write(i + " Entrys added !\n");
            }
            // lets test a little bit to see what data is there


            return rows;
        }

        internal static string DataPath(params string[] parts)
        {
            string[] dataParts = new string[parts.Length + 1];
            dataParts[0] = "data";
            Array.Copy(parts, 0, dataParts, 1, parts.Length);
            return ResolveDataPath(Path.Combine(dataParts));
        }

        internal static string ResolveDataPath(string path)
        {
            string normalized = path.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            string workingDirectoryPath = Path.GetFullPath(normalized);
            if (File.Exists(workingDirectoryPath) || Directory.Exists(workingDirectoryPath))
            {
                return workingDirectoryPath;
            }

            string outputDirectoryPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalized));
            if (File.Exists(outputDirectoryPath) || Directory.Exists(outputDirectoryPath))
            {
                return outputDirectoryPath;
            }

            return workingDirectoryPath;
        }

        public void loadGODB(string path)
        {
            // This is just an example of loading CSV    
            //return dataTable;
            Output.Write("Loading GameObjects ..");
            int linecount = 0;
            foreach (string[] data in StreamCsvRows(path, ','))
            {
                //Output.WriteLine("Show Colums for Line : " + linecount.ToString() + " GOID:  " + data[1].ToString() + " Name " + data[0].ToString());

                GameObjectItem goItem = new GameObjectItem();
                goItem.setGOID(Convert.ToInt32(data[1]));
                goItem.setName(data[0]);
                GODB.Add(goItem);
                linecount++;
            }
            RebuildGameObjectIndex();
        }

        public void loadAbilityDB(string path)
        {
            Output.Write("Loading Abilities..");

            int linecount = 1;
            foreach (string[] data in StreamCsvRows(path, ';'))
            {
                AbilityItem ability = new AbilityItem();
                // we want to skip the first line as it should have the Names
                if (linecount > 1)
                {
                    ability.setAbilityID(Convert.ToUInt16(data[0]));
                    ability.setGOID(Convert.ToInt32(data[1]));
                    ability.setAbilityName(data[2]);
                    if (data[3].Length > 0)
                    {
                        ability.setIsCastable(bool.Parse(data[3]));
                    }

                    if (data.Length >= 5)
                    {
                        if (data[4].Length > 0)
                        {
                            ability.setCastingTime(float.Parse(data[4], CultureInfo.InvariantCulture));
                        }
                    }

                    if (data.Length >= 6)
                    {
                        if (data[5].Length > 2)
                        {
                            ability.setCastAnimStart(StringUtils.hexStringToBytes(data[5]));
                        }
                    }

                    if (data.Length >= 7)
                    {
                        if (data[6].Length > 2)
                        {
                            ability.setCastAnimMid(StringUtils.hexStringToBytes((data[6])));
                        }
                    }

                    if (data.Length >= 8)
                    {
                        if (data[7].Length > 2)
                        {
                            ability.setCastAnimEnd(StringUtils.hexStringToBytes(data[7]));
                        }
                    }

                    if (data.Length >= 9)
                    {
                        if (data[8].Length > 0)
                        {
                            ability.setActivationFX(UInt32.Parse(data[8]));
                        }
                    }

                    if (data.Length >= 10)
                    {
                        if (data[9].Length > 0)
                        {
                            ability.setValueFrom(Int16.Parse(data[9]));
                        }
                    }

                    if (data.Length >= 11)
                    {
                        if (data[10].Length > 0)
                        {
                            ability.setValueTo(UInt16.Parse(data[10]));
                        }
                    }

                    if (data.Length >= 12)
                    {
                        if (data[11].Length > 0)
                        {
                            ability.setIsBuff(bool.Parse(data[11]));
                        }
                    }

                    if (data.Length >= 13)
                    {
                        if (data[12].Length > 0)
                        {
                            ability.setBuffTime(float.Parse(data[12], NumberStyles.Any, BuffTimeCulture));
                        }
                    }

                    if (data.Length >= 14)
                    {
                        if (data[13].Length > 0)
                        {
                            ability.setAbilityExecutionFX(UInt32.Parse(data[13]));
                        }
                    }

                    AbilityDB.Add(ability);
                }
                linecount++;
            }
            RebuildAbilityIndex();
        }

        public void loadClothingDB(string path)
        {
            Output.Write("Loading Clothing..");

            int linecount = 1;
            foreach (string[] data in StreamCsvRows(path, ';'))
            {
                if (linecount > 1)
                {
                    //Output.WriteLine("Show Colums for Line : " + linecount.ToString() + " GOID:  " + data[1].ToString() + " Name " + data[0].ToString());

                    ClothingItem clothItem = new ClothingItem();

                    clothItem.setGoidDecimal(Convert.ToUInt16(data[0]));
                    clothItem.setClothesType(data[1]);
                    clothItem.setShortName(data[2]);
                    clothItem.setItemName(data[3]);
                    clothItem.setModelId(Convert.ToUInt16(data[4]));
                    clothItem.setColorId(Convert.ToUInt16(data[5]));
                    ClothingRSIDB.Add(clothItem);
                }
                linecount++;
            }
            RebuildClothingIndex();
        }

        public void loadWorldObjectsDb(string path)
        {
            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                LoadWorldObjectsFile(path);
            }
            finally
            {
                Output.WriteLine("[DATA] " + Path.GetFileName(path) + " loaded in " + loadTimer.ElapsedMilliseconds + " ms");
            }
        }

        internal sealed class StringSpanInterner
        {
            private string[] buckets;
            private int count;

            public StringSpanInterner(int initialCapacity = 1024)
            {
                int size = 16;
                while (size < initialCapacity) size <<= 1;
                buckets = new string[size];
            }

            public int Count => count;

            public string Intern(ReadOnlySpan<char> span)
            {
                if (span.IsEmpty)
                {
                    return string.Empty;
                }

                int hash = string.GetHashCode(span);
                int mask = buckets.Length - 1;
                int slot = hash & mask;
                while (true)
                {
                    string existing = buckets[slot];
                    if (existing == null)
                    {
                        break;
                    }
                    if (existing.AsSpan().SequenceEqual(span))
                    {
                        return existing;
                    }
                    slot = (slot + 1) & mask;
                }

                if ((count + 1) * 10 >= buckets.Length * 7)
                {
                    Resize();
                    mask = buckets.Length - 1;
                    slot = hash & mask;
                    while (buckets[slot] != null)
                    {
                        slot = (slot + 1) & mask;
                    }
                }

                string created = span.ToString();
                buckets[slot] = created;
                count++;
                return created;
            }

            private void Resize()
            {
                string[] oldBuckets = buckets;
                string[] nextBuckets = new string[oldBuckets.Length << 1];
                int mask = nextBuckets.Length - 1;
                for (int i = 0; i < oldBuckets.Length; i++)
                {
                    string entry = oldBuckets[i];
                    if (entry == null) continue;
                    int slot = string.GetHashCode(entry.AsSpan()) & mask;
                    while (nextBuckets[slot] != null)
                    {
                        slot = (slot + 1) & mask;
                    }
                    nextBuckets[slot] = entry;
                }
                buckets = nextBuckets;
            }
        }

        private static int ParseHexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            throw new FormatException("Invalid hex digit: " + c);
        }

        internal static UInt32 ParseHexUInt32LittleEndian(ReadOnlySpan<char> hex)
        {
            if (hex.Length < 8)
            {
                throw new FormatException("Expected at least 8 hex digits for UInt32.");
            }
            uint b0 = (uint)((ParseHexNibble(hex[0]) << 4) | ParseHexNibble(hex[1]));
            uint b1 = (uint)((ParseHexNibble(hex[2]) << 4) | ParseHexNibble(hex[3]));
            uint b2 = (uint)((ParseHexNibble(hex[4]) << 4) | ParseHexNibble(hex[5]));
            uint b3 = (uint)((ParseHexNibble(hex[6]) << 4) | ParseHexNibble(hex[7]));
            return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
        }

        internal static UInt16 ParseHexUInt16LittleEndian(ReadOnlySpan<char> hex)
        {
            if (hex.Length < 4)
            {
                throw new FormatException("Expected at least 4 hex digits for UInt16.");
            }
            uint b0 = (uint)((ParseHexNibble(hex[0]) << 4) | ParseHexNibble(hex[1]));
            uint b1 = (uint)((ParseHexNibble(hex[2]) << 4) | ParseHexNibble(hex[3]));
            return (UInt16)(b0 | (b1 << 8));
        }

        private static ReadOnlySpan<char> NextCsvField(ref ReadOnlySpan<char> remaining)
        {
            int comma = remaining.IndexOf(',');
            if (comma < 0)
            {
                ReadOnlySpan<char> last = remaining;
                remaining = ReadOnlySpan<char>.Empty;
                return last;
            }

            ReadOnlySpan<char> field = remaining.Slice(0, comma);
            remaining = remaining.Slice(comma + 1);
            return field;
        }

        internal static StaticWorldObject ParseStaticWorldObjectLine(
            ReadOnlySpan<char> line,
            Dictionary<UInt16, byte[]> typeCache,
            StringSpanInterner quatInterner,
            out UInt16 typeId)
        {
            ReadOnlySpan<char> rem = line;
            UInt16 metrId = UInt16.Parse(NextCsvField(ref rem), NumberStyles.Integer, CultureInfo.InvariantCulture);
            UInt16 sectorId = UInt16.Parse(NextCsvField(ref rem), NumberStyles.Integer, CultureInfo.InvariantCulture);
            UInt32 mxoStaticId = ParseHexUInt32LittleEndian(NextCsvField(ref rem));
            UInt32 staticId = ParseHexUInt32LittleEndian(NextCsvField(ref rem));

            ReadOnlySpan<char> typeSpan = NextCsvField(ref rem);
            typeId = ParseHexUInt16LittleEndian(typeSpan);
            if (!typeCache.TryGetValue(typeId, out byte[] typeBytes))
            {
                typeBytes = new byte[] { (byte)(typeId & 0xFF), (byte)(typeId >> 8) };
                typeCache[typeId] = typeBytes;
            }

            bool exterior = bool.Parse(NextCsvField(ref rem));
            double posX = double.Parse(NextCsvField(ref rem), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
            double posY = double.Parse(NextCsvField(ref rem), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
            double posZ = double.Parse(NextCsvField(ref rem), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
            double rot = double.Parse(NextCsvField(ref rem), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
            string quat = quatInterner.Intern(NextCsvField(ref rem));

            return new StaticWorldObject
            {
                metrId = metrId,
                sectorID = sectorId,
                mxoStaticId = mxoStaticId,
                staticId = staticId,
                type = typeBytes,
                exterior = exterior,
                pos_x = posX,
                pos_y = posY,
                pos_z = posZ,
                rot = rot,
                quat = quat
            };
        }

        private void LoadWorldObjectsFile(string path)
        {
            Output.Write("Loading Static Objects from " + path + " - please wait...");

            string resolvedPath = ResolveDataPath(path);
            StringSpanInterner quatInterner = activeQuatInterner ?? new StringSpanInterner(1024);
            int nonEmptyLines = 0;

            using (var sr = new StreamReader(resolvedPath, Encoding.UTF8, true, 65536))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    nonEmptyLines++;
                    if (nonEmptyLines == 1) continue; // CSV header

                    StaticWorldObject worldObject = ParseStaticWorldObjectLine(
                        line.AsSpan(), staticObjectTypeCache, quatInterner, out UInt16 typeId);

                    int rowIndex = WorldObjectsDB.Count;
                    WorldObjectsDB.Add(worldObject);
                    WorldObjectsById.AddAt(worldObject, rowIndex);
                    if (typeId == 6568)
                    {
                        AddWorldObjectToWorldServer(worldObject);
                    }
                }
            }

            Output.Write(nonEmptyLines + " Entrys added !\n");
        }

        public void AddWorldObjectToWorldServer(StaticWorldObject worldObject)
        {
            UInt16 typeId = NumericalUtils.ByteArrayToUint16(worldObject.type, 1);
            switch (typeId)
            {
                case 6568:
                    Subway subway = new Subway(worldObject);
                    Subways.Add(subway);
                    WorldServer.subways.Add(subway);
                    WorldServer.gameServerEntities.Add(subway);
                    subway.StartCountdown();
                    break;
            }
        }

        public void loadVendorItems(string dataVendorItemsCsv)
        {
            // metr_id;vendor_static_id;items
            int linecount = 1;
            foreach (string[] data in StreamCsvRows(dataVendorItemsCsv, ';'))
            {
                if (linecount > 1)
                {
                    /*
                    List<UInt32> items = new List<UInt32>();
                    items.Add(38145);
                    items.Add(38142);
                    items.Add(38146);
                    */

                    string intValues = data[2];
                    UInt32[] items = intValues.Split(',').Select(n => Convert.ToUInt32(n)).ToArray();
                    Vendor theVendor = new Vendor(Convert.ToUInt16(data[0]), Convert.ToUInt32(data[1]), items);
                    if (data.Length > 6 && UInt32.TryParse(data[6], out UInt32 npc)) theVendor.npcStaticID = npc;
                    Vendors.Add(theVendor);
                }

                linecount++;
            }
        }

        public void loadEmotes()
        {
            Emotes.Add(new EmoteItem(0xe6020058, 0x01)); // /beckon
            Emotes.Add(new EmoteItem(0x9b010058, 0x02)); // /bigwave
            Emotes.Add(new EmoteItem(0xe7020058, 0x03)); // /bow
            Emotes.Add(new EmoteItem(0x0d00003a, 0x04)); // /clap
            Emotes.Add(new EmoteItem(0x1000003a, 0x05)); // /crossarms
            Emotes.Add(new EmoteItem(0x1100003a, 0x06)); // /nod
            Emotes.Add(new EmoteItem(0x570d0058, 0x07)); // /agree
            Emotes.Add(new EmoteItem(0x590d0058, 0x08)); // /yes
            Emotes.Add(new EmoteItem(0x1600003a, 0x09)); // /orangutan
            Emotes.Add(new EmoteItem(0x10050004, 0x0a)); // /point
            Emotes.Add(new EmoteItem(0xd1020058, 0x0b)); // /pointback
            Emotes.Add(new EmoteItem(0x1300003a, 0x0c)); // /pointleft
            Emotes.Add(new EmoteItem(0x1400003a, 0x0d)); // /pointright
            Emotes.Add(new EmoteItem(0xf50c0058, 0x0e)); // /pointup
            Emotes.Add(new EmoteItem(0xfa0c0058, 0x0f)); // /pointdown
            Emotes.Add(new EmoteItem(0x0e00003a, 0x10)); // /salute
            Emotes.Add(new EmoteItem(0x1200003a, 0x11)); // /shakehead
            Emotes.Add(new EmoteItem(0x580d0058, 0x12)); // /disagree
            Emotes.Add(new EmoteItem(0x0d00003e, 0x13)); // /no
            Emotes.Add(new EmoteItem(0xe9020058, 0x14)); // /stomp
            Emotes.Add(new EmoteItem(0x0f00003a, 0x15)); // /tapfoot
            Emotes.Add(new EmoteItem(0x0c00003a, 0x16)); // /wave
            Emotes.Add(new EmoteItem(0xef0c0058, 0x17)); // /dangerarea
            Emotes.Add(new EmoteItem(0x130e0058, 0x18)); // /comeforward
            Emotes.Add(new EmoteItem(0xf00c0058, 0x19)); // /enemyinsight
            Emotes.Add(new EmoteItem(0xf00c0058, 0x1a)); // /enemy
            Emotes.Add(new EmoteItem(0xf20c0058, 0x1b)); // /disperse
            Emotes.Add(new EmoteItem(0x1500003a, 0x1c)); // /lookaround
            Emotes.Add(new EmoteItem(0xf60c0058, 0x1d)); // /takecover
            Emotes.Add(new EmoteItem(0xf70c0058, 0x1e)); // /cover
            Emotes.Add(new EmoteItem(0xf30c0058, 0x1f)); // /mapcheck
            Emotes.Add(new EmoteItem(0xf80c0058, 0x20)); // /onehandedhandstand
            Emotes.Add(new EmoteItem(0xfb0c0058, 0x21)); // /giggle
            Emotes.Add(new EmoteItem(0xf40c0058, 0x22)); // /handstand
            Emotes.Add(new EmoteItem(0xfc0c0058, 0x23)); // /hearnoevil
            Emotes.Add(new EmoteItem(0xfd0c0058, 0x24)); // /seenoevil
            Emotes.Add(new EmoteItem(0xfe0c0058, 0x25)); // /speaknoevil
            Emotes.Add(new EmoteItem(0x000d0058, 0x26)); // /coverears
            Emotes.Add(new EmoteItem(0x010d0058, 0x27)); // /covermouth
            Emotes.Add(new EmoteItem(0xff0c0058, 0x28)); // /covereyes
            Emotes.Add(new EmoteItem(0x130d0058, 0x29)); // /blowkiss
            Emotes.Add(new EmoteItem(0x1f0d0058, 0x2a)); // /blush
            Emotes.Add(new EmoteItem(0x270d0058, 0x2b)); // /cheer
            Emotes.Add(new EmoteItem(0x650d0058, 0x2c)); // /crackknuckles
            Emotes.Add(new EmoteItem(0x650d0058, 0x2d)); // /crackknuckles
            Emotes.Add(new EmoteItem(0x7a0d0058, 0x2e)); // /cry
            Emotes.Add(new EmoteItem(0x7b0d0058, 0x2f)); // /curtsey
            Emotes.Add(new EmoteItem(0x7c0d0058, 0x30)); // /formalbow
            Emotes.Add(new EmoteItem(0x7d0d0058, 0x31)); // /formalcurtsey
            Emotes.Add(new EmoteItem(0x810d0058, 0x32)); // /bowhead
            Emotes.Add(new EmoteItem(0x810d0058, 0x33)); // /bowhead
            Emotes.Add(new EmoteItem(0x7e0d0058, 0x34)); // /insult
            Emotes.Add(new EmoteItem(0xa9150058, 0x35)); // /scream
            Emotes.Add(new EmoteItem(0xa5150058, 0x36)); // /anguish
            Emotes.Add(new EmoteItem(0x820d0058, 0x37)); // /karatepower
            Emotes.Add(new EmoteItem(0xb90d0058, 0x38)); // /karatepower2
            Emotes.Add(new EmoteItem(0xba0d0058, 0x39)); // /karatepower3
            Emotes.Add(new EmoteItem(0x830d0058, 0x3a)); // /karatespeed
            Emotes.Add(new EmoteItem(0x840d0058, 0x3b)); // /karatespeed2
            Emotes.Add(new EmoteItem(0x850d0058, 0x3c)); // /karatespeed3
            Emotes.Add(new EmoteItem(0xbb0d0058, 0x3d)); // /karatedefense
            Emotes.Add(new EmoteItem(0xbc0d0058, 0x3e)); // /karatedefense2
            Emotes.Add(new EmoteItem(0xbd0d0058, 0x3f)); // /karatedefense3
            Emotes.Add(new EmoteItem(0x870d0058, 0x40)); // /kneel
            Emotes.Add(new EmoteItem(0x880d0058, 0x41)); // /takeaknee
            Emotes.Add(new EmoteItem(0x890d0058, 0x42)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x43)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x44)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x45)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x46)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x47)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x48)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x49)); // /kungfu
            Emotes.Add(new EmoteItem(0x890d0058, 0x4a)); // /kungfu
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x4b)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x4c)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x4d)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x4e)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x4f)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x50)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x51)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x52)); // /aikido
            Emotes.Add(new EmoteItem(0xbe0d0058, 0x53)); // /aikido
            Emotes.Add(new EmoteItem(0x920d0058, 0x54)); // /laugh
            Emotes.Add(new EmoteItem(0x7f0d0058, 0x55)); // /rude
            Emotes.Add(new EmoteItem(0xa40d0058, 0x56)); // /loser
            Emotes.Add(new EmoteItem(0x010000b4, 0x57)); // /bigtrouble
            Emotes.Add(new EmoteItem(0x940d0058, 0x58)); // /okay
            Emotes.Add(new EmoteItem(0x940d0058, 0x59)); // /ok
            Emotes.Add(new EmoteItem(0x960d0058, 0x5a)); // /peace
            Emotes.Add(new EmoteItem(0x770d0058, 0x5b)); // /pullhair
            Emotes.Add(new EmoteItem(0x970d0058, 0x5c)); // /rolldice
            Emotes.Add(new EmoteItem(0x780d0058, 0x5d)); // /sarcasticclap
            Emotes.Add(new EmoteItem(0x790d0058, 0x5e)); // /golfclap
            Emotes.Add(new EmoteItem(0x980d0058, 0x5f)); // /scratchhead
            Emotes.Add(new EmoteItem(0x990d0058, 0x60)); // /shrug
            Emotes.Add(new EmoteItem(0x9a0d0058, 0x62)); // /stretch
            Emotes.Add(new EmoteItem(0xf90c0058, 0x63)); // /suckitdown
            Emotes.Add(new EmoteItem(0x9b0d0058, 0x64)); // /surrender
            Emotes.Add(new EmoteItem(0x9c0d0058, 0x65)); // /thumbsup
            Emotes.Add(new EmoteItem(0x1200003a, 0x66)); // /shakehead
            Emotes.Add(new EmoteItem(0xa20d0058, 0x67)); // /shameshame
            Emotes.Add(new EmoteItem(0x9d0d0058, 0x68)); // /twothumbsup
            Emotes.Add(new EmoteItem(0x9f0d0058, 0x69)); // /puke
            Emotes.Add(new EmoteItem(0x9e0d0058, 0x6a)); // /vomit
            Emotes.Add(new EmoteItem(0xa00d0058, 0x6b)); // /whistle
            Emotes.Add(new EmoteItem(0xa60d0058, 0x6c)); // /grovel
            Emotes.Add(new EmoteItem(0xe1020058, 0x6d)); // /yawn
            Emotes.Add(new EmoteItem(0xa70d0058, 0x6e)); // /plead
            Emotes.Add(new EmoteItem(0xa50d0058, 0x6f)); // /shakefist
            Emotes.Add(new EmoteItem(0x4e0200b4, 0x70)); // /cool
            Emotes.Add(new EmoteItem(0xb70d0058, 0x71)); // /crackneck
            Emotes.Add(new EmoteItem(0x150e0058, 0x72)); // /assemble
            Emotes.Add(new EmoteItem(0x880200b4, 0x73)); // /mockcry
            Emotes.Add(new EmoteItem(0xa70200b4, 0x74)); // /throat
            Emotes.Add(new EmoteItem(0x9b0200b4, 0x75)); // /powerpose
            Emotes.Add(new EmoteItem(0xbf0200b4, 0x76)); // /thumbsdown
            Emotes.Add(new EmoteItem(0xcc0200b4, 0x77)); // /twothumbsdown
            Emotes.Add(new EmoteItem(0x250e0058, 0x78)); // /taunt
            Emotes.Add(new EmoteItem(0x1f0e0058, 0x79)); // /moveout
            Emotes.Add(new EmoteItem(0x1f0e0058, 0x7a)); // /move
            Emotes.Add(new EmoteItem(0x1d0e0058, 0x7b)); // /iamready
            Emotes.Add(new EmoteItem(0x1b0e0058, 0x7c)); // /rdy
            Emotes.Add(new EmoteItem(0x190e0058, 0x7d)); // /ready
            Emotes.Add(new EmoteItem(0x0d000080, 0x7e)); // /stop
            Emotes.Add(new EmoteItem(0x0d0e0058, 0x7f)); // /bigcheer
            Emotes.Add(new EmoteItem(0xd80200b4, 0x80)); // /whoa
            Emotes.Add(new EmoteItem(0xd5140058, 0x81)); // /talkrelieved
            Emotes.Add(new EmoteItem(0x17140058, 0x82)); // /talk1
            Emotes.Add(new EmoteItem(0x19140058, 0x83)); // /talk2
            Emotes.Add(new EmoteItem(0x1b140058, 0x84)); // /talk3
            Emotes.Add(new EmoteItem(0x1d140058, 0x85)); // /talkangry
            Emotes.Add(new EmoteItem(0x21140058, 0x86)); // /talkforceful
            Emotes.Add(new EmoteItem(0x1f140058, 0x87)); // /talkexcited
            Emotes.Add(new EmoteItem(0x23140058, 0x88)); // /talkscared
            Emotes.Add(new EmoteItem(0xd1140058, 0x89)); // /talkchuckle
            Emotes.Add(new EmoteItem(0xd3140058, 0x8a)); // /talkhurt
            Emotes.Add(new EmoteItem(0xd5140058, 0x8b)); // /talkrelieved
            Emotes.Add(new EmoteItem(0xe3150058, 0x8c)); // /talknegative
            Emotes.Add(new EmoteItem(0xdd150058, 0x8d)); // /talkpuzzled
            Emotes.Add(new EmoteItem(0xd9140058, 0x8e)); // /talkwhisperobvious
            Emotes.Add(new EmoteItem(0xdb150058, 0x8f)); // /talkgroup
            Emotes.Add(new EmoteItem(0xd7150058, 0x90)); // /talkflirtatious
            Emotes.Add(new EmoteItem(0xd9150058, 0x91)); // /talkaffirmative
            Emotes.Add(new EmoteItem(0xe1140058, 0x92)); // /overheat
            Emotes.Add(new EmoteItem(0xdd140058, 0x93)); // /thewave
            Emotes.Add(new EmoteItem(0xdb140058, 0x94)); // /snake
            Emotes.Add(new EmoteItem(0xdf140058, 0x95)); // /tsuj
            Emotes.Add(new EmoteItem(0x27140058, 0x96)); // /touchearpiece
            Emotes.Add(new EmoteItem(0xe1140058, 0x97)); // /overheat
            Emotes.Add(new EmoteItem(0x51160058, 0x98)); // /backflop
            Emotes.Add(new EmoteItem(0x55160058, 0x99)); // /backflop1
            Emotes.Add(new EmoteItem(0x53160058, 0x9a)); // /backflop2
            Emotes.Add(new EmoteItem(0x57160058, 0x9b)); // /ballet
            Emotes.Add(new EmoteItem(0x59160058, 0x9c)); // /bang
            Emotes.Add(new EmoteItem(0x5b160058, 0x9d)); // /cutitout
            Emotes.Add(new EmoteItem(0x5d160058, 0x9e)); // /giddyup
            Emotes.Add(new EmoteItem(0x5f160058, 0x9f)); // /horns
            Emotes.Add(new EmoteItem(0x61160058, 0xa0)); // /mimewall
            Emotes.Add(new EmoteItem(0x63160058, 0xa1)); // /mimeelbow
            Emotes.Add(new EmoteItem(0x67160058, 0xa2)); // /mimerope
            Emotes.Add(new EmoteItem(0x65160058, 0xa3)); // /picknose
            Emotes.Add(new EmoteItem(0x6b160058, 0xa4)); // /duh
            Emotes.Add(new EmoteItem(0x6d160058, 0xa5)); // /timeout
            Emotes.Add(new EmoteItem(0x6f160058, 0xa6)); // /whichway
            Emotes.Add(new EmoteItem(0x820200b4, 0xa9)); // /kickdoor
            Emotes.Add(new EmoteItem(0x710200b4, 0xaa)); // /examine
            Emotes.Add(new EmoteItem(0x8e0200b4, 0xac)); // /pickup
            Emotes.Add(new EmoteItem(0xb30200b4, 0xad)); // /takepill
            Emotes.Add(new EmoteItem(0x0b0000e0, 0xaf)); // /cough
            Emotes.Add(new EmoteItem(0xa10200b4, 0xb0)); // /righton
            Emotes.Add(new EmoteItem(0x0c0000e0, 0xb1)); // /sleep
            Emotes.Add(new EmoteItem(0xea020058, 0xb2)); // /tiphat
            Emotes.Add(new EmoteItem(0xa10000b4, 0xb3)); // /confused
            Emotes.Add(new EmoteItem(0x000d0058, 0xb4)); // /coverears
            Emotes.Add(new EmoteItem(0x760200b4, 0xb5)); // /eyedrops
            Emotes.Add(new EmoteItem(0x8e0200b4, 0xb6)); // /pickup
            Emotes.Add(new EmoteItem(0x0e050004, 0xba)); // /talkdepressed
            Emotes.Add(new EmoteItem(0x0d050004, 0xbb)); // /throw
            Emotes.Add(new EmoteItem(0xc50200b4, 0xbc)); // /toss
            Emotes.Add(new EmoteItem(0x4b00003a, 0xbe)); // /shakehands
            Emotes.Add(new EmoteItem(0xe4020058, 0xc1)); // /slap
            Emotes.Add(new EmoteItem(0xab0d0058, 0xc7)); // /dogsniff
            Emotes.Add(new EmoteItem(0xfd0d0058, 0xca)); // /hug
            Emotes.Add(new EmoteItem(0xca0d0058, 0xcd)); // /weddingkiss
            Emotes.Add(new EmoteItem(0xff0d0058, 0xd0)); // /holdbothhands
            Emotes.Add(new EmoteItem(0xfb0d0058, 0xd3)); // /kissthering
            Emotes.Add(new EmoteItem(0xf50d0058, 0xd6)); // /manhug
            Emotes.Add(new EmoteItem(0x050e0058, 0xd9)); // /pound
            Emotes.Add(new EmoteItem(0x030e0058, 0xdc)); // /weddingring
            Emotes.Add(new EmoteItem(0x010e0058, 0xdf)); // /propose
            Emotes.Add(new EmoteItem(0xc5140058, 0xe2)); // /dap
            Emotes.Add(new EmoteItem(0xfb0d0058, 0xe5)); // /kiss
            Emotes.Add(new EmoteItem(0x05160058, 0xe8)); // /weddingcake
            Emotes.Add(new EmoteItem(0x59160058, 0xee)); // /bangbang
            Emotes.Add(new EmoteItem(0x050000e0, 0xa8)); // /leanwallloop
            RebuildEmoteIndex();
        }

        private Dictionary<UInt32, byte> EnsureEmoteIndex()
        {
            if (Emotes == null) return null;
            Dictionary<UInt32, byte> index = emoteByLongId;
            if (index != null && Volatile.Read(ref emoteIndexCount) == Emotes.Count)
            {
                return index;
            }
            return RebuildEmoteIndex();
        }

        private Dictionary<UInt32, byte> RebuildEmoteIndex()
        {
            lock (CatalogIndexLock)
            {
                if (Emotes == null) return null;
                int count = Emotes.Count;
                var next = new Dictionary<UInt32, byte>(count);
                for (int i = 0; i < count; i++)
                {
                    EmoteItem item = Emotes[i];
                    if (item != null)
                    {
                        next.TryAdd(item.emoteIDLong, item.emoteShortID);
                    }
                }
                emoteByLongId = next;
                Volatile.Write(ref emoteIndexCount, count);
                return next;
            }
        }

        private Dictionary<UInt16, AbilityItem> EnsureAbilityIndex()
        {
            if (AbilityDB == null) return null;
            Dictionary<UInt16, AbilityItem> index = abilityById;
            if (index != null && Volatile.Read(ref abilityIndexCount) == AbilityDB.Count)
            {
                return index;
            }
            return RebuildAbilityIndex();
        }

        private Dictionary<UInt16, AbilityItem> RebuildAbilityIndex()
        {
            lock (CatalogIndexLock)
            {
                if (AbilityDB == null) return null;
                int count = AbilityDB.Count;
                var next = new Dictionary<UInt16, AbilityItem>(count);
                for (int i = 0; i < count; i++)
                {
                    AbilityItem item = AbilityDB[i];
                    if (item != null)
                    {
                        next.TryAdd(item.getAbilityID(), item);
                    }
                }
                abilityById = next;
                Volatile.Write(ref abilityIndexCount, count);
                return next;
            }
        }

        private Dictionary<int, GameObjectItem> EnsureGameObjectIndex()
        {
            if (GODB == null) return null;
            Dictionary<int, GameObjectItem> index = gameObjectById;
            if (index != null && Volatile.Read(ref gameObjectIndexCount) == GODB.Count)
            {
                return index;
            }
            return RebuildGameObjectIndex();
        }

        private Dictionary<int, GameObjectItem> RebuildGameObjectIndex()
        {
            lock (CatalogIndexLock)
            {
                if (GODB == null) return null;
                int count = GODB.Count;
                var next = new Dictionary<int, GameObjectItem>(count);
                for (int i = 0; i < count; i++)
                {
                    GameObjectItem item = GODB[i];
                    if (item != null)
                    {
                        next.TryAdd(item.getGOID(), item);
                    }
                }
                gameObjectById = next;
                Volatile.Write(ref gameObjectIndexCount, count);
                return next;
            }
        }

        private Dictionary<UInt32, ClothingItem> EnsureClothingIndex()
        {
            if (ClothingRSIDB == null) return null;
            Dictionary<UInt32, ClothingItem> index = clothingByGoid;
            if (index != null && Volatile.Read(ref clothingIndexCount) == ClothingRSIDB.Count)
            {
                return index;
            }
            return RebuildClothingIndex();
        }

        private Dictionary<UInt32, ClothingItem> RebuildClothingIndex()
        {
            lock (CatalogIndexLock)
            {
                if (ClothingRSIDB == null) return null;
                int count = ClothingRSIDB.Count;
                var next = new Dictionary<UInt32, ClothingItem>(count);
                for (int i = 0; i < count; i++)
                {
                    ClothingItem item = ClothingRSIDB[i];
                    if (item != null)
                    {
                        next.TryAdd(item.getGoidDecimal(), item);
                    }
                }
                clothingByGoid = next;
                Volatile.Write(ref clothingIndexCount, count);
                return next;
            }
        }

        public byte findEmoteByLongId(UInt32 _emoteLongId)
        {
            Dictionary<UInt32, byte> index = EnsureEmoteIndex();
            return index != null && index.TryGetValue(_emoteLongId, out byte emoteIdShort) ? emoteIdShort : (byte)0x00;
        }

        public List<StaticWorldObject> findObjectsBySectorWorldRangeAndType(float x, float z, uint metrId,
            UInt16 typeId)
        {
            IEnumerable<StaticWorldObject> staticWorldObjectsIenumerator = WorldObjectsDB.Where(w =>
                w.metrId == metrId && NumericalUtils.ByteArrayToUint16(w.type, 1) == typeId &&
                mathUtils.IsInCircle(x, z, (float) w.pos_x, (float) w.pos_z, 5000));
            List<StaticWorldObject> TempStaticWorldObjects = staticWorldObjectsIenumerator.ToList();
            return TempStaticWorldObjects;
        }

        public StaticWorldObject getObjectValues(UInt32 objectId, UInt16? district = null)
        {
            Output.WriteLine("REQUEST OBJECT WITH ID :" +
                             StringUtils.bytesToString_NS(NumericalUtils.uint32ToByteArray(objectId, 0)));
            StaticWorldObject worldObject = null;

            // Indexed lookup: this used to scan all ~2.8M static objects for every client request, and
            // the client sends dozens of these while a district streams in.
            worldObject = WorldObjectsById.Find(objectId, district);
            if (worldObject == null)
            {
                worldObject = new StaticWorldObject();
            }

            return worldObject;
        }

        public AbilityItem getAbilityByID(UInt16 id)
        {
            Dictionary<UInt16, AbilityItem> index = EnsureAbilityIndex();
            return index != null && index.TryGetValue(id, out AbilityItem abTemp) ? abTemp : null;
        }

        public GameObjectItem getGameObjectItemById(UInt16 id)
        {
            Dictionary<int, GameObjectItem> index = EnsureGameObjectIndex();
            return index != null && index.TryGetValue(id, out GameObjectItem goItem) ? goItem : null;
        }

        /// <summary>A row keyed by this static id wins; otherwise the merchant static resolved for a row
        /// (research/world-data/resolve_vendor_npcs.py), so using the drawn merchant opens its stock.</summary>
        internal static Vendor FindVendor(List<Vendor> vendors, UInt32 staticId, UInt16 metrId) =>
            vendors.Find(c => c.vendorStaticID == staticId && c.metrId == metrId) ??
            vendors.Find(c => staticId != 0 && c.npcStaticID == staticId && c.metrId == metrId);

        public Vendor getVendorByGoIDandMetrId(UInt32 GoID, UInt16 metrId)
        {
            Vendor theVendor = FindVendor(Vendors, GoID, metrId);

            return theVendor;
        }

        public ClothingItem getItemValues(UInt32 itemID)
        {
            Dictionary<UInt32, ClothingItem> index = EnsureClothingIndex();
            if (index != null && index.TryGetValue(itemID, out ClothingItem clothingTemp))
            {
                return clothingTemp;
            }
            return new ClothingItem();
        }

        // SingleTon Pattern - as we only want to load the Data once at startup and manage 

        private static readonly object InstanceLock = new object();

        /// <summary>
        /// Locked because the world threads start before Main gets here: the visibility thread asking for the instance
        /// while the first was still loading made a second one, and every NPC existed twice, one on top of the other
        /// (found by tools/HeadlessClient). Callers wait until the data has loaded.
        /// </summary>
        public static DataLoader getInstance()
        {
            DataLoader existing = Instance;
            if (existing != null)
            {
                return existing;
            }

            lock (InstanceLock)
            {
                if (Instance == null)
                {
                    Instance = new DataLoader();
                }

                return Instance;
            }
        }
    }
}