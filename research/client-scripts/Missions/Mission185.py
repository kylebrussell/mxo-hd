# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\Missions\Mission185.py
# Compiled at: 2006-03-30 16:29:37
from Mission import Mission
from re import split
from ltfxmap import *
import types, stringtable_client as StringTable

class Mission185(Mission):
    __module__ = __name__

    def OnStartOfMission(self, teamids):
        teamleader = teamids[0]
        self.ComputeZoneIDToRoom(teamleader)
        return

    def OnStartOfStage(self, phasenumber, stagenumber, teamids):
        if phasenumber != 0:
            return
        teamleader = teamids[0]
        if stagenumber == 0:
            mission.addTempAbility(teamleader, 460, 1)
            mission.addTempAbility(teamleader, 52, 100)
            mission.addTempAbility(teamleader, 151, 100)
            mission.addTempAbility(teamleader, 152, 100)
            mission.addTempAbility(teamleader, 142, 100)
            mission.sealDoor(teamleader, ENTER_ENTER_DOJO)
            mission.sealDoor(teamleader, DOJO_ENTER_DOJO)
            mission.sealDoor(teamleader, SERVER_DOJO_SERVER)
            mission.sealDoor(teamleader, DOJO_DOJO_SERVER)
            mission.sealDoor(teamleader, SERVER_SERVER_EXIT)
            mission.sealDoor(teamleader, EXIT_SERVER_EXIT)
            mission.sendModalMessage(teamleader, 10001, 'D3wd, red pill or blue?', 0, StringTable.ID_OBJ_MISSION_REDPILL, StringTable.ID_OBJ_MISSION_BLUEPILL)
            mission.playNarration(teamleader, START_DIALOG)
        if stagenumber == 1:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_CAMERA_KEYS, 671091620)
            mission.playNarration(teamleader, CAMERA_DIALOG)
        if stagenumber == 2:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_MOVEMENT_KEYS, 671091621)
            mission.playNarration(teamleader, ARROW_DIALOG)
        if stagenumber == 3:
            mission.toggleView(teamleader, PANEL_COMPASS, 1)
            mission.sendModalMessage(teamleader, 1003, StringTable.ID_TUTORIAL_INITIAL_COMPASS, 0)
            mission.playNarration(teamleader, COMPASS_DIALOG)
        if stagenumber == 4:
            mission.toggleView(teamleader, PANEL_TARGET, 1)
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_DOUBLE_CLICK, 0)
            mission.scheduleEvent(teamleader, 60, CELLPHONE_GOAL)
            mission.playNarration(teamleader, TARGET_DIALOG)
        if stagenumber == 5:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_RIGHT_CLICK, 0)
            mission.scheduleEvent(teamleader, 60, TALKING_GOAL)
            mission.playNarration(teamleader, RADIAL_DIALOG)
        if stagenumber == 6:
            mission.sendModalMessage(teamleader, 1006, StringTable.ID_TUTORIAL_INITIAL_CHAT_WINDOW, 0)
            mission.playNarration(teamleader, INTERACTION_DIALOG)
        if stagenumber == 7:
            mission.toggleView(teamleader, PANEL_INVENTORY, 1)
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_INVENTORY_BUTTON, 0)
            mission.scheduleEvent(teamleader, 60, INVENTORY_GOAL)
            mission.playNarration(teamleader, INVENTORY_DIALOG)
        if stagenumber == 8:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_EQUIP_ITEMS, 0)
            mission.playNarration(teamleader, ITEM_DIALOG)
        if stagenumber == 9:
            mission.toggleView(teamleader, PANEL_ACTIONS, 1)
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_ACTIONS_BUTTON, 0)
            mission.scheduleEvent(teamleader, 60, ABILITIES_GOAL)
        if stagenumber == 10:
            mission.toggleView(teamleader, PANEL_TOOLBAR, 1)
            mission.sendModalMessage(teamleader, 1010, StringTable.ID_TUTORIAL_INITIAL_HOTBAR, 0)
            mission.playNarration(teamleader, ACTION_DIALOG)
        if stagenumber == 11:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_HOTBAR_ABILITIES, 0)
            mission.playNarration(teamleader, ABILITY_DIALOG)
        if stagenumber == 12:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_MAP, 0)
            mission.toggleView(teamleader, PANEL_MAP, 1)
            mission.scheduleEvent(teamleader, 60, MAP_GOAL)
        if stagenumber == 13:
            mission.unlockDoor(teamleader, ENTER_ENTER_DOJO)
            mission.unlockDoor(teamleader, DOJO_ENTER_DOJO)
            mission.setExclusiveWaypoint(teamleader, ENTER_ENTER_DOJO)
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_COMBAT_START, 0)
            mission.scheduleEvent(teamleader, 60, DOJO_GOAL)
            mission.playNarration(teamleader, GOTODOJO_DIALOG)
        if stagenumber == 14:
            mission.sendModalMessage(teamleader, 1014, StringTable.ID_TUTORIAL_INITIAL_COMBAT_INTRODUCTION, 0)
            mission.playNarration(teamleader, GOTODOJO_SUCCESS)
        if stagenumber == 15:
            mission.sendModalMessage(teamleader, 1015, StringTable.ID_TUTORIAL_INITIAL_COMBAT_TACTICS, 0)
        if stagenumber == 16:
            mission.sendModalMessage(teamleader, 1016, StringTable.ID_TUTORIAL_INITIAL_COMBAT_TACTICS_SPEED, 0)
        if stagenumber == 17:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_COMBAT_GUARDPROGRAM, 0)
            mission.playNarration(teamleader, AIKIDOSTART_DIALOG)
            mission.scheduleEvent(teamleader, 60, INTERLOCK_GOAL)
        if stagenumber == 18:
            pass
        if stagenumber == 19:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_COMBAT_VIRUS, 0)
            mission.scheduleEvent(teamleader, 180, VIRUS_GOAL)
            mission.addTempAbility(teamleader, 45, 100)
            mission.addTempAbility(teamleader, 57, 100)
        if stagenumber == 20:
            mission.unlockDoor(teamleader, DOJO_DOJO_SERVER)
            mission.unlockDoor(teamleader, SERVER_DOJO_SERVER)
            mission.setExclusiveWaypoint(teamleader, DOJO_DOJO_SERVER)
            mission.killNPC(teamleader, 'Firearm Construct')
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_COMBAT_END, 0)
            mission.playNarration(teamleader, GOTOHACKER_DIALOG)
        if stagenumber == 21:
            mission.sendModalMessage(teamleader, 1021, StringTable.ID_TUTORIAL_INITIAL_HACKING_START, 0)
            mission.playNarration(teamleader, GOTOHACKER_SUCCESS)
        if stagenumber == 22:
            mission.scheduleEvent(teamleader, 30, HACKING_GOAL)
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_HACKING_INTRODUCTION, 0)
            mission.playNarration(teamleader, HACKCOMPUTER_DIALOG)
        if stagenumber == 23:
            mission.sendModalMessage(teamleader, 1023, StringTable.ID_TUTORIAL_INITIAL_HACKING_EXPLANATION, 0)
            mission.playNarration(teamleader, HACKCOMPUTER_SUCCESS)
        if stagenumber == 24:
            mission.sendModalMessage(teamleader, 9999, StringTable.ID_TUTORIAL_INITIAL_HACKING_END, 0)
            mission.unlockDoor(teamleader, SERVER_SERVER_EXIT)
            mission.unlockDoor(teamleader, EXIT_SERVER_EXIT)
            mission.setExclusiveWaypoint(teamleader, SERVER_SERVER_EXIT)
            mission.playNarration(teamleader, LOCKPICK_SUCCESS)
        if stagenumber == 25:
            mission.sendModalMessage(teamleader, 1025, StringTable.ID_TUTORIAL_INITIAL_HARDLINE_UPLOAD, 0)
            mission.playNarration(teamleader, HARDLINE_DIALOG1)
        if stagenumber == 26:
            mission.sendModalMessage(teamleader, 1026, StringTable.ID_TUTORIAL_INITIAL_HARDLINE_EXIT, 0)
            mission.playNarration(teamleader, HARDLINE_DIALOG3)
        if stagenumber == 27:
            mission.toggleView(teamleader, PANEL_MISSION, 1)
            mission.sendModalMessage(teamleader, 1027, StringTable.ID_TUTORIAL_INITIAL_MISSION_TAB, 0)
            mission.scheduleEvent(teamleader, 120, LEAVING_GOAL)
        return

    def OnToggledViewActivated(self, phasenumber, teamleader, viewid):
        if phasenumber != 0:
            return
        if viewid == PANEL_INVENTORY:
            mission.completeGoal(teamleader, INVENTORY_GOAL)
        if viewid == PANEL_ACTIONS:
            mission.completeGoal(teamleader, ABILITIES_GOAL)
        if viewid == PANEL_MAP:
            mission.completeGoal(teamleader, MAP_GOAL)
        return

    def OnModalDialogueAccepted(self, phasenumber, teamleader, messageid, leftchosen):
        if phasenumber != 0:
            return
        if messageid == 9999:
            return
        if messageid > 10000:
            if messageid == 10001:
                if leftchosen:
                    mission.sendModalMessage(teamleader, 1000, StringTable.ID_TUTORIAL_INITIAL_INTRODUCTION, 0)
                else:
                    mission.sendModalMessage(teamleader, 10002, 'Take the blue pill and exit the matrix?', 0, StringTable.ID_YES, StringTable.ID_NO)
            if messageid == 10002:
                if leftchosen:
                    mission.bootPlayer(teamleader)
                else:
                    mission.teleportToMissionElement(teamleader, DOJO_ENTER_DOJO)
                    mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION)
                    mission.sendModalMessage(teamleader, 1000, StringTable.ID_TUTORIAL_INITIAL_INTRODUCTION, 0)
            return
        if messageid < 1000:
            mission.activateStage(teamleader, phasenumber, messageid)
        else:
            messageid = messageid - 1000
            mission.completeGoal(teamleader, messageid)
        return

    def OnMissionEvent(self, phasenumber, eventid, teamids):
        if phasenumber != 0:
            return
        teamleader = teamids[0]
        if eventid == LEAVING_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_LEAVING, 0)
            mission.scheduleEvent(teamleader, 60, LEAVING_GOAL)
            mission.playNarration(teamleader, HARDLINE_FAILURE)
        if mission.isGoalComplete(teamleader, eventid):
            return
        if eventid == INVENTORY_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_INVENTORY_REMINDER, 0)
            mission.scheduleEvent(teamleader, 60, INVENTORY_GOAL)
            mission.playNarration(teamleader, INVENTORY_FAILURE)
            return
        if eventid == ABILITIES_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_ABILITIES_REMINDER, 0)
            mission.scheduleEvent(teamleader, 60, ABILITIES_GOAL)
            mission.playNarration(teamleader, ABILITY_FAILURE)
            return
        if eventid == MAP_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_MAP, 0)
            mission.scheduleEvent(teamleader, 60, MAP_GOAL)
        if eventid == DOJO_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_DOJO, 0)
            mission.scheduleEvent(teamleader, 60, DOJO_GOAL)
            mission.playNarration(teamleader, GOTODOJO_FAILURE)
            return
        if eventid == HACKING_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_HACKING, 0)
            mission.scheduleEvent(teamleader, 30, HACKING_GOAL)
            mission.playNarration(teamleader, HACKCOMPUTER_FAILURE)
            return
        if eventid == CELLPHONE_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_CELLPHONE, 0)
            mission.scheduleEvent(teamleader, 60, CELLPHONE_GOAL)
            mission.playNarration(teamleader, TARGET_FAILURE)
            return
        if eventid == TALKING_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_TALKING, 0)
            mission.scheduleEvent(teamleader, 60, TALKING_GOAL)
            mission.playNarration(teamleader, RADIAL_FAILURE)
            return
        if eventid == INTERLOCK_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_INTERLOCK, 0)
            mission.scheduleEvent(teamleader, 60, INTERLOCK_GOAL)
            mission.playNarration(teamleader, AIKIDO_FAILURE)
            return
        if eventid == VIRUS_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_INITIAL_CONDITIONAL_VIRUS, 0)
            mission.scheduleEvent(teamleader, 120, VIRUS_GOAL)
            return
        return

    def OnObjectTargeted(self, phasenumber, teamleader, itemname):
        return

    def OnPlayerEnterZone(self, phasenumber, teamleader, zoneid):
        room = self.GetRoomFromZone(zoneid)
        waypointDoor = None
        if room == ENTER_DOJO:
            waypointDoor = DOJO_ENTER_DOJO
        elif room == DOJO_SERVER:
            waypointDoor = SERVER_DOJO_SERVER
        elif room == SERVER_EXIT:
            waypointDoor = EXIT_SERVER_EXIT
        elif room == ENTER and mission.isGoalComplete(teamleader, DOJO_GOAL):
            waypointDoor = ENTER_ENTER_DOJO
        elif room == DOJO and mission.isGoalComplete(teamleader, VIRUS_GOAL):
            waypointDoor = DOJO_DOJO_SERVER
        elif room == SERVER and mission.isGoalComplete(teamleader, HACKING_GOAL):
            waypointDoor = SERVER_SERVER_EXIT
        if waypointDoor != None:
            mission.setExclusiveWaypoint(teamleader, waypointDoor)
        return

    def GetRoomFromZone(self, Zone):
        if self.ZoneToRoom.has_key(Zone):
            return self.ZoneToRoom[Zone]
        return

    def ComputeZoneIDToRoom(self, teamleader):
        for doorName in Doors:
            try:
                zoneIDs = mission.getDoorZoneIDs(teamleader, doorName)
                for zone in zoneIDs:
                    if self.ZoneToRoom.has_key(zone):
                        for DoorRoom in split(' -- ', doorName):
                            if DoorRoom == self.ZoneToRoom[zone][0]:
                                self.ZoneToRoom[zone] = DoorRoom
                            elif DoorRoom == self.ZoneToRoom[zone][1]:
                                self.ZoneToRoom[zone] = DoorRoom

                    else:
                        self.ZoneToRoom[zone] = split(' -- ', doorName)

            except:
                pass

        for zone in self.ZoneToRoom:
            if type(self.ZoneToRoom[zone]) == types.ListType:
                for room in self.ZoneToRoom[zone]:
                    for specialRoom in SpecialRooms:
                        if room == specialRoom:
                            self.ZoneToRoom[zone] = specialRoom

        return

    def OnStartOfTutorialMission(self, teamids):
        teamleader = teamids[0]
        mission.toggleView(teamleader, PANEL_INVENTORY, 0)
        mission.toggleView(teamleader, PANEL_ACTIONS, 0)
        mission.toggleView(teamleader, PANEL_STATUS, 0)
        mission.toggleView(teamleader, PANEL_MISSION, 0)
        mission.toggleView(teamleader, PANEL_TEAM, 0)
        mission.toggleView(teamleader, PANEL_MAP, 0)
        mission.toggleView(teamleader, PANEL_CREW, 0)
        mission.toggleView(teamleader, PANEL_FACTION, 0)
        mission.toggleView(teamleader, PANEL_PDA, 0)
        mission.toggleView(teamleader, PANEL_COMPASS, 0)
        mission.toggleView(teamleader, PANEL_TARGET, 0)
        mission.toggleView(teamleader, PANEL_TOOLBAR, 0)
        mission.toggleView(teamleader, PANEL_MINITEAM, 0)
        return

    def OnInventoryItemsSwapped(self, phasenumber, sender, destSlot, destID, destData, srcSlot, srcID, srcData):
        if phasenumber != 0:
            return
        if mission.isGoalComplete(sender, EQUIP_GOAL):
            return
        if (destID == MALE_SHOES or srcID == MALE_SHOES or destID == FEMALE_SHOES or srcID == FEMALE_SHOES) and (destSlot == SHOE_SLOT or srcSlot == SHOE_SLOT):
            mission.completeGoal(sender, EQUIP_GOAL)
        return

    def OnShortcutAdded(self, phasenumber, sender, shortcutType, classData, instanceData, invSlot):
        if phasenumber != 0:
            return
        if shortcutType != 0 or classData != 151:
            return
        if mission.isGoalComplete(sender, HOTBAR_GOAL):
            return
        mission.completeGoal(sender, HOTBAR_GOAL)
        return

    def OnMovementUpdated(self, phasenumber, sender, posX, posY, posZ, facingAngle):
        if phasenumber != 0:
            return
        cameraComplete = mission.isGoalComplete(sender, CAMERA_GOAL)
        movementComplete = mission.isGoalComplete(sender, MOVEMENT_GOAL)
        introComplete = mission.isGoalComplete(sender, INTRO_GOAL)
        if cameraComplete and movementComplete:
            return
        if self.previousX != None and introComplete:
            if not cameraComplete and self.previousAngle != facingAngle:
                mission.completeGoal(sender, CAMERA_GOAL)
            if not movementComplete and self.previousX != posX or self.previousY != posY or self.previousZ != posZ:
                mission.completeGoal(sender, MOVEMENT_GOAL)
        self.previousX = posX
        self.previousY = posY
        self.previousZ = posZ
        self.previousAngle = facingAngle
        return

    ZoneToRoom = {}
    previousX = None
    previousY = None
    previouzZ = None
    previousAngle = None


