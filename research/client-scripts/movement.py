# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\movement.py
# Compiled at: 2006-03-30 16:29:39
import traceback
demeanors = [
 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 
 19, 20, 21, 22, 23, 24, 25, 26]

def Init():
    globs = globals()
    offset = 0
    for name in demeanors:
        name = name.replace(' ', '')
        name = name.replace('-', '')
        name = 'Demeanor_' + name
        globs[name] = offset
        offset += 1

    return


Init()

def GetDemeanorList():
    return demeanors
    return


return

# okay decompiling work/monolith_pyc\movement.pyc
