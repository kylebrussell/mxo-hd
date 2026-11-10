# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\obj.py
# Compiled at: 2006-03-30 16:29:39
gObjectTypeInfo = {}

def Init():
    objnames = discovery.getAllObjects()
    globs = globals()
    for (name, id) in objnames.iteritems():
        name = name.replace(' ', '')
        name = name.replace('-', '')
        globs[name] = id

    return


def GetTypeInfo(id):
    if id in gObjectTypeInfo:
        return gObjectTypeInfo[id]
    gObjectTypeInfo[id] = discovery.getTypeInfo(id)
    return gObjectTypeInfo[id]
    return


Init()
return

# okay decompiling work/monolith_pyc\obj.pyc