PANEL_INVENTORY = 1
PANEL_ACTIONS = 2
PANEL_STATUS = 3
PANEL_MISSION = 4
PANEL_TEAM = 5
PANEL_MAP = 6
PANEL_CREW = 7
PANEL_FACTION = 8
PANEL_OPTIONS = 9
PANEL_PDA = 10
PANEL_COMPASS = 11
PANEL_TARGET = 12
PANEL_TOOLBAR = 13
PANEL_CHAT = 14
PANEL_CAMERA = 15
PANEL_ZOOM = 16
PANEL_MOVEMENT = 17
PANEL_MISSION_DIALOGUE = 18
PANEL_PLAYER_VIEW = 19
PANEL_MINITEAM = 20
INTRO_GOAL = 0
CAMERA_GOAL = 1
MOVEMENT_GOAL = 2
CELLPHONE_GOAL = 4
TALKING_GOAL = 5
INVENTORY_GOAL = 7
EQUIP_GOAL = 8
ABILITIES_GOAL = 9
HOTBAR_GOAL = 11
MAP_GOAL = 12
DOJO_GOAL = 13
INTERLOCK_GOAL = 18
VIRUS_GOAL = 19
HACKING_GOAL = 22
LEAVING_GOAL = 27
START_DIALOG = 2248147575L
CAMERA_DIALOG = 2248147579L
CAMERA_SUCCESS = 2248147580L
CAMERA_FAILURE = 2248147581L
ARROW_DIALOG = 2248147582L
ARROW_SUCCESS = 2248147583L
ARROW_FAILURE = 2248147584L
COMPASS_DIALOG = 2248147585L
COMPASS_SUCCESS = 2248147588L
COMPASS_FAILURE = 2248147589L
TARGET_DIALOG = 2248147590L
TARGET_SUCCESS = 2248147592L
TARGET_FAILURE = 2248147593L
RADIAL_DIALOG = 2248147594L
RADIAL_SUCCESS = 2248147596L
RADIAL_FAILURE = 2248147597L
INTERACTION_DIALOG = 2248147598L
INTERACTION_SUCCESS = 2248147601L
INTERACTION_FAILURE = 2248147602L
INVENTORY_DIALOG = 2248147604L
INVENTORY_SUCCESS = 2248147606L
INVENTORY_FAILURE = 2248147605L
ITEM_DIALOG = 2248147607L
ITEM_SUCCESS = 2248147609L
ITEM_FAILURE = 2248147610L
ACTION_DIALOG = 2248147611L
ACTION_SUCCESS = 2248147614L
ACTION_FAILURE = 2248147613L
ABILITY_DIALOG = 2248147614L
ABILITY_SUCCESS = 2248147617L
ABILITY_FAILURE = 2248147616L
GOTODOJO_DIALOG = 2248147618L
GOTODOJO_SUCCESS = 2248147620L
GOTODOJO_FAILURE = 2248147619L
AIKIDO_DIALOG = 2248147621L
AIKIDO_SUCCESS = 2248147632L
AIKIDO_FAILURE = 2248147622L
AIKIDOSTART_DIALOG = 2248147631L
GOTOHACKER_DIALOG = 2248147635L
GOTOHACKER_SUCCESS = 2248147636L
GOTOHACKER_FAILURE = 0L
HACKCOMPUTER_DIALOG = 2248147638L
HACKCOMPUTER_SUCCESS = 2248147640L
HACKCOMPUTER_FAILURE = 2248147641L
GOTOSPY_DIALOG = 2248147642L
GOTOSPY_SUCCESS = 2248147643L
GOTOSPY_FAILURE = 0L
SNEAK_DIALOG = 2248147645L
SNEAK_SUCCSES = 2248147647L
SNEAK_FAILURE = 2248147646L
SEARCH_DIALOG = 2248147652L
LOCKPICK_DIALOG = 2248147654L
LOCKPICK_SUCCESS = 2248147655L
LOCKPICK_FAILURE = 2248147656L
HARDLINE_DIALOG1 = 2248147657L
HARDLINE_DIALOG2 = 2248147658L
HARDLINE_DIALOG3 = 2248147660L
HARDLINE_FAILURE = 2248147661L
HARDLINE_SUCCESS = 2248147662L
ENTER_ENTER_DOJO = 'Entrance -- Enter/Dojo'
DOJO_ENTER_DOJO = 'Enter/Dojo -- Dojo'
SERVER_DOJO_SERVER = 'Dojo/Server -- Serverroom'
DOJO_DOJO_SERVER = 'Dojo/Server -- Dojo'
SERVER_SERVER_EXIT = 'Serverroom -- Server/Exit'
EXIT_SERVER_EXIT = 'Server/Exit -- Exit'
Doors = [
 ENTER_ENTER_DOJO, DOJO_ENTER_DOJO, SERVER_DOJO_SERVER, DOJO_DOJO_SERVER, SERVER_SERVER_EXIT, 
 EXIT_SERVER_EXIT]
ENTER = 'Entrance'
ENTER_DOJO = 'Enter/Dojo'
DOJO = 'Dojo'
DOJO_SERVER = 'Dojo/Server'
SERVER = 'Serverroom'
SERVER_EXIT = 'Server/Exit'
EXIT = 'Exit'
Rooms = [ENTER, ENTER_DOJO, DOJO, DOJO_SERVER, SERVER, SERVER_EXIT, EXIT]
SpecialRooms = [
 ENTER, EXIT]
SHOE_SLOT = 56
MALE_SHOES = 1264
FEMALE_SHOES = 909
return

# okay decompiling work/monolith_pyc\Missions\Mission185.pyc
