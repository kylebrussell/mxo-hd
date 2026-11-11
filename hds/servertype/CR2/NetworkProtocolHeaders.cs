using System;
//namespace hds.servertype.cr2
namespace hds
{

    // This list is the client REQUEST rpc headers
    public enum RPCRequestHeader
    {
        CLIENT_SPAWN_READY = 0x05,
        CLIENT_CHAT = 0x28,
        CLIENT_WHISPER = 0x29,
        CLIENT_COMBAT_STANCE = 0x3d, // 01 = draw weapon / ready, 00 = stand down
        CLIENT_CLOSE_COMBAT = 0x40,
        CLIENT_RANGE_COMBAT = 0x41,
        CLIENT_FREE_FIRE_CHOICE = 0x3f, // 3f <inventory slot u16> <kind u32>: hotbar Free Fire weapon (kind 0x10 = item)
        CLIENT_LEAVE_COMBAT = 0x44,
        CLIENT_DUEL_REQUEST = 0x50,            // 50 <target handle u32>: /duel on the target; 0 = /duel stop
        CLIENT_DUEL_RESPONSE = 0x52,           // 52 <challenger handle u32> <accepts u8> <reason u8>
        CLIENT_PVP_FLAG = 0x19e,               // 81 9e (no body): /pvp
        CLIENT_JUMP_START = 0xc2,
        CLIENT_JUMP_CANCEL = 0xc4,
        CLIENT_OBJECTINTERACTION_DYNAMIC = 0xc7,
        CLIENT_OBJECTINTERACTION_STATIC = 0xc8,
        CLIENT_REGION_LOADED = 0xc9,   
        CLIENT_TARGET = 0x151,
        CLIENT_MISSION_INVITE_PLAYER = 0x8c,
        CLIENT_MISSION_REQUEST = 0x94,
        CLIENT_MISSION_INFO = 0x98,
        CLIENT_MISSION_ACCEPT = 0x9b,
        CLIENT_MISSION_ABORT = 0xa6,
        CLIENT_ELEVATOR_FLOOR = 0x140,  // 81 40 <button u16>: the floor picked on an elevator panel, from the bottom
        CLIENT_VIEW_TOGGLED = 0x104,    // 81 04 <view u16> <token u32>: a panel the tutorial showed was used
        CLIENT_SHORTCUT_ADDED = 0x18d,  // 81 8d <type u32> <inventory slot u32> <class data u32> <instance data u32>
        CLIENT_MODAL_ANSWER = 0x190,    // 81 90 <message u32> <left chosen u8>
        CLIENT_MISSION_EVENT = 0x191,   // 81 91 <event u32>: an FX token coming back
        CLIENT_LEAVE_GROUP = 0x71,
        CLIENT_ABILITY_HANDLER = 0xB9,
        CLIENT_CHANGE_CT = 0x42,

        // Hardline
        CLIENT_HARDLINE_STATUS_REQUEST = 0x6c,
        CLIENT_HARDLINE_EXIT_LA_CONFIRM = 0xfc,
        CLIENT_EXIT_GAME_FINISH = 0xfe,          // 80 fe <mode>: 00 = quit, 01 = go to the Loading Area after death
        CLIENT_LA_SELECT_HARDLINE = 0xff,        // 80 ff <district u32> <hardline u32>: leave the Loading Area
        CLIENT_RESPAWN_REQUEST = 0x14c,          // 81 4c (no body): the dead player asks to respawn

        CLIENT_HANDLE_MISSION_INVITE = 0x6f,
        CLIENT_TEAM_LOOT_MODE = 0x90, // 80 90 <mode 1..4>; retail echoed it to the team

        // Ability Handlers
        CLIENT_ABILITY_LOADER = 0xae,
        CLIENT_UPGRADE_ABILITY_LEVEL = 0xb7,
        CLIENT_UPGRADE_ATTRIBUTE = 0xb5,
        CLIENT_DISABLE_BUFF = 0xB6,

