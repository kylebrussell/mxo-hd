# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\Missions\Mission986.py
# Compiled at: 2006-11-15 14:24:42
from Mission import Mission
from ltfxmap import *
import stringtable_client as StringTable

class Mission986(Mission):
    __module__ = __name__

    def OnStartOfMission(self, teamids):
        teamleader = teamids[0]
        mission.addTempAbility(teamleader, 460, 1)
        if consolevar.TutorialWarpFactor:
            self.MissionWarping = True
            goals = []
            if consolevar.TutorialWarpFactor == constants.Tutorial.Transition:
                goals = range(CAMERA_GOAL, CHOICE_GOAL)
            if consolevar.TutorialWarpFactor == constants.Tutorial.WhiteRoom:
                goals = range(CAMERA_GOAL, REDPILL_GOAL)
            if consolevar.TutorialWarpFactor == constants.Tutorial.Scarlet:
                goals = range(CAMERA_GOAL, CALIBRATE_GOAL)
            if consolevar.TutorialWarpFactor == constants.Tutorial.DojoAnte:
                goals = range(CAMERA_GOAL, GIVE_GOAL)
            if consolevar.TutorialWarpFactor == constants.Tutorial.Dojo:
                goals = range(CAMERA_GOAL, WITHDRAWL_GOAL)
            if consolevar.TutorialWarpFactor == constants.Tutorial.Gun:
                goals = range(CAMERA_GOAL, RESPAWN_GOAL)
            if consolevar.TutorialWarpFactor == constants.Tutorial.Hardline:
                goals = range(CAMERA_GOAL, FIREARM_COMBAT_GOAL)
            if consolevar.TutorialWarpFactor == constants.Tutorial.End:
                goals = range(CAMERA_GOAL, FIREARM_COMBAT_GOAL)
            for goal in goals:
                mission.completeGoal(teamleader, goal)

            self.MissionWarping = False
            if len(goals):
                mission.completeGoal(teamleader, goals[-1] + 1)
            return
        mission.playNarration(teamleader, CAMERA_DIALOG)
        mission.sendMissionEvent(teamleader, 1)
        mission.scheduleEvent(teamleader, 26, CAMERA_GOAL + DELAY_OFFSET)
        return

    def OnPhaseComplete(self, phasenumber, teamids):
        if phasenumber != 0:
            return
        teamleader = teamids[0]
        return

    def OnAbort(self, teamids):
        curLevel = mission.getAvgLevel(teamids[0])
        if curLevel == 1:
            mission.grantExperience(teamids[0], 501)
        return

    def OnStartOfStage(self, phasenumber, goalnumber, teamids):
        return

    def OnGoalComplete(self, phasenumber, goalnumber, teamids):
        if self.MissionWarping:
            return
        if phasenumber != 0:
            return
        teamleader = teamids[0]
        if goalnumber == CAMERA_GOAL:
            mission.playNarration(teamleader, MOVEMENT_DIALOG)
            mission.scheduleEvent(teamleader, 1, MOVEMENT_GOAL + DELAY_OFFSET)
            return
        if goalnumber == MOVEMENT_GOAL:
            mission.toggleView(teamleader, constants.PANEL.TARGET, 1)
            mission.playNarration(teamleader, TALKING_DIALOG)
            mission.scheduleEvent(teamleader, 0, TALKING_GOAL + DELAY_OFFSET)
            return
        if goalnumber == TALKING_GOAL:
            mission.playNarration(teamleader, CHOICE_DIALOG)
            mission.scheduleEvent(teamleader, 28, CHOICE_GOAL + DELAY_OFFSET)
            return
        if goalnumber == CHOICE_GOAL:
            mission.scheduleEvent(teamleader, 7, REDPILL_GOAL + PRETELEPORT_OFFSET)
            return
        if goalnumber == REDPILL_GOAL:
            mission.playNarration(teamleader, CALIBRATE_DIALOG)
            mission.toggleView(teamleader, constants.PANEL.PLAYER_VIEW, 1)
            mission.scheduleEvent(teamleader, DELAY_TIME, CALIBRATE_GOAL + DELAY_OFFSET)
            return
        if goalnumber == CALIBRATE_GOAL:
            mission.playNarration(teamleader, CALIBRATE_SUCCESS)
            mission.scheduleEvent(teamleader, DELAY_TIME, TALK2_GOAL + PRETELEPORT_OFFSET)
            return
        if goalnumber == TALK2_GOAL:
            mission.playNarration(teamleader, CONTAINER_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, CONTAINER_GOAL + DELAY_OFFSET)
            return
        if goalnumber == CONTAINER_GOAL:
            mission.playNarration(teamleader, LOOT_DIALOG)
            mission.scheduleEvent(teamleader, 0, LOOT_GOAL + DELAY_OFFSET)
            return
        if goalnumber == LOOT_GOAL:
            mission.toggleView(teamleader, constants.PANEL.INVENT, 1)
            mission.playNarration(teamleader, INVENTORY_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, INVENTORY_GOAL + DELAY_OFFSET)
            return
        if goalnumber == INVENTORY_GOAL:
            mission.playNarration(teamleader, EQUIP_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, EQUIP_GOAL + DELAY_OFFSET)
            return
        if goalnumber == EQUIP_GOAL:
            mission.playNarration(teamleader, GIVE_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, GIVE_GOAL + DELAY_OFFSET)
            return
        if goalnumber == GIVE_GOAL:
            mission.playNarration(teamleader, GIVE_SUCCESS)
            mission.scheduleEvent(teamleader, 7, DAMAGE_GOAL + PRETELEPORT_OFFSET)
            return
        if goalnumber == DAMAGE_GOAL:
            mission.toggleView(teamleader, constants.PANEL.ACTIONS, 1)
            mission.playNarration(teamleader, ACTIONS_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, ACTIONS_GOAL + DELAY_OFFSET)
            return
        if goalnumber == ACTIONS_GOAL:
            mission.toggleView(teamleader, constants.PANEL.TOOLBAR, 1)
            mission.toggleView(teamleader, constants.PANEL.BUFFHUD, 1)
            mission.playNarration(teamleader, HOTBAR_DIALOG)
            return
        if goalnumber == HOTBAR_GOAL:
            mission.scheduleEvent(teamleader, DELAY_TIME, CHEAPSHOT_GOAL + DELAY_OFFSET)
            return
        if goalnumber == CHEAPSHOT_GOAL:
            mission.scheduleEvent(teamleader, DELAY_TIME, SPEED_GOAL + DELAY_OFFSET)
            mission.toggleView(teamleader, constants.PANEL.INTERLOCK_SPEED, 1)
            return
        if goalnumber == SPEED_GOAL:
            mission.playNarration(teamleader, SPEED_SUCCESS)
            mission.scheduleEvent(teamleader, DELAY_TIME, POWER_GOAL + DELAY_OFFSET)
            mission.toggleView(teamleader, constants.PANEL.INTERLOCK_POWER, 1)
            return
        if goalnumber == POWER_GOAL:
            mission.playNarration(teamleader, POWER_SUCCESS)
            mission.toggleView(teamleader, constants.PANEL.INTERLOCK_GRAB, 1)
            mission.scheduleEvent(teamleader, DELAY_TIME, GRAB_GOAL + DELAY_OFFSET)
            return
        if goalnumber == GRAB_GOAL:
            mission.playNarration(teamleader, GRAB_SUCCESS)
            mission.scheduleEvent(teamleader, DELAY_TIME, BLOCK_GOAL + DELAY_OFFSET)
            mission.toggleView(teamleader, constants.PANEL.INTERLOCK_BLOCK, 1)
            return
        if goalnumber == BLOCK_GOAL:
            mission.playNarration(teamleader, BLOCK_SUCCESS)
            mission.scheduleEvent(teamleader, DELAY_TIME, WITHDRAWL_GOAL + DELAY_OFFSET)
            mission.toggleView(teamleader, constants.PANEL.INTERLOCK_WITHDRAWL, 1)
            return
        if goalnumber == WITHDRAWL_GOAL:
            mission.playNarration(teamleader, ENTERDOJO_DIALOG)
            mission.sendMissionEvent(teamleader, 2)
            return
        if goalnumber == ENTERDOJO_GOAL:
            mission.playNarration(teamleader, COMBAT_DIALOG)
            mission.scheduleEvent(teamleader, 39, COMBAT_GOAL + DELAY_OFFSET)
            return
        if goalnumber == COMBAT_GOAL:
            return
        if goalnumber == ABILITIES_USE_GOAL:
            if mission.isGoalComplete(teamleader, COMBAT_LOOT_GOAL):
                mission.sendModalMessage(teamleader, RESPAWN_GOAL, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_RESPAWN, 0, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_RESPAWN_YES, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_RESPAWN_NO)
                mission.playNarration(teamleader, COMBAT_RESPAWN_DIALOG)
                return
            mission.playNarration(teamleader, COMBAT_LOOT_DIALOG)
            mission.scheduleEvent(teamleader, 2, COMBAT_LOOT_GOAL + DELAY_OFFSET)
            return
        if goalnumber == COMBAT_LOOT_GOAL:
            mission.playNarration(teamleader, RESPAWN_DIALOG)
            mission.sendModalMessage(teamleader, RESPAWN_GOAL, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_RESPAWN, 0, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_RESPAWN_YES, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_RESPAWN_NO)
            return
        if goalnumber == RESPAWN_GOAL:
            mission.playNarration(teamleader, COMBAT_LOOT_SUCCESS)
            mission.scheduleEvent(teamleader, 7, CONNING_GOAL + PRETELEPORT_OFFSET)
            return
        if goalnumber == CONNING_GOAL:
            mission.playNarration(teamleader, CONNING_SUCCESS)
            mission.scheduleEvent(teamleader, DELAY_TIME, FIREARM_COMBAT_GOAL + DELAY_OFFSET)
            return
        if goalnumber == FIREARM_COMBAT_GOAL:
            mission.playNarration(teamleader, FIREARM_COMBAT_SUCCESS)
            mission.scheduleEvent(teamleader, 7, UPLOAD_GOAL + PRETELEPORT_OFFSET)
            return
        if goalnumber == UPLOAD_GOAL:
            mission.toggleView(teamleader, constants.PANEL.STATUS, 1)
            mission.grantExperience(teamleader, 501)
            mission.playNarration(teamleader, LEVELUP_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, LEVELUP_GOAL + DELAY_OFFSET)
            return
        if goalnumber == LEVELUP_GOAL:
            mission.playNarration(teamleader, LEVELUP2_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, OPENLEVEL_GOAL + DELAY_OFFSET)
            return
        if goalnumber == OPENLEVEL_GOAL:
            mission.playNarration(teamleader, LEVELUP_ABILITY_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, SELECTAWAKENED_GOAL + DELAY_OFFSET)
            return
        if goalnumber == SELECTAWAKENED_GOAL:
            mission.playNarration(teamleader, LEVELUP_ABILITY2_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, ADVANCEAWAKENED_GOAL + DELAY_OFFSET)
            return
        if goalnumber == ADVANCEAWAKENED_GOAL:
            mission.playNarration(teamleader, LEVELUP_ABILITY3_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, APPLY_LEVELUP_GOAL + DELAY_OFFSET)
            return
        if goalnumber == APPLY_LEVELUP_GOAL:
            mission.playNarration(teamleader, LEVELUP_ATTRIBUTE_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, OPEN_ATTRIBUTE_GOAL + DELAY_OFFSET)
            return
        if goalnumber == OPEN_ATTRIBUTE_GOAL:
            mission.playNarration(teamleader, LEVELUP_ATTRIBUTE2_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, UPGRADE_ATTRIBUTE_GOAL + DELAY_OFFSET)
            return
        if goalnumber == UPGRADE_ATTRIBUTE_GOAL:
            mission.playNarration(teamleader, LEVELUP_ATTRIBUTE3_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, CONFIRM_ATTRIBUTE_GOAL + DELAY_OFFSET)
            return
        if goalnumber == CONFIRM_ATTRIBUTE_GOAL:
            mission.playNarration(teamleader, EXIT_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, EXIT_GOAL + DELAY_OFFSET)
            return
        if goalnumber == EXIT_GOAL:
            mission.playNarration(teamleader, EXIT_SUCCESS)
            return
        return

    def OnToggledViewActivated(self, phasenumber, teamleader, viewid):
        if phasenumber != 0:
            return
        if viewid == constants.PANEL.PLAYER_VIEW:
            mission.completeGoal(teamleader, CALIBRATE_GOAL)
        if viewid == constants.PANEL.STATUS:
            mission.completeGoal(teamleader, LEVELUP_GOAL)
        if viewid == constants.PANEL.INVENT:
            mission.completeGoal(teamleader, INVENTORY_GOAL)
        if viewid == constants.PANEL.ACTIONS:
            mission.completeGoal(teamleader, ACTIONS_GOAL)
        if viewid == constants.PANEL.TOOLBAR:
            mission.completeGoal(teamleader, HOTBAR_GOAL)
        if viewid == constants.PANEL.INTERLOCK_POWER:
            mission.completeGoal(teamleader, POWER_GOAL)
        if viewid == constants.PANEL.INTERLOCK_SPEED:
            mission.completeGoal(teamleader, SPEED_GOAL)
        if viewid == constants.PANEL.INTERLOCK_BLOCK:
            mission.completeGoal(teamleader, BLOCK_GOAL)
        if viewid == constants.PANEL.INTERLOCK_GRAB:
            mission.completeGoal(teamleader, GRAB_GOAL)
        if viewid == constants.PANEL.INTERLOCK_WITHDRAWL:
            mission.completeGoal(teamleader, WITHDRAWL_GOAL)
        if viewid == constants.PANEL.LEVEL_UP:
            mission.completeGoal(teamleader, OPENLEVEL_GOAL)
        if viewid == constants.PANEL.FIRST_ABILITY:
            mission.completeGoal(teamleader, SELECTAWAKENED_GOAL)
        if viewid == constants.PANEL.ADVANCE_LEVEL:
            mission.completeGoal(teamleader, ADVANCEAWAKENED_GOAL)
        if viewid == constants.PANEL.APPLY_ADVANCE:
            mission.completeGoal(teamleader, APPLY_LEVELUP_GOAL)
        if viewid == constants.PANEL.OPEN_ATTRIBUTE:
            mission.completeGoal(teamleader, OPEN_ATTRIBUTE_GOAL)
        if viewid == constants.PANEL.UPGRADE_ATTRIBUTE:
            mission.completeGoal(teamleader, UPGRADE_ATTRIBUTE_GOAL)
        if viewid == constants.PANEL.CONFIRM_ATTRIBUTE:
            mission.completeGoal(teamleader, CONFIRM_ATTRIBUTE_GOAL)
        return

    def OnModalDialogueAccepted(self, phasenumber, teamleader, messageid, leftchoice):
        if phasenumber != 0:
            return
        if messageid == 9999:
            return
        if messageid == CHOICE_GOAL:
            if not leftchoice:
                mission.sendMissionEvent(teamleader, 0)
                mission.playNarration(teamleader, REDPILL_DIALOG)
                mission.completeGoal(teamleader, CHOICE_GOAL)
            else:
                mission.playNarration(teamleader, BLUEPILL_CONFIRM)
                mission.sendModalMessage(teamleader, CHOICE_CONFIRM, StringTable.ID_TUTORIAL_NEW_PILL_BLUE, 0, StringTable.ID_OBJ_MISSION_BLUEPILL, StringTable.ID_OBJ_MISSION_REDPILL)
            return
        if messageid == CHOICE_CONFIRM:
            if not leftchoice:
                mission.sendMissionEvent(teamleader, 0)
                mission.playNarration(teamleader, REDPILL_DIALOG)
                mission.completeGoal(teamleader, CHOICE_GOAL)
            else:
                mission.sendMissionEvent(teamleader, 0)
                mission.playNarration(teamleader, BLUEPILL_TAKEN)
                mission.scheduleEvent(teamleader, 5, BLUEPILL_TAKEN)
            return
        if messageid == RESPAWN_GOAL:
            if leftchoice:
                mission.respawnNPC(teamleader, 'Bob')
                mission.unkillNPC(teamleader, 'Bob')
                mission.playNarration(teamleader, COMBAT_REMINDER)
            else:
                mission.completeGoal(teamleader, RESPAWN_GOAL)
            return
        return

    def OnMissionEvent(self, phasenumber, eventid, teamids):
        if phasenumber != 0:
            return
        teamleader = teamids[0]
        if eventid == EXIT_GOAL:
            mission.playNarration(teamleader, EXIT_REMINDER)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, EXIT_GOAL)
            return
        if eventid >= TRANSITION_OFFSET:
            pass
        elif eventid >= DELAY_OFFSET:
            if mission.isGoalComplete(teamleader, eventid - DELAY_OFFSET):
                return
        elif mission.isGoalComplete(teamleader, eventid):
            return
        if eventid == CAMERA_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CAMERA, 671091620)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 46 - 41, CAMERA_GOAL)
            return
        if eventid == MOVEMENT_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_MOVEMENT, 671091621)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 12 - 1, MOVEMENT_GOAL)
            return
        if eventid == TALKING_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_TALK, CAPTAIN_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 3 - 0, TALKING_GOAL)
            return
        if eventid == CHOICE_GOAL + DELAY_OFFSET:
            mission.sendModalMessage(teamleader, CHOICE_GOAL, StringTable.ID_TUTORIAL_NEW_PILL, 0, StringTable.ID_OBJ_MISSION_BLUEPILL, StringTable.ID_OBJ_MISSION_REDPILL)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 11, CHOICE_GOAL)
            return
        if eventid == REDPILL_GOAL + DELAY_OFFSET:
            mission.completeGoal(teamleader, REDPILL_GOAL)
            return
        if eventid == CALIBRATE_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_HEALTH, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 8 - DELAY_TIME, CALIBRATE_GOAL)
            return
        if eventid == TALK2_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_TALK2, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 3 - DELAY_TIME, TALK2_GOAL)
            return
        if eventid == CONTAINER_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_SEARCH, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 8 - DELAY_TIME, CONTAINER_GOAL)
            return
        if eventid == LOOT_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LOOT, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 2 - 0, LOOT_GOAL)
            return
        if eventid == INVENTORY_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_INVENTORY, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 3 - DELAY_TIME, INVENTORY_GOAL)
            return
        if eventid == EQUIP_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_INVENTORY_WEAR, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 6 - DELAY_TIME, EQUIP_GOAL)
            return
        if eventid == GIVE_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_GIVE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 6 - DELAY_TIME, GIVE_GOAL)
            return
        if eventid == DAMAGE_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_DAMAGE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, 40, DAMAGE_GOAL)
            return
        if eventid == ACTIONS_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_ACTIONS, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 7 - DELAY_TIME, ACTIONS_GOAL)
            return
        if eventid == HOTBAR_GOAL + DELAY_OFFSET:
            return
        if eventid == CHEAPSHOT_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_HOTBAR_ABILITIES, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 7 - DELAY_TIME, CHEAPSHOT_GOAL)
            return
        if eventid == SPEED_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_SPEED, 671093495)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 6 - DELAY_TIME, SPEED_GOAL)
            return
        if eventid == POWER_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_POWER, 671093509)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 6 - DELAY_TIME, POWER_GOAL)
            return
        if eventid == BLOCK_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_BLOCK, 671093502)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 6 - DELAY_TIME, BLOCK_GOAL)
            return
        if eventid == GRAB_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_GRAB, 671093503)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 6 - DELAY_TIME, GRAB_GOAL)
            return
        if eventid == WITHDRAWL_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_WITHDRAW, 671093501)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 6 - DELAY_TIME, WITHDRAWL_GOAL)
            return
        if eventid == ENTERDOJO_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_DOJO, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, ENTERDOJO_GOAL)
            return
        if eventid == COMBAT_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 14, COMBAT_GOAL)
            return
        if eventid == ABILITIES_USE_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_ABILITIES, OPERATOR_IMG)
            mission.completeGoal(teamleader, ABILITIES_USE_GOAL)
            return
        if eventid == COMBAT_LOOT_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LOOT2, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 4 - 2, COMBAT_LOOT_GOAL)
            return
        if eventid == CONNING_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CONNING, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, 12, CONNING_GOAL)
            return
        if eventid == FIREARM_COMBAT_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_FIRE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 4 - DELAY_TIME, FIREARM_COMBAT_GOAL)
            return
        if eventid == UPLOAD_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_UPLOAD, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 18 - 7, UPLOAD_GOAL)
            return
        if eventid == LEVELUP_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_STATUS, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 14 - DELAY_TIME, LEVELUP_GOAL)
            return
        if eventid == OPENLEVEL_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, OPENLEVEL_GOAL)
            return
        if eventid == SELECTAWAKENED_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ABILITY, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, SELECTAWAKENED_GOAL)
            return
        if eventid == ADVANCEAWAKENED_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ABILITY2, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, ADVANCEAWAKENED_GOAL)
            return
        if eventid == APPLY_LEVELUP_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ABILITY3, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, APPLY_LEVELUP_GOAL)
            return
        if eventid == OPEN_ATTRIBUTE_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ATTRIBUTE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, OPEN_ATTRIBUTE_GOAL)
            return
        if eventid == UPGRADE_ATTRIBUTE_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ATTRIBUTE2, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, UPGRADE_ATTRIBUTE_GOAL)
            return
        if eventid == CONFIRM_ATTRIBUTE_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ATTRIBUTE3, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 9 - DELAY_TIME, CONFIRM_ATTRIBUTE_GOAL)
            return
        if eventid == EXIT_GOAL + DELAY_OFFSET:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_HARDLINE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME + 8 - DELAY_TIME, EXIT_GOAL)
            return
        if eventid == CAMERA_GOAL:
            mission.playNarration(teamleader, CAMERA_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CAMERA, 671091620)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, CAMERA_GOAL)
            return
        if eventid == MOVEMENT_GOAL:
            mission.playNarration(teamleader, MOVEMENT_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_MOVEMENT, 671091621)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, MOVEMENT_GOAL)
            return
        if eventid == TALKING_GOAL:
            mission.playNarration(teamleader, TALKING_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_TALK, CAPTAIN_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, TALKING_GOAL)
            return
        if eventid == CHOICE_GOAL:
            mission.playNarration(teamleader, CHOICE_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_PILL, CAPTAIN_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, CHOICE_GOAL)
            return
        if eventid == CALIBRATE_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_HEALTH, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, CALIBRATE_GOAL)
            return
        if eventid == TALK2_GOAL:
            mission.playNarration(teamleader, TALK2_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_TALK2, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, TALK2_GOAL)
            return
        if eventid == CONTAINER_GOAL:
            mission.playNarration(teamleader, CONTAINER_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_SEARCH, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, CONTAINER_GOAL)
            return
        if eventid == LOOT_GOAL:
            if mission.isGoalComplete(sender, EQUIP_GOAL):
                return
            mission.playNarration(teamleader, LOOT_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LOOT_CONDITIONAL_TAKEALL, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, LOOT_GOAL)
            return
        if eventid == INVENTORY_GOAL:
            mission.playNarration(teamleader, INVENTORY_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_INVENTORY, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, INVENTORY_GOAL)
            return
        if eventid == EQUIP_GOAL:
            mission.playNarration(teamleader, EQUIP_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_INVENTORY_WEAR, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, EQUIP_GOAL)
            return
        if eventid == GIVE_GOAL:
            mission.playNarration(teamleader, GIVE_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_GIVE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, GIVE_GOAL)
            return
        if eventid == DAMAGE_GOAL:
            mission.completeGoal(teamleader, DAMAGE_GOAL)
            return
        if eventid == ACTIONS_GOAL:
            mission.playNarration(teamleader, ACTIONS_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_ACTIONS, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, ACTIONS_GOAL)
            return
        if eventid == CHEAPSHOT_GOAL:
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_HOTBAR_ABILITIES, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, CHEAPSHOT_GOAL)
            return
        if eventid == POWER_GOAL:
            mission.playNarration(teamleader, POWER_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_POWER, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, POWER_GOAL)
            return
        if eventid == SPEED_GOAL:
            mission.playNarration(teamleader, SPEED_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_SPEED, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, SPEED_GOAL)
            return
        if eventid == BLOCK_GOAL:
            mission.playNarration(teamleader, BLOCK_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_BLOCK, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, BLOCK_GOAL)
            return
        if eventid == GRAB_GOAL:
            mission.playNarration(teamleader, GRAB_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_GRAB, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, GRAB_GOAL)
            return
        if eventid == WITHDRAWL_GOAL:
            mission.playNarration(teamleader, WITHDRAWL_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT_WITHDRAW, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, WITHDRAWL_GOAL)
            return
        if eventid == ENTERDOJO_GOAL:
            mission.playNarration(teamleader, ENTERDOJO_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_DOJO, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, ENTERDOJO_GOAL)
            return
        if eventid == COMBAT_GOAL:
            mission.playNarration(teamleader, COMBAT_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_CLOSECOMBAT, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, COMBAT_GOAL)
            return
        if eventid == ABILITIES_USE_GOAL:
            return
        if eventid == COMBAT_LOOT_GOAL:
            mission.playNarration(teamleader, COMBAT_LOOT_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LOOT2_CONDITIONAL_TAKEALL, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, COMBAT_LOOT_GOAL)
            return
        if eventid == CONNING_GOAL:
            mission.completeGoal(teamleader, CONNING_GOAL)
            return
        if eventid == FIREARM_COMBAT_GOAL:
            mission.playNarration(teamleader, FIREARM_COMBAT_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_FIRE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, FIREARM_COMBAT_GOAL)
            return
        if eventid == UPLOAD_GOAL:
            mission.playNarration(teamleader, UPLOAD_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_UPLOAD, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, UPLOAD_GOAL)
            return
        if eventid == LEVELUP_GOAL:
            mission.playNarration(teamleader, LEVELUP_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_STATUS, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, LEVELUP_GOAL)
            return
        if eventid == OPENLEVEL_GOAL:
            mission.playNarration(teamleader, LEVELUP2_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, OPENLEVEL_GOAL)
            return
        if eventid == SELECTAWAKENED_GOAL:
            mission.playNarration(teamleader, LEVELUP_ABILITY_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ABILITY, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, SELECTAWAKENED_GOAL)
            return
        if eventid == ADVANCEAWAKENED_GOAL:
            mission.playNarration(teamleader, LEVELUP_ABILITY2_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ABILITY2, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, ADVANCEAWAKENED_GOAL)
            return
        if eventid == APPLY_LEVELUP_GOAL:
            mission.playNarration(teamleader, LEVELUP_ABILITY3_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ABILITY3, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, APPLY_LEVELUP_GOAL)
            return
        if eventid == OPEN_ATTRIBUTE_GOAL:
            mission.playNarration(teamleader, LEVELUP_ATTRIBUTE_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ATTRIBUTE, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, OPEN_ATTRIBUTE_GOAL)
            return
        if eventid == UPGRADE_ATTRIBUTE_GOAL:
            mission.playNarration(teamleader, LEVELUP_ATTRIBUTE2_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ATTRIBUTE2, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, UPGRADE_ATTRIBUTE_GOAL)
            return
        if eventid == CONFIRM_ATTRIBUTE_GOAL:
            mission.playNarration(teamleader, LEVELUP_ATTRIBUTE3_REMINDER)
            mission.broadcastMessage(teamleader, StringTable.ID_TUTORIAL_NEW_LEVELUP_ATTRIBUTE3, OPERATOR_IMG)
            mission.scheduleEvent(teamleader, ELAPSE_TIME, CONFIRM_ATTRIBUTE_GOAL)
            return
        if eventid == REDPILL_GOAL + RECOVERY_OFFSET:
            mission.playNarration(teamleader, CRAPPYPANTS_DIALOG)
            mission.scheduleEvent(teamleader, WHITEROOMVO_TIME, REDPILL_GOAL + DELAY_OFFSET)
            return
        if eventid == CALIBRATE_GOAL + TRANSITION_OFFSET:
            mission.playNarration(teamleader, TALK2_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, TALK2_GOAL + DELAY_OFFSET)
            return
        if eventid == DAMAGE_GOAL + TRANSITION_OFFSET:
            mission.playNarration(teamleader, DAMAGE_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, DAMAGE_GOAL + DELAY_OFFSET)
            return
        if eventid == CONNING_GOAL + TRANSITION_OFFSET:
            mission.playNarration(teamleader, CONNING_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, CONNING_GOAL + DELAY_OFFSET)
            return
        if eventid == UPLOAD_GOAL + TRANSITION_OFFSET:
            mission.playNarration(teamleader, UPLOAD_DIALOG)
            mission.scheduleEvent(teamleader, DELAY_TIME, UPLOAD_GOAL + DELAY_OFFSET)
            return
        if eventid == REDPILL_GOAL + PRETELEPORT_OFFSET:
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_FADE_TO_BLACK)
            mission.scheduleEvent(teamleader, BLACKSTART_TIME, BLACKSTART_DELAY)
            return
        if eventid == TALK2_GOAL + PRETELEPORT_OFFSET:
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_IN, TELEPORT_OFFSET + 2)
            return
        if eventid == DAMAGE_GOAL + PRETELEPORT_OFFSET:
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_IN, TELEPORT_OFFSET + 3)
            return
        if eventid == CONNING_GOAL + PRETELEPORT_OFFSET:
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_IN, TELEPORT_OFFSET + 4)
            return
        if eventid == UPLOAD_GOAL + PRETELEPORT_OFFSET:
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_IN, TELEPORT_OFFSET + 5)
            return
        if eventid == TELEPORT_OFFSET + 2:
            mission.teleportToMissionElement(teamleader, 'Gallery')
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_OUT)
            mission.scheduleEvent(teamleader, TELEPORT_TIME, CALIBRATE_GOAL + TRANSITION_OFFSET)
            return
        if eventid == TELEPORT_OFFSET + 3:
            mission.teleportToMissionElement(teamleader, 'Dojo Ante')
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_OUT)
            mission.scheduleEvent(teamleader, TELEPORT_TIME, DAMAGE_GOAL + TRANSITION_OFFSET)
            return
        if eventid == TELEPORT_OFFSET + 4:
            mission.teleportToMissionElement(teamleader, 'Firearm Training')
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_OUT)
            mission.scheduleEvent(teamleader, TELEPORT_TIME, CONNING_GOAL + TRANSITION_OFFSET)
            return
        if eventid == TELEPORT_OFFSET + 5:
            mission.teleportToMissionElement(teamleader, 'Exit')
            mission.playLocalFX(teamleader, FX_OBJECT_INTERFACE_WHITE_TRANSITION_OUT)
            mission.scheduleEvent(teamleader, TELEPORT_TIME, UPLOAD_GOAL + TRANSITION_OFFSET)
            if consolevar.TutorialWarpFactor == constants.Tutorial.End:
                if consolevar.TutorialWarpFactor == constants.Tutorial.End:
                    goals = range(UPLOAD_GOAL, EXIT_GOAL)
                    for goal in goals:
                        mission.completeGoal(teamleader, goal)

                    self.MissionWarping = False
                    if len(goals):
                        mission.completeGoal(teamleader, goals[-1] + 1)
            return
        if eventid == BLACKSTART_DELAY:
            mission.teleportToMissionElement(teamleader, 'Interrogation')
            mission.scheduleEvent(teamleader, PODRECOVERY_TIME, REDPILL_GOAL + RECOVERY_OFFSET)
            return
        if eventid == BLUEPILL_TAKEN:
            mission.bootPlayer(teamleader)
            return
        return

    def OnObjectTargeted(self, phasenumber, teamleader, itemname):
        return

    def OnPlayerEnterZone(self, phasenumber, teamleader, zoneid):
        return

    def OnStartOfTutorialMission(self, teamids):
        teamleader = teamids[0]
        mission.toggleView(teamleader, constants.PANEL.INVENT, 0)
        mission.toggleView(teamleader, constants.PANEL.ACTIONS, 0)
        mission.toggleView(teamleader, constants.PANEL.STATUS, 0)
        mission.toggleView(teamleader, constants.PANEL.MISSION, 0)
        mission.toggleView(teamleader, constants.PANEL.TEAM, 0)
        mission.toggleView(teamleader, constants.PANEL.MAP, 0)
        mission.toggleView(teamleader, constants.PANEL.CREW, 0)
        mission.toggleView(teamleader, constants.PANEL.FACTION, 0)
        mission.toggleView(teamleader, constants.PANEL.PDA, 0)
        mission.toggleView(teamleader, constants.PANEL.TARGET, 0)
        mission.toggleView(teamleader, constants.PANEL.TOOLBAR, 0)
        mission.toggleView(teamleader, constants.PANEL.BUFFHUD, 0)
        mission.toggleView(teamleader, constants.PANEL.MINITEAM, 0)
        mission.toggleView(teamleader, constants.PANEL.PLAYER_VIEW, 0)
        return

    def OnInventoryItemsSwapped(self, phasenumber, sender, destSlot, destID, destData, srcSlot, srcID, srcData):
        if phasenumber != 0:
            return
        if mission.isGoalComplete(sender, EQUIP_GOAL):
            return
        if not mission.isGoalComplete(sender, EQUIP_GOAL - 1):
            return
        if srcID in (MALE_SHOES, FEMALE_SHOES) and destSlot == constants.Inventory.ShoesSlot:
            mission.completeGoal(sender, EQUIP_GOAL)
        return

    def OnShortcutAdded(self, phasenumber, sender, shortcutType, classData, instanceData, invSlot):
        if phasenumber != 0:
            return
        if not mission.isGoalComplete(sender, HOTBAR_GOAL):
            return
        if mission.isGoalComplete(sender, CHEAPSHOT_GOAL):
            return
        if shortcutType == 0 and classData == constants.Ability.CheapShotAbility:
            mission.completeGoal(sender, CHEAPSHOT_GOAL)
        return

    def OnMovementUpdated(self, phasenumber, sender, posX, posY, posZ, facingAngle):
        if phasenumber != 0:
            return
        cameraComplete = mission.isGoalComplete(sender, CAMERA_GOAL)
        movementComplete = mission.isGoalComplete(sender, MOVEMENT_GOAL)
        if cameraComplete and movementComplete:
            return
        if self.previousX != None:
            if not cameraComplete and self.previousAngle != facingAngle:
                mission.completeGoal(sender, CAMERA_GOAL)
            if not movementComplete and self.previousX != posX or self.previousY != posY or self.previousZ != posZ:
                mission.completeGoal(sender, MOVEMENT_GOAL)
        self.previousX = posX
        self.previousY = posY
        self.previousZ = posZ
        self.previousAngle = facingAngle
        return

    previousX = None
    previousY = None
    previouzZ = None
    previousAngle = None
    MissionWarping = None


