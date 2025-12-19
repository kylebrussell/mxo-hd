using System;
using System.Collections.Generic;
using System.Text;
using hds.auth;
using hds.databases;
using hds.world.scripting;

namespace hds.shared{
    public class Store{

        /* Configuration */

        public static ServerConfig config { get; set; }
        public static WorldConfig worldConfig { get; set; }

        /* Servers */
        public static AuthServer auth {get;set;}
        public static MarginServer margin {get;set;}
        public static WorldServer world {get;set;}

        /* Threading */
        public static WorldThreads worldThreads { get; set; }

        /* Database Handling */
        public static DatabaseManager dbManager { get; set; }

        /* Protocol Handling */
        [ThreadStatic]
        private static WorldClient threadCurrentClient;
        public static WorldClient currentClient { get { return threadCurrentClient; } set { threadCurrentClient = value; } }

        /* Scripting Handling */

        public static ScriptManager rpcScriptManager { get; set; }

    }
}
