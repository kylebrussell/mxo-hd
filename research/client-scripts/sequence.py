# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\sequence.py
# Compiled at: 2009-06-29 12:32:43
import stringtable_client as ST, random

def Init():
    print 'Sequence system Inited'
    return


def GetEngineObject(GO):
    if GO.CharacterMovement is not None:
        return GO.CharacterMovement
    if GO.Model is not None:
        return GO.Model
    if GO.Normal is not None:
        return GO.Normal
    return


def GetObjectPosition(GO):
    Engine = GetEngineObject(GO)
    if Engine is not None:
        return Engine.Position
    else:
        return None
    return


def GetObjectOrientation(GO):
    Engine = GetEngineObject(GO)
    if Engine is not None:
        return Engine.Orientation
    else:
        return None
    return


def OnSequence1Completed(GangMasterAdapter=None):
    print 'Complete'
    if GangMasterAdapter is None:
        return
    print 'Got Gang'
    Item = discovery.getGameObjectType('PandoraRewardContainer')
    TargetLocator = GangMasterAdapter.TargetLocator
    Target = discovery.getPlayerCharID(TargetLocator)
    Code = 'SetPermanentExclusion(%d);AddRewardGroup("PANDORA_TABLES_Pandora1PSR")' % int(Target)
    Position = GetObjectPosition(GangMasterAdapter)
    Orientation = GetObjectOrientation(GangMasterAdapter)
    discovery.SpawnObject(Item, consolevar.ConID, Position, Orientation, Code)
    print 'Done0r'
    return


def OnSequence1Failed(GangMasterAdapter=None):
    return


def GetNearestBox(GangMasterAdapter, GOID, dist):
    position = GetObjectPosition(GangMasterAdapter)
    if position is None:
        return None
    components = (constants.ComponentFactory.ELTGOModel, constants.ComponentFactory.ELTGOStateMachine)
    objects = physics.getObjectsInRadius(position.x, position.y, position.z, dist, components, 400.0)
    closestDist = dist * dist * 400 + 1
    closestBox = None
    for object in objects:
        if object.type == GOID:
            objPos = GetObjectPosition(object)
            if objPos is not None:
                curDist = (position.x - objPos.x) * (position.x - objPos.x) + (position.y - objPos.y) * (position.y - objPos.y) + (position.z - objPos.z) * (position.z - objPos.z)
                if curDist < closestDist:
                    closestDist = curDist
                    closestBox = object

    return closestBox
    return


def SpawnPandoraReward(GangMasterAdapter, RewardTable, Position):
    if GangMasterAdapter is None or Position is None:
        return
    Item = discovery.getGameObjectType('PandoraRewardContainer')
    TargetLocator = GangMasterAdapter.TargetLocator
    Target = discovery.getPlayerCharID(TargetLocator)
    Code = 'SetPermanentExclusion(%d);AddRewardGroup("%s")' % (int(Target), RewardTable)
    Orientation = GetObjectOrientation(GangMasterAdapter)
    discovery.SpawnObject(Item, consolevar.ConID, Position, Orientation, Code)
    return


PANDORA_BOX_RANGE = 500

