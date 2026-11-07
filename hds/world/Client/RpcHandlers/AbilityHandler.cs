using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using hds.shared;
using hds.world.Skill;

namespace hds
{
    public class AbilityHandler
    {
        public AbilityItem currentAbility;
        public UInt16 currentTargetViewId;
        public Timer damageTimer;
        public Timer hyperjumpTimer;
        private List<JumpStep> Steps = new List<JumpStep>();


        public void ProcessUpgradeAbility(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);
            UInt16 abilityId = reader.ReadUInt16(1);
            UInt16 unknownUint16 = reader.ReadUInt16(1);
            uint levelToIncrease = reader.ReadUint8();

            AbilityItem abilityItem = DataLoader.getInstance().getAbilityByID(abilityId);
            ushort currentLevel = Store.dbManager.WorldDbHandler.UpgradeAbilityLevel(abilityItem.getGOID(), levelToIncrease);

            ServerPackets packets = new ServerPackets();
            packets.SendUpgradeAbilityLevel(abilityId, currentLevel);

        }
        
        public void ProcessAbility(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);
            UInt16 AbilityID = reader.ReadUInt16(1);
            UInt16 currentTargetViewId = reader.ReadUInt16(1);


            // load the ability name from a list to see if we match the right ability
            DataLoader AbilityLoader = DataLoader.getInstance();
            currentAbility = AbilityLoader.getAbilityByID(AbilityID);

            // lets create a message for the client - we will later execute the right AbilityScript for it 
            ServerPackets pak = new ServerPackets();
            pak.sendSystemChatMessage(Store.currentClient,
                "Ability ID is " + AbilityID.ToString() + " and the name is " + currentAbility.getAbilityName() +
                " and Target ViewId Is " + currentTargetViewId, "BROADCAST");