        CLIENT_LOOT_ALL = 0x117,
        CLIENT_LOOT_ACCEPT = 0x11f,

        // Inventory
        CLIENT_USE_MISSION_TICKET = 0x59, // 59 <slot u8>: native GoldenTicket item use
        CLIENT_ITEM_MOUNT_RSI = 0x63,
        CLIENT_ITEM_UNMOUNT_RSI = 0x64,
        CLIENT_ITEM_MOVE_SLOT = 0x65,
        CLIENT_ITEM_RECYCLE = 0x5D,
        CLIENT_VENDOR_BUY = 0x10e,
        CLIENT_VENDOR_SELL = 0x111,
        CLIENT_COLLECTOR_EXCHANGE = 0x116,
        CLIENT_TRADE_REQUEST = 0x182,
        CLIENT_TRADE_ACTION = 0x185,
        
        // BuddyList
        CLIENT_ADD_FRIEND = 0xd6,
        CLIENT_REMOVE_FRIEND = 0xda,

        // MarketPlace
        CLIENT_MP_LIST_ITEMS = 0x124,
        CLIENT_MP_OPEN = 0x121,
        CLIENT_MP_OPEN_WINDOW = 0x129, // 81 29 00 00 00 00 01: retail's marketplace open
        CLIENT_MP_BUY = 0x126,          // 81 26 <listing id> <slot>
        CLIENT_MP_PLACE_ITEM = 0x12b,   // 81 2b <category> <price> <container> <slot> 00 00 00 00

        //Commands

        CLIENT_CMD_WHEREAMI = 0x154,
        CLIENT_CMD_WHO = 0x152, // ToDo: implement
        CLIENT_PLAYER_GET_DETAILS = 0x192, // ToDo: implement
        CLIENT_PLAYER_GET_BACKGROUND = 0x194, // ToDo: implement
        CLIENT_PLAYER_SET_BACKGROUND = 0x196,

        // Char Emotes and things
        CLIENT_CHANGE_MOOD = 0x35,

        // Teleport
        CLIENT_TELEPORT_HL = 0x18e,

        // Reset Client RPC
        CLIENT_READY_WORLDCHANGE = 0x108,

        // Currently unhandled but maybe useful
        // http://code.google.com/p/mxo-singularity/wiki/RpcPacketMap
        CLIENT_EMOTE = 0x30,
        CLIENT_ANIMATION_START = 0x33,
        CLIENT_ANIMATION_STOP = 0x34,

        // Login handshake (docs/CLIENT-ACTIONS.md): sent together once the client is in the world
        CLIENT_LOADING_COMPLETE = 0x0c,        // 0c 01: LoadingComplete; retail answered nothing
        CLIENT_FEATURES_REQUEST = 0x148,       // 81 48 (no body): retail answered 81 49, the feature list

        // Faction and crew
        CLIENT_FACTION_INFO = 0xf4,
        CLIENT_FACTION_INVITE = 0x7a,
        CLIENT_FACTION_DISBAND_FACTION = 0x77,
        CLIENT_DEPOSIT_MONEY_FACTION_CREW = 0x78,
        CLIENT_FACTION_CREW_INVITE_PLAYER = 0x6e,
        
    }

    public enum RPCResponseHeaders
    {
        // Server Features
        SERVER_FEATURE_EVENT = 0x3a05,
        
        // World
        SERVER_LOAD_WORLD_CMD = 0x06,
        SERVER_PLAYER_JUMP_ID = 0xc3,
        SERVER_LOAD_RPC_RESET = 0x8107,
        SERVER_LOAD_WORLD = 0x8108,
        SERVER_FLASH_TRAFFIC = 0x81a9,

        // Enviroment
        SERVER_SUBWAY_INTERACTION = 0x813a,
        SERVER_ELEVATOR_PANEL = 0x813f,
        SERVER_SUBWAY_MAP = 0x813e,

