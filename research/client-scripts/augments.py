# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\augments.py
# Compiled at: 2006-03-30 16:29:38
import traceback, obj
HatAugments = []
GlassesAugments = []
ShirtAugments = []
GlovesAugments = []
CoatAugments = []
PantsAugments = []
ShoesAugments = []
WeaponAugments = []
HackerToolAugments = []
MasterAugmentTable = [[], HatAugments, GlassesAugments, ShirtAugments, GlovesAugments, CoatAugments, PantsAugments, [], ShoesAugments, WeaponAugments, HackerToolAugments]

def GetAugmentTable(category):
    if category >= len(MasterAugmentTable):
        return []
    return MasterAugmentTable[category]
    return


return

# okay decompiling work/monolith_pyc\augments.pyc