            // ToDo: do something with the entity (or queue a fx hit animation or something lol)    
            ProcessAbilityScript(this.currentAbility);
        }

        public void ProcessAbilityScript(AbilityItem abilityItem)
        {
            // Display Cast Bar if it is necessary
            ServerPackets pak = new ServerPackets();
            if (this.currentAbility.getCastingTime() > 0)
            {
                pak.SendCastAbilityBar(currentAbility.getAbilityID(), currentAbility.getCastingTime());
                processSelfAnimation(abilityItem);
            }

            processCharacterAnimationSelf(abilityItem.getAbilityID());

            if (currentAbility.getAbilityID() == 12 || currentAbility.getAbilityID() == 184)
            {
                pak.SendHyperSpeed();
            }

            if (currentAbility.getAbilityID() == 970)
            {
                // Spawn Code Write Window
                pak.SendCodeWriterWindow();
            }

            pak.sendISCurrent(Store.currentClient, 50);
        }

        public void abilityAnimateTheTarget(object e)
        {
            ServerPackets pak = new ServerPackets();
            UInt32 targetAnim = this.currentAbility.getActivationFX();
            if (targetAnim == 0)
            {
                targetAnim = currentAbility.getAbilityExecutionFX();
            }

            pak.SendCastAbilityOnEntityId(currentTargetViewId, targetAnim, 150);
        }

        public void processCharacterAnimationSelf(UInt16 abilityID)
        {
            ServerPackets pak = new ServerPackets();
            // 2904 0429 = Hacker_VirusLaunch_A
            // 2a04 042a = Hacker_VirusLaunch_D
            // see movementAnims.tx - its for codes something (0x31)
            if (currentAbility.getAbilityExecutionFX() > 0)
            {
                pak.SendCastAbilityOnEntityId(2, currentAbility.getAbilityExecutionFX(), 200);
            }

            if (currentAbility.getCastingTime() > 0)
            {
                byte[] castAnimStart = currentAbility.getCastAnimStart();
                // Cast
                pak.sendSystemChatMessage(Store.currentClient,
                    "Animation Starts with Byte ID " + StringUtils.bytesToString(castAnimStart), "BROADCAST");
                pak.sendPlayerAnimation(Store.currentClient, StringUtils.bytesToString_NS(castAnimStart));

                // And Time a "Damage" or "Buff" Animation
                int castingTime = (int) currentAbility.getCastingTime() * 1000;
                currentTargetViewId = Store.currentClient.playerData.currentSelectedTargetViewId;
                damageTimer = new Timer(abilityAnimateTheTarget, this, castingTime, 0);
            }
        }


        public void processSelfAnimation(AbilityItem ability)
        {
//            ServerPackets serverPackets = new ServerPackets();
//            serverPackets.sendAbilitySelfAnimation(2, ability.getAbilityID(), (UInt32) ability.getAbilityExecutionFX());

            ServerPackets serverPackets = new ServerPackets();
            serverPackets.SendAbilitySelfAnimation(2, ability.getAbilityID(),
                NumericalUtils.ByteArrayToUint32(ability.getCastAnimStart(), 1));
        }

        public void ProcessJump(ref byte[] rpcData)
        {
            double xDest = 0;
            double yDest = 0;
            double zDest = 0;
            PacketReader reader = new PacketReader(rpcData);
            xDest = reader.ReadDouble(1);
            yDest = reader.ReadDouble(1);
            zDest = reader.ReadDouble(1);
            
            byte[] unknownJumpBytes = reader.ReadBytes(6);
            UInt32 peakHeight = reader.ReadUInt32(1);
            uint maybeFlag = reader.ReadUint8(); // is it everytime 01 ? maybe must check 
            
            UInt32 jumpId = reader.ReadUInt32(1); // JumpId

            // Players current X Z Y
            double x = 0;
            double y = 0;
            double z = 0;
            byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            int rotation = (int) Store.currentClient.playerInstance.YawInterval.getValue()[0];
            float xPos = (float) x;
            float yPos = (float) y;
            float zPos = (float) z;

            // ToDo: maybe remove or improve (i am not sure how this is written)
            //HyperJumpCalculatedStepMovement(xPos, yPos, zPos, xDest, yDest, zDest, clientJumpIdUnknown, maybeMaxHeight);

            // JumpEndTime is server sim time in milliseconds (retail: the 0x82 packet sim time x 1000 plus
            // ~9 s for a hyperjump). This used to be Unix seconds, decades ahead of the sim clock, so the
            // client stayed in the air for good and stopped sending movement.
            float peak = BitConverter.ToSingle(BitConverter.GetBytes(peakHeight), 0);
            UInt32 endTime = JumpEndTime(TimeUtils.GetSimTimeSeconds(), peak);

            ServerPackets packets = new ServerPackets();
            packets.SendHyperJumpID(jumpId);
            packets.SendHyperJumpWithOneStep((float)xDest,(float)yDest,(float)zDest, peakHeight, endTime);
            Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(xDest, yDest, zDest));
        }

        /// <summary>Gravity in world units (centimetres) per second squared.</summary>
        private const double Gravity = 981.0;

        /// <summary>
        /// Jump end time in sim milliseconds: up to the peak height (a float the client sends, world units)
        /// and back down, 2·sqrt(2h/g), at least 0.5 s. A retail hyperjump with peak 10300 lasted 9.9 s;
        /// this gives 9.2 s.
        /// </summary>
        public static UInt32 JumpEndTime(float simTimeSeconds, float peakHeight)
        {
            double h = Math.Max(0.0, double.IsFinite(peakHeight) ? peakHeight : 0.0);
            double seconds = Math.Max(0.5, 2.0 * Math.Sqrt(2.0 * h / Gravity));
            return (UInt32)Math.Round((simTimeSeconds + seconds) * 1000.0);
        }
        
        

        private void HyperJumpCalculatedStepMovement(float xPos, float yPos, float zPos, double xDest, double yDest,
            double zDest, uint clientJumpIdUnknown, float maybeMaxHeight)
        {
            LtVector3f[] JumpMovements = Maths.ParabolicMovement(new LtVector3f(xPos, yPos, zPos),
                new LtVector3f((float) xDest, (float) yDest, (float) zDest), 50, 128);

            float distance = Maths.getDistance(xPos, yPos, zPos, (float) xDest, (float) yDest, (float) zDest);
            UInt16 duration = (UInt16) (distance * 0.5f);

            UInt32 startTime = TimeUtils.getUnixTimeUint32();
            UInt32 endTime = startTime + duration;

            ServerPackets packets = new ServerPackets();
            packets.SendHyperJumpID(clientJumpIdUnknown);
            Store.currentClient.playerData.isJumping = true;
            Store.currentClient.playerData.incrementJumpID();
            UInt32 maybeTimeBasedValue = 40384248;
            foreach (LtVector3f currentJumpPos in JumpMovements)
            {
                Steps.Add(new JumpStep(currentJumpPos, new LtVector3f((float) xDest, (float) yDest, (float) zDest),
                    maybeMaxHeight, endTime, Store.currentClient.playerData.getJumpID(), maybeTimeBasedValue));
                //packets.SendHyperJumpStepUpdate(currentJumpPos, xDest, yDest, zDest, maybeMaxHeight, endTime);
                maybeTimeBasedValue = maybeTimeBasedValue + 100;
            }

            hyperjumpTimer = new Timer(ProcessJumpStep, this, 0, 0);
        }

        private void ProcessJumpStep(Object e)
        {
            JumpStep Step = Steps[0];
            Steps.RemoveAt(0);
            ServerPackets packets = new ServerPackets();

            bool isLastStep = Steps.Count == 0;

            packets.SendHyperJumpStepUpdate(Step.FromPos, Step.ToPos.x, Step.ToPos.y, Step.ToPos.z, Step.JumpHeight,
                Step.endTime, Step.jumpId, Step.maybeTimeBasedValue, isLastStep);

            if (Steps.Count > 0)
            {
                hyperjumpTimer = new Timer(ProcessJumpStep, this, 50, 0);
            }
            else
            {
                Store.currentClient.playerData.isJumping = false;
                // ToDo: Check if we need to send some Message for the Clientview
            }    
        }

        public void processHyperJumpCancel(ref byte[] rpcData)
        {
            double xDest = 0;
            double yDest = 0;
            double zDest = 0;
            PacketReader reader = new PacketReader(rpcData);
            xDest = reader.ReadDouble(1);
            yDest = reader.ReadDouble(1);
            zDest = reader.ReadDouble(1);

            // ToDo: figure out what this 6 bytes are could be
            // Skip 6 bytes as we currently didnt knew
            UInt32 clientJumpIdUnknown = reader.ReadUInt32(1);

            // Players current X Z Y
            double x = 0;
            double y = 0;
            double z = 0;
            byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            int rotation = (int) Store.currentClient.playerInstance.YawInterval.getValue()[0];
            float xPos = (float) x;
            float yPos = (float) y;
            float zPos = (float) z;

            // Start/end are server sim time in ms, like JumpEndTime (these used to be Unix seconds). The fall to
            // the new destination is timed as a drop from the current height above it.
            float simSeconds = TimeUtils.GetSimTimeSeconds();
            UInt32 startTime = (UInt32)Math.Round(simSeconds * 1000.0);
            // A straight drop of H takes sqrt(2H/g), which is JumpEndTime's up-and-down time for a peak of H/4.
            UInt32 endTime = JumpEndTime(simSeconds, (float)Math.Max(0.0, yPos - yDest) / 4f);

            ServerPackets packets = new ServerPackets();
            packets.SendHyperJumpID(clientJumpIdUnknown);
            packets.SendHyperJumpUpdate(xPos, yPos, zPos, (float) xDest, (float) yDest, (float) zDest, startTime,
                endTime);
            Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(xDest, yDest, zDest));
        }
    }
}