def OnSequence47Wave1Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora2BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence47Wave2Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora2BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence47Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora2BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'PANDORA_TABLES_Pandora2PSR', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence47Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora2BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_PANDORA_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence48Wave0Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora3BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence48Wave1Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora3BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence48Wave1Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora3BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence48Wave2Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora3BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence48Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora3BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'PANDORA_TABLES_Pandora3PSR', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence48Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora3BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_PANDORA_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence49Wave0Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora1BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence49Wave1Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora1BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence49Wave1Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora1BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence49Wave2Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora1BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence49Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora1BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'PANDORA_TABLES_Pandora1PSR', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence49Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora1BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_PANDORA_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence50Wave0Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence50Wave1Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence50Wave1Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence50Wave2Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence50Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'PANDORA_TABLES_Pandora4PSR', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence50Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_PANDORA_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence51Wave0Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave0Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave1Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave1Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave1Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave2Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave2Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave2Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave3Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave3Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave3Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave4Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave4Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave4Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave5Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave5Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave5Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave6Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave6Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave6Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave7Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave7Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave7Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave8Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Wave8Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave8Expired(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxWaitNextWave')
    return


def OnSequence51Wave9Started(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    box.StateMachine.setState('BoxPortalSecondaryState')
    return


def OnSequence51Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('Pandora4BoxWorld'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'PANDORA_TABLES_Pandora4PSR', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence58Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    Item = discovery.getGameObjectType('PandoraRewardContainer')
    TargetLocator = GangMasterAdapter.TargetLocator
    Target = discovery.getPlayerCharID(TargetLocator)
    Position = GetObjectPosition(GangMasterAdapter)
    Position.y -= 2 * (discovery.getGameObjectPropValue(discovery.getGameObjectType('PlayerCharacter'), 'HalfExtents').y - discovery.getGameObjectPropValue(Item, 'HalfExtents').y)
    Code = 'SetPermanentExclusion(%d);AddRewardGroup("%s")' % (int(Target), 'EVENT_TABLES_Halloween2')
    Orientation = GetObjectOrientation(GangMasterAdapter)
    discovery.SpawnObject(Item, consolevar.ConID, Position, Orientation, Code)
    return


def SpawnDataNodeReward(GangMasterAdapter, Code):
    if GangMasterAdapter is None:
        return
    Item = discovery.getGameObjectType('DataNodeContainer')
    TargetLocator = GangMasterAdapter.TargetLocator
    Target = discovery.getPlayerCharID(TargetLocator)
    Position = GetObjectPosition(GangMasterAdapter)
    Position.y -= 2 * (discovery.getGameObjectPropValue(discovery.getGameObjectType('PlayerCharacter'), 'HalfExtents').y - discovery.getGameObjectPropValue(Item, 'HalfExtents').y)
    Orientation = GetObjectOrientation(GangMasterAdapter)
    Code = Code.replace('!d', '%d' % int(Target))
    PlayerObject = discovery.getObjectFromLocator(TargetLocator)
    if PlayerObject is None:
        level = 0
    else:
        level = PlayerObject.Level
    Code = Code.replace('!l', '%d' % int(level))
    discovery.SpawnObject(Item, consolevar.ConID, Position, Orientation, Code)
    return


def OnSequence157Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_DataNodeInfoTable')
    return


def OnSequence158Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_DataNodeExpTable'))
    return


def OnSequence159Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_DataNodeExpTable'))
    return


def OnSequence160Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_RedFrag')
    return


def OnSequence161Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_RedFragAll1_4')
    return


def OnSequence162Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_RedFragAll1_4')
    return


def OnSequence163Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_RedFragAll1_4')
    return


def OnSequence164Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_RedFragAll1_4')
    return


def OnSequence165Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_RedFragRandomTLR', 'DATANODETABLES_RedFragRandomTLR'))
    return


def OnSequence166Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_PortableSignalBooster')
    return


def OnSequence167Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_DataNodeExpTable', 'DATANODETABLES_RedFragAll1_4'))
    return


def OnSequence168Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_DataNodeExpTable', 'DATANODETABLES_RedFragAll1_4'))
    return


def OnSequence169Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_Extractor6RecipeFrag')
    return


def OnSequence170Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_CommonSetFull'))
    return


def OnSequence171Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeExpTable', 'DATANODETABLES_RedFragAll'))
    return


def OnSequence172Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_Quicksilver')
    return


def OnSequence173Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_Extractor7RecipeFrag')
    return


def OnSequence174Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_Extractor7ExtraFrag')
    return


def OnSequence175Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_RedFragAllRandomTLR', 'DATANODETABLES_RandomLuggableTLR'))
    return


def OnSequence176Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'DATANODETABLES_CodeOptimizer')
    return


def OnSequence177Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l);AddRewardGroup("%s", !l)' % ('DATANODETABLES_DataNodeInfoTable', 'DATANODETABLES_Overdrive'))
    return


def OnSequence281Completed(GangMasterAdapter=None):
    num = random.randrange(0, 10)
    if num == 0:
        OnSequence169Completed(GangMasterAdapter)
    elif num == 1:
        OnSequence170Completed(GangMasterAdapter)
    elif num == 2:
        OnSequence171Completed(GangMasterAdapter)
    elif num == 3:
        OnSequence172Completed(GangMasterAdapter)
    elif num == 4:
        OnSequence173Completed(GangMasterAdapter)
    elif num == 5:
        OnSequence174Completed(GangMasterAdapter)
    elif num == 6:
        OnSequence175Completed(GangMasterAdapter)
    elif num == 7:
        OnSequence176Completed(GangMasterAdapter)
    elif num == 8:
        OnSequence177Completed(GangMasterAdapter)
    elif GangMasterAdapter is None:
        return
    SpawnDataNodeReward(GangMasterAdapter, 'SetPermanentExclusion(!d);AddRewardGroup("%s", !l)' % 'SPECIAL_TABLE_Gun')
    return


def OnSequence189Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldInit'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiKunoToyBoxInitRew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence189Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldInit'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence190Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldMast'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiKunoToyBoxMastRew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence190Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldMast'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence191Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldGran'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiKunoToyBoxGranRew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence191Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldGran'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence192Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldDaimyo'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiKunoToyBoxDaimRew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence192Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldDaimyo'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence193Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldPink'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiKunoToyBoxPinkRew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence193Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('KunoToyBoxWorldPink'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence194Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('SleepToyBoxWorld1'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiSleepToyBox1Rew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence194Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('SleepToyBoxWorld1'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence195Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('SleepToyBoxWorld2'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiSleepToyBox2Rew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence195Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('SleepToyBoxWorld2'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


def OnSequence196Completed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('SleepToyBoxWorld3'), PANDORA_BOX_RANGE)
    if box is None:
        return
    SpawnPandoraReward(GangMasterAdapter, 'QUESTLOOT_SatiSleepToyBox3Rew', box.Position)
    box.StateMachine.setState('BoxFinishedState')
    return


def OnSequence196Failed(GangMasterAdapter=None):
    if GangMasterAdapter is None:
        return
    box = GetNearestBox(GangMasterAdapter, discovery.getGameObjectType('SleepToyBoxWorld3'), PANDORA_BOX_RANGE)
    if box is None:
        return
    if GangMasterAdapter.TargetLocator is not None:
        discovery.clientSystemMessage(GangMasterAdapter.TargetLocator, ST.ID_TOY_BOX_SEQUENCE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
    box.StateMachine.setState('BoxCleanupState')
    return


return

# okay decompiling work/monolith_pyc\sequence.pyc