CAMERA_GOAL = 0
MOVEMENT_GOAL = 1
TALKING_GOAL = 2
CHOICE_GOAL = 3
REDPILL_GOAL = 4
CALIBRATE_GOAL = 5
TALK2_GOAL = 6
CONTAINER_GOAL = 7
LOOT_GOAL = 8
INVENTORY_GOAL = 9
EQUIP_GOAL = 10
GIVE_GOAL = 11
DAMAGE_GOAL = 12
ACTIONS_GOAL = 13
HOTBAR_GOAL = 14
CHEAPSHOT_GOAL = 15
SPEED_GOAL = 16
POWER_GOAL = 17
GRAB_GOAL = 18
BLOCK_GOAL = 19
WITHDRAWL_GOAL = 20
ENTERDOJO_GOAL = 21
COMBAT_GOAL = 22
ABILITIES_USE_GOAL = 23
COMBAT_LOOT_GOAL = 24
RESPAWN_GOAL = 25
CONNING_GOAL = 26
FIREARM_COMBAT_GOAL = 27
UPLOAD_GOAL = 28
LEVELUP_GOAL = 29
OPENLEVEL_GOAL = 30
SELECTAWAKENED_GOAL = 31
ADVANCEAWAKENED_GOAL = 32
APPLY_LEVELUP_GOAL = 33
OPEN_ATTRIBUTE_GOAL = 34
UPGRADE_ATTRIBUTE_GOAL = 35
CONFIRM_ATTRIBUTE_GOAL = 36
EXIT_GOAL = 37
START_DIALOG = 2248149104L
CAMERA_DIALOG = 2248149104L
CAMERA_REMINDER = 2248149108L
MOVEMENT_DIALOG = 2248149109L
MOVEMENT_REMINDER = 2248149111L
TALKING_DIALOG = 2248149113L
TALKING_REMINDER = 2248149114L
CHOICE_DIALOG = 2248149115L
CHOICE_REMINDER = 2248149119L
BLUEPILL_CONFIRM = 2248149120L
BLUEPILL_TAKEN = 2248149121L
REDPILL_DIALOG = 2248149122L
CALIBRATE_DIALOG = 2248149349L
CALIBRATE_SUCCESS = 2248148756L
CRAPPYPANTS_DIALOG = 2248148944L
TALK2_DIALOG = 2248148757L
TALK2_REMINDER = 2248148758L
MAP_DIALOG = 2248148433L
MAP_REMINDER = 2248148434L
COMPASS_DIALOG = 2248148435L
COMPASS_REMINDER = 2248148437L
CONTAINER_DIALOG = 2248148762L
CONTAINER_REMINDER = 2248148763L
LOOT_DIALOG = 2248148764L
LOOT_REMINDER = 2248148765L
INVENTORY_DIALOG = 2248148767L
INVENTORY_REMINDER = 2248148769L
EQUIP_DIALOG = 2248148770L
EQUIP_REMINDER = 2248148771L
GIVE_DIALOG = 2248148772L
GIVE_REMINDER = 2248148774L
GIVE_SUCCESS = 2248148775L
DAMAGE_DIALOG = 2248149350L
SPEED_SUCCESS = 2248149353L
SPEED_REMINDER = 2248149383L
POWER_SUCCESS = 2248149354L
POWER_REMINDER = 2248149384L
GRAB_SUCCESS = 2248149351L
GRAB_REMINDER = 2248149385L
BLOCK_SUCCESS = 2248149355L
BLOCK_REMINDER = 2248149382L
WITHDRAWL_SUCCESS = 2248149356L
WITHDRAWL_REMINDER = 2248149386L
ACTIONS_DIALOG = 2248148777L
ACTIONS_REMINDER = 2248148778L
HOTBAR_DIALOG = 2248148779L
HOTBAR_REMINDER = 0L
ENTERDOJO_DIALOG = 2248148783L
ENTERDOJO_REMINDER = 2248148787L
COMBAT_DIALOG = 2248148989L
COMBAT_REMINDER = 2248148787L
COMBAT_RESPAWN_DIALOG = 2248150063L
ABILITIES_DIALOG = 2248148788L
ABILITIES_REMINDER = 2248148789L
COMBAT_LOOT_DIALOG = 2248148791L
COMBAT_LOOT_REMINDER = 2248148833L
RESPAWN_DIALOG = 2248149357L
COMBAT_LOOT_SUCCESS = 2248148795L
HOTBAR_INVENTORY_DIALOG = 2248148796L
HOTBAR_INVENTORY_REMINDER = 2248148797L
FIREARM_EQUIP_DIALOG = 2248148798L
CONNING_DIALOG = 2248149358L
CONNING_SUCCESS = 2248148799L
FIREARM_COMBAT_SUCCESS = 2248148800L
FIREARM_EQUIP_REMINDER = 2248148801L
FIREARM_COMBAT_DIALOG = 2248148798L
FIREARM_COMBAT_REMINDER = 2248148799L
UPLOAD_DIALOG = 2248149348L
UPLOAD_REMINDER = 2248148804L
LEVELUP_DIALOG = 2248148806L
LEVELUP_REMINDER = 2248148807L
LEVELUP2_DIALOG = 2248148808L
LEVELUP2_REMINDER = 2248150055L
LEVELUP_ABILITY_DIALOG = 2248150053L
LEVELUP_ABILITY_REMINDER = 2248150056L
LEVELUP_ABILITY2_DIALOG = 2248150054L
LEVELUP_ABILITY2_REMINDER = 2248150060L
LEVELUP_ABILITY3_DIALOG = 2248150057L
LEVELUP_ABILITY3_REMINDER = 2248150058L
LEVELUP_ATTRIBUTE_DIALOG = 2248150059L
LEVELUP_ATTRIBUTE_REMINDER = 2248150060L
LEVELUP_ATTRIBUTE2_DIALOG = 2248150061L
LEVELUP_ATTRIBUTE2_REMINDER = 2248150060L
LEVELUP_ATTRIBUTE3_DIALOG = 2248150062L
LEVELUP_ATTRIBUTE3_REMINDER = 2248150060L
EXIT_DIALOG = 2248149029L
EXIT_REMINDER = 2248148810L
EXIT_SUCCESS = 2248148811L
DOJO_ANTE = 'Dojo -- Dojo Ante'
CAPTAIN_IMG = 2415920801L
OPERATOR_IMG = 671093152L
MALE_SHOES = 1258
FEMALE_SHOES = 905
ELAPSE_TIME = 60
DELAY_TIME = 5
DELAY_OFFSET = 1000
BLACKSTART_TIME = 10
PODRECOVERY_TIME = 24
WHITEROOMVO_TIME = 23 + 2
TELEPORT_TIME = 5
TRANSITION_OFFSET = 2000
RECOVERY_OFFSET = 3000
PRETELEPORT_OFFSET = 4000
TELEPORT_OFFSET = 5000
BLACKSTART_DELAY = 6000
CHOICE_CONFIRM = 7000
return

# okay decompiling work/monolith_pyc\Missions\Mission986.pyc
