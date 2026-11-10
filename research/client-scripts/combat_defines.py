# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\combat_defines.py
# Compiled at: 2006-03-30 16:29:38
COMBAT_CONTROLLED_MASK = constants.combat_prone.LAYING_FACEDOWN | constants.combat_prone.LAYING_FACEUP | constants.combat_prone.VISUAL_FACEDOWN | constants.combat_prone.VISUAL_FACEUP | constants.combat_prone.I_LB_TRANSITION | constants.combat_prone.V_LB_TRANSITION
ABILITY_CONTROLLED_MASK = -1 & ~COMBAT_CONTROLLED_MASK
PRONE_RECOVERY_INSTANT = constants.combat_prone.VISUAL_FACEDOWN | constants.combat_prone.VISUAL_FACEUP | constants.combat_prone.I_LB_TRANSITION | constants.combat_prone.V_LB_TRANSITION
return

# okay decompiling work/monolith_pyc\combat_defines.pyc