        // Player
        SERVER_PLAYER_ATTRIBUTE = 0x80b2, 
        SERVER_PLAYER_UPGRADE_ABILITY = 0x80b8,
        SERVER_PLAYER_UPGRADE_ATTRIBUTE = 0x80b6,
        SERVER_MANAGE_BONUS = 0xbc,
        
        // Coder Stuff
        SERVER_CODER_ATTRIBUTE_UNKNOWN = 0x80bd,
        SERVER_CODER_UNKNOWN = 0x8178,
        
        SERVER_EXIT_HL = 0x80fb,
        SERVER_HARDLINE_STATUS = 0x6d, // answers CLIENT_HARDLINE_STATUS_REQUEST (0x6c): synced hardlines of a district
        SERVER_JACKOUT_FINISH = 0x80fd,
        SERVER_PLAYER_EXP   = 0x80e5,
        SERVER_PLAYER_EXP_ANIM   = 0x80e6,
        SERVER_PLAYER_INFO  = 0x80e4,
        SERVER_PLAYER_GET_DETAILS = 0x8193,
        SERVER_PLAYER_GET_BACKGROUND = 0x8195,
        SERVER_PLAYER_HANDLE_BACKGROUND = 0x8198,

        // Friendlist
        SERVER_FRIENDLIST_STATUS_ADD = 0x80D7,
        SERVER_FRIENDLIST_STATUS_DELETE = 0x80DB,
        SERVER_FRIEND_ONLINE = 0x80de,
        SERVER_FRIEND_OFFLINE = 0x80df,

        // Mission related
        SERVER_MISSION_RESPONSE_LIST    = 0x8095,
        SERVER_MISSION_RESPONSE_NAME    = 0x8096,
        SERVER_MISSION_LIST_END = 0x8097,
        SERVER_MISSION_SET_OBJECTIVE = 0x80a0,
        SERVER_MISSION_SET_NAME = 0x809c,
        SERVER_MISSION_INFO_RESPONSE = 0x8099,
        SERVER_MISSION_LOCATION_POSITION = 0x809E,
        SERVER_LOOT_WINDOW_RESPONSE = 0x8119,
        SERVER_LOOT_ACCEPTED_RESPONSE = 0x8120,
        SERVER_TEAM_CREATE        = 0x808d,
        SERVER_TEAM_INVITE_MEMBER = 0x808f,

        // Abilitys
        SERVER_CAST_BAR_ABILITY = 0x80ac,
        SERVER_ABILITY_LOAD = 0x80b2,
        SERVER_ABILITY_UNLOAD = 0x80b3,
        SERVER_HYPERJUMP_ID = 0xc3,

        // Chat & Commands
        SERVER_CHAT_MESSAGE_RESPONSE = 0x2e,
        SERVER_CHAT_WHEREAMI_RESPONSE = 0x8154, // CR2: 0x8154
        SERVER_CHAT_WHO_RESPONSE = 0x8153,

        // Marketplace
        SERVER_LOAD_MARKERPLACE = 0x8125,
        SERVER_VENDOR_OPEN = 0x810d,

        // Crew & Faction
        SERVER_FACTION_NAME_RESPONSE = 0x80f5,
        SERVER_CREW_MEMBERS_LIST = 0x8086,
        SERVER_CREW_INVITE = 0x8088,
        SERVER_JOIN_GROUP = 0x70,
        SERVER_LEAVE_GROUP = 0x72,
        SERVER_FACTION_CREATE_ERROR = 0x7b,
        SERVER_FACTION_PLAYER_INFO = 0x7c,
        SERVER_FACTION_UPDATE_MONEY = 0x79

    }

    public enum ProtocolHeaders
    {
        OBJECT_VIEW_PROTOCOL = 0x03,
        RPC_PROTOCOL = 0x04,
        MPM_05_PROTOCOL = 0x05

    }
}

