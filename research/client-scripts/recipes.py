# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\recipes.py
# Compiled at: 2006-03-30 16:29:39
import obj
Cookbook = (
 (
  obj.CodeFragmentB, obj.CodeFragmentC, obj.TapDataNodeAbility), (obj.CodeFragmentB, obj.CodeFragmentC, obj.CodeFragmentC, obj.AnalyzeWeaknessTool))

def CraftRecipe(*ingredients):
    """ingredients is a tuple of Game Obj IDs.  Return value is new Game Obj ID (zero for failure)."""
    globs = globals()
    for recipe in Cookbook:
        if len(recipe) - 1 == len(ingredients):
            discovery.serverPrint('SP: Crafting - Testing Recipe')
            if ingredients == recipe[:len(ingredients)]:
                a_str = 'SP: Crafting - Recipe '
                for item in recipe:
                    type_info = discovery.getTypeInfo(item)
                    a_str = a_str + type_info['localname']
                    a_str = a_str + '\n '

                discovery.serverPrint(a_str)
                return recipe[len(recipe) - 1]

    discovery.serverPrint('SP: Crafting - No Matching Recipe Found')
    return 0
    return


def GetRecipesForWriteCodeLevel(level):
    """ find the recipes for level """
    obj_list = []
    for recipe in Cookbook:
        item = recipe[len(recipe) - 1]
        type_info = discovery.getTypeInfo(item)
        if type_info and type_info.has_key('Complexity'):
            if level < type_info.Complexity:
                obj_list.append(item)

    return obj_list
    return


def GetRecipesForObject(obj):
    """ find the recipe for the object """
    print 'getting recipe for ', obj
    for recipe in Cookbook:
        if obj == recipe[len(recipe) - 1]:
            foo = recipe[0:len(recipe) - 1]
            print 'Get recipe : ', recipe[0:len(recipe) - 1], ' type : ', type(recipe[0:len(recipe) - 1])
            return foo

    return None
    return


def debugTest():
    discovery.outputDebugString('-------------------------------------------------\n')
    discovery.outputDebugString('python- code fragment recipes loaded.\n')
    discovery.outputDebugString('-------------------------------------------------\n')
    discovery.serverPrint('SP: Code Fragment Recipes Functioning')
    return


def WriteOutCookbook():
    file = open('c:/matrix/game/matrix/resource/Python/Monolith/clientcookbook.txt', 'w')
    for recipe in Cookbook:
        item = recipe[len(recipe) - 1]
        file.write('%i ' % item)
        for i in range(0, len(recipe) - 1):
            file.write('%i ' % recipe[i])

        file.write('\n')

    file.close()
    return


return

# okay decompiling work/monolith_pyc\recipes.pyc
