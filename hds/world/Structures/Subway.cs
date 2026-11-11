using System;
using System.Timers;
using hds.shared;


namespace hds.world.Structures
{
    public class Subway
    {
        public StaticWorldObject worldObject;
        public Object6568 gameObjectData;
        
        public static int secondsClosingDoors = 3;
        public static int secondsDriveOut = 5;
        public static int secondsStayOut = 3;
        public static int secondsDriveIn = 5;
        public static int secondsOpenDoors = 3;
        public static int secondsWaitForPassengers = 30; // Change to 60 later
        public int secondsLeft = 60;
        public SUBWAY_STATE currentState = SUBWAY_STATE.IDLE_CLOSE_DOORS;

        public enum SUBWAY_STATE
        {
            DRIVE_IN,
            STAY,
            IDLE_CLOSE_DOORS,
            IDLE_OPEN_DOORS,
            ACTIVATE_INTERACTION,
            DRIVE_OUT,
        }

        public byte stateByte;

        public Subway(StaticWorldObject worldObject)
        {
            this.worldObject = worldObject;
            gameObjectData = new Object6568();
            gameObjectData.SetRelatedStaticObjId(worldObject.mxoStaticId);
            gameObjectData.DisableAllAttributes();
            gameObjectData.Orientation.enable();
            gameObjectData.Position.enable();
            gameObjectData.CurrentState.enable();
            // Set Values
            gameObjectData.Position.setValue(NumericalUtils.doublesToLtVector3d(worldObject.pos_x,
                worldObject.pos_y, worldObject.pos_z));
            gameObjectData.Orientation.setValue(StringUtils.hexStringToBytes(worldObject.quat));
            gameObjectData.CurrentState.setValue(0x0b000000);
            worldObject.pos_y = worldObject.pos_x + 71.45;
        }

        public void StartCountdown()
        {
            Timer myTimer = new Timer(1000);
            myTimer.AutoReset = true; // the key is here so it repeats
            myTimer.Elapsed += DecreaseSeconds;
            myTimer.Interval = 1000; // 1000 ms is one second
            myTimer.Start();
            UpdateSubwayState();
        }

        public void DecreaseSeconds(Object source, ElapsedEventArgs e)
        {
            secondsLeft--;
            if (secondsLeft <= 0)
            {
                byte currentStateByte = stateByte;
                switch (currentState)
                {
                    case SUBWAY_STATE.ACTIVATE_INTERACTION:
                        stateByte = 0xbd;
                        secondsLeft = secondsClosingDoors;
                        currentState = SUBWAY_STATE.IDLE_CLOSE_DOORS;
                        if (currentStateByte != stateByte)
                        {
                            UpdateSubwayState();    
                        }
                        break;
                    case SUBWAY_STATE.IDLE_CLOSE_DOORS:
                        stateByte = 0xbe;
                        secondsLeft = secondsDriveOut;
                        currentState = SUBWAY_STATE.DRIVE_OUT;
                        UpdateSubwayState();
                        if (currentStateByte != stateByte)
                        {
                            UpdateSubwayState();    
                        }
                        break;
                    case SUBWAY_STATE.DRIVE_OUT:
                        stateByte = 0xb9;
                        secondsLeft = secondsStayOut;
                        currentState = SUBWAY_STATE.STAY;
                        if (currentStateByte != stateByte)
                        {
                            UpdateSubwayState();    
                        }
                        break;
                    case SUBWAY_STATE.STAY:
                        stateByte = 0xba;
                        secondsLeft = secondsDriveIn;
                        currentState = SUBWAY_STATE.DRIVE_IN;
                        if (currentStateByte != stateByte)
                        {
                            UpdateSubwayState();    
                        }
                        break;
                    case SUBWAY_STATE.DRIVE_IN:
                        stateByte = 0xbb;
                        currentState = SUBWAY_STATE.IDLE_OPEN_DOORS;
                        secondsLeft = secondsOpenDoors;
                        if (currentStateByte != stateByte)
                        {
                            UpdateSubwayState();    
                        }
                        break;
                    case SUBWAY_STATE.IDLE_OPEN_DOORS:
                        stateByte = 0xbc;
                        currentState = SUBWAY_STATE.ACTIVATE_INTERACTION;
                        secondsLeft = secondsWaitForPassengers;
                        if (currentStateByte != stateByte)
                        {
                            UpdateSubwayState();    
                        }
                        break;

                }
            }
        }

        /// <summary>
        /// The view manager's key for a subway car. Views are found by entity id alone, and NPCs' ids are small numbers, so
        /// the old "metrId followed by mxoStaticId" string (Moriah Projects' car was 18) matched NPC 18: the NPC pass deleted
        /// the car's view as an out-of-range NPC and the subway pass made it again, twice a second. The top bits keep subway
        /// ids apart from NPCs, players and the other statics.
        /// </summary>
        public static UInt64 EntityId(StaticWorldObject car) => 0x5000000000000000UL | ((UInt64)car.metrId << 32) | car.mxoStaticId;

        public void UpdateSubwayState()
        {
            var pak = new PacketContent();
            pak.AddByte(0x01);
            pak.AddByte(0x02);
            pak.AddByte(stateByte);
            pak.AddByteArray(new byte[] {0x0b, 0x00, 0x00, 0x00, 0x00});
            // 02 03 03 00 01 02 be 0b 00 00 00 00
            // The state is for the debug log only: it used to go to every player in range as system chat
            Output.WriteDebugLog("[SUBWAY] Car " + worldObject.mxoStaticId + " in district " + worldObject.metrId + " now " + currentState);
            Store.world.SendViewUpdateToClientWhoHasStaticObjectSpawned(pak, worldObject, EntityId(worldObject));
        }
    }
}