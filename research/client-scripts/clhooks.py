# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\clhooks.py
# Compiled at: 2008-12-16 02:01:20
import combatvalidate as CV, combat_calculations as CC, ltfxmap as FX, st_neighborhoodnames as NH, stringtable_client as ST, gameFeatures as GF
from random import randint
neighborhood2fx = {(NH.NH_CAMON): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_CAMON_HEIGHTS), (NH.NH_MARA): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MARA), (NH.NH_TABOR): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_TABOR_PARK), (NH.NH_ZIA): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_ZIA), (NH.NH_ACHAN): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_ACHAN), (NH.NH_RIDGE_WAY): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_ROGERS_WAY), (NH.NH_KEDEMOTH): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_KEDEMOTH), (NH.NH_HAMMERSFIELD): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_ESHEAN_PROJECTS), (NH.NH_HAMMERVILLE): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_DANNAH_HEIGHTS), (NH.NH_LOPEZ_POINT): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_LUCERO_POINT), (NH.NH_URIAH): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_URIAH), (NH.NH_PILLSEN): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_PILSEN), (NH.NH_MAGOG): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MAGOG), (NH.NH_APOLLYON): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_APOLLYON), (NH.NH_SOUTHARD): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_SOUTHARD), (NH.NH_FURIHATA): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_FURIHATA), (NH.NH_SOBRAJ_SHORES): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_SOBRA_SHORES), (NH.NH_STANO): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_STAMOS), (NH.NH_KINOSHTA): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MURASAKI), (NH.NH_KAEDE): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_KAEDE), (NH.NH_AKASAKA): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_AKASAKA), (NH.NH_UENO): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_DISTRICT_UENO), (NH.NH_JURONG): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_JURONG), (NH.NH_IKEBUKURO): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_IKEBUKURO), (NH.NH_KOWLOON): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_KOWLOON), (NH.NH_SAI_KUNG): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_SAI_KUNG), (NH.NH_SHINJUKU): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_DISTRICT_SHINJUKU), (NH.NH_SHIRAKABA): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_DISTRICT_SHIRAKABA), (NH.NH_LEMONE): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_LEMONE), (NH.NH_MANSONPARK): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MANSSEN_PARK), (NH.NH_GUNNESSLAKE): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_GUINNESS_LAKE), (NH.NH_MIDIAN_PARK): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MIDIAN_PARK), (NH.NH_GACYHEIGHTS): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_GRACY_HEIGHTS), (NH.NH_MORIAH_PROJECTS): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MORIAH_PROJECTS), (NH.NH_BATHORYROW): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_BATHARY_ROW), (NH.NH_MANNSDALE_PROJECTS): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MANNSDALE), (NH.NH_CRESTON_HEIGHTS): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_CRESTON_HEIGHTS), (NH.NH_INDUSTRY_SQUARE): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_INDUSTRY_SQUARE), (NH.NH_STRATFORD_CAMPUS): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_STRATFORD_CAMPUS), (NH.NH_UNION_HILL): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_UNION_HILL), (NH.NH_BALDWIN_HEIGHTS): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_BALDWIN_HEIGHTS), (NH.NH_PARK_EAST): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_PARK_EAST), (NH.NH_CENTER_PARK): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_CENTER_PARK), (NH.NH_MORRELL): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MORRELL), (NH.NH_LAMAR): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_LAMAR), (NH.NH_HAMPTON_GREEN): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_HAMPTON_GREEN), (NH.NH_MARIBEAU): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_MARIBEAU), (NH.NH_CHELSEA): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_CHELSEA), (NH.NH_HISTORIC_DISTRICT): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_HISTORIC_DISTRICT), (NH.NH_VAUXTON): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_VAUXTON), (NH.NH_SOUTH_VAUXTON): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_SOUTH_VAUXTON), (NH.NH_EDGEWATER): (FX.FX_OBJECT_INTERFACE_NEIGHBORHOOD_NAMES_EDGEWATER)}

def GetNeighborhoodFXID(neighborID):
    if neighborID in neighborhood2fx:
        return neighborhood2fx[neighborID]
    return 0
    return


def CanUseGameFeature(character, gameFeature, subscriptionData):
    if GF.HandleConditional(character, gameFeature, subscriptionData) == True:
        return 1
    return 0
    return


def WriteCode_IsFragmentKnowable(abil_level, fragment):
    discovery.outputDebugString(' is frag known ')
    discovery.outputDebugString(' rarity : %d/%d ' % (fragment.Rarity, abil_level))
    rarity = 0
    if fragment.Rarity is not None:
        rarity = fragment.Rarity
    else:
        print 'Object Has No Rarity'
    if abil_level > 0:
        if abil_level - rarity > 0:
            return 0
    return 0
    return


def GetCinematicCount():
    return 34
    return


def GetCinematicConvertedName(cineIndex):
    if cineIndex == 0:
        return 'cin1_1.cnb'
    elif cineIndex == 1:
        return 'cin1_2.cnb'
    elif cineIndex == 2:
        return 'cin1_3.cnb'
    elif cineIndex == 3:
        return 'cin2_1.cnb'
    elif cineIndex == 4:
        return 'cin2_2.cnb'
    elif cineIndex == 5:
        return 'cin2_3.cnb'
    elif cineIndex == 6:
        return 'cin3_1.cnb'
    elif cineIndex == 7:
        return 'cin3_2.cnb'
    elif cineIndex == 8:
        return 'cin3_3.cnb'
    elif cineIndex == 9:
        return 'cin4_1.cnb'
    elif cineIndex == 10:
        return 'cin4_2.cnb'
    elif cineIndex == 11:
        return 'cin4_3.cnb'
    elif cineIndex == 12:
        return 'cin5_1.cnb'
    elif cineIndex == 13:
        return 'cin5_2.cnb'
    elif cineIndex == 14:
        return 'cin5_3.cnb'
    elif cineIndex == 15:
        return 'cin6_1.cnb'
    elif cineIndex == 16:
        return 'cin6_2.cnb'
    elif cineIndex == 17:
        return 'cin6_3.cnb'
    elif cineIndex == 18:
        return 'cin7_1.cnb'
    elif cineIndex == 19:
        return 'cin7_2.cnb'
    elif cineIndex == 20:
        return 'cin7_3.cnb'
    elif cineIndex == 21:
        return 'cin8_1.cnb'
    elif cineIndex == 22:
        return 'cin8_2.cnb'
    elif cineIndex == 23:
        return 'cin8_3.cnb'
    elif cineIndex == 24:
        return 'cin9_1.cnb'
    elif cineIndex == 25:
        return 'cin9_2.cnb'
    elif cineIndex == 26:
        return 'cin9_3.cnb'
    elif cineIndex == 27:
        return 'cin10_1.cnb'
    elif cineIndex == 28:
        return 'cin10_2.cnb'
    elif cineIndex == 29:
        return 'cin10_3.cnb'
    elif cineIndex == 30:
        return 'cin11_1.cnb'
    elif cineIndex == 31:
        return 'cin11_2.cnb'
    elif cineIndex == 32:
        return 'cin11_3.cnb'
    elif cineIndex == 33:
        return 'cin12_1.cnb'
    return ' '
    return


def GetCinematicName(cineIndex):
    if cineIndex == 0:
        return 'Cinematic 1.1'
    elif cineIndex == 1:
        return 'Cinematic 1.2'
    elif cineIndex == 2:
        return 'Cinematic 1.3'
    elif cineIndex == 3:
        return 'Cinematic 2.1'
    elif cineIndex == 4:
        return 'Cinematic 2.2'
    elif cineIndex == 5:
        return 'Cinematic 2.3'
    elif cineIndex == 6:
        return 'Cinematic 3.1'
    elif cineIndex == 7:
        return 'Cinematic 3.2'
    elif cineIndex == 8:
        return 'Cinematic 3.3'
    elif cineIndex == 9:
        return 'Cinematic 4.1'
    elif cineIndex == 10:
        return 'Cinematic 4.2'
    elif cineIndex == 11:
        return 'Cinematic 4.3'
    elif cineIndex == 12:
        return 'Cinematic 5.1'
    elif cineIndex == 13:
        return 'Cinematic 5.2'
    elif cineIndex == 14:
        return 'Cinematic 5.3'
    elif cineIndex == 15:
        return 'Cinematic 6.1'
    elif cineIndex == 16:
        return 'Cinematic 6.2'
    elif cineIndex == 17:
        return 'Cinematic 6.3'
    elif cineIndex == 18:
        return 'Cinematic 7.1'
    elif cineIndex == 19:
        return 'Cinematic 7.2'
    elif cineIndex == 20:
        return 'Cinematic 7.3'
    elif cineIndex == 21:
        return 'Cinematic 8.1'
    elif cineIndex == 22:
        return 'Cinematic 8.2'
    elif cineIndex == 23:
        return 'Cinematic 8.3'
    elif cineIndex == 24:
        return 'Cinematic 9.1'
    elif cineIndex == 25:
        return 'Cinematic 9.2'
    elif cineIndex == 26:
        return 'Cinematic 9.3'
    elif cineIndex == 27:
        return 'Chapter 10.1'
    elif cineIndex == 28:
        return 'Chapter 10.2'
    elif cineIndex == 29:
        return 'Chapter 10.3'
    elif cineIndex == 30:
        return 'Chapter 11.1'
    elif cineIndex == 31:
        return 'Chapter 11.2'
    elif cineIndex == 32:
        return 'Cinematic 11.3'
    elif cineIndex == 33:
        return 'Cinematic 12.1'
    return 'Invalid Cinematic'
    return


def GetCinematicImage(cineIndex):
    if cineIndex == 0:
        return 'resource\\cinematics\\cin1_1.jpg'
    elif cineIndex == 1:
        return 'resource\\cinematics\\cin1_2.jpg'
    elif cineIndex == 2:
        return 'resource\\cinematics\\cin1_3.jpg'
    elif cineIndex == 3:
        return 'resource\\cinematics\\cin2_1.jpg'
    elif cineIndex == 4:
        return 'resource\\cinematics\\cin2_2.jpg'
    elif cineIndex == 5:
        return 'resource\\cinematics\\cin2_3.jpg'
    elif cineIndex == 6:
        return 'resource\\cinematics\\cin3_1.jpg'
    elif cineIndex == 7:
        return 'resource\\cinematics\\cin3_2.jpg'
    elif cineIndex == 8:
        return 'resource\\cinematics\\cin3_3.jpg'
    elif cineIndex == 9:
        return 'resource\\cinematics\\cin4_1.jpg'
    elif cineIndex == 10:
        return 'resource\\cinematics\\cin4_2.jpg'
    elif cineIndex == 11:
        return 'resource\\cinematics\\cin4_3.jpg'
    elif cineIndex == 12:
        return 'resource\\cinematics\\cin5_1.jpg'
    elif cineIndex == 13:
        return 'resource\\cinematics\\cin5_2.jpg'
    elif cineIndex == 14:
        return 'resource\\cinematics\\cin5_3.jpg'
    elif cineIndex == 15:
        return 'resource\\cinematics\\cin6_1.jpg'
    elif cineIndex == 16:
        return 'resource\\cinematics\\cin6_2.jpg'
    elif cineIndex == 17:
        return 'resource\\cinematics\\cin6_3.jpg'
    elif cineIndex == 18:
        return 'resource\\cinematics\\cin7_1.jpg'
    elif cineIndex == 19:
        return 'resource\\cinematics\\cin7_2.jpg'
    elif cineIndex == 20:
        return 'resource\\cinematics\\cin7_3.jpg'
    elif cineIndex == 21:
        return 'resource\\cinematics\\cin8_1.jpg'
    elif cineIndex == 22:
        return 'resource\\cinematics\\cin8_2.jpg'
    elif cineIndex == 23:
        return 'resource\\cinematics\\cin8_3.jpg'
    elif cineIndex == 24:
        return 'resource\\cinematics\\cin9_1.jpg'
    elif cineIndex == 25:
        return 'resource\\cinematics\\cin9_2.jpg'
    elif cineIndex == 26:
        return 'resource\\cinematics\\cin9_3.jpg'
    elif cineIndex == 27:
        return 'resource\\cinematics\\cin10_1.jpg'
    elif cineIndex == 28:
        return 'resource\\cinematics\\cin10_2.jpg'
    elif cineIndex == 29:
        return 'resource\\cinematics\\cin10_3.jpg'
    elif cineIndex == 30:
        return 'resource\\cinematics\\cin11_1.jpg'
    elif cineIndex == 31:
        return 'resource\\cinematics\\cin11_2.jpg'
    elif cineIndex == 32:
        return 'resource\\cinematics\\cin11_3.jpg'
    elif cineIndex == 33:
        return 'resource\\cinematics\\cin12_1.jpg'
    return ' '
    return


def GetCinematicURLName(cineIndex):
    if cineIndex == 0:
        return '1.1_high.wmv'
    elif cineIndex == 1:
        return '1.2_high.wmv'
    elif cineIndex == 2:
        return '1.3_high.wmv'
    elif cineIndex == 3:
        return '2.1_high.wmv'
    elif cineIndex == 4:
        return '2.2_high.wmv'
    elif cineIndex == 5:
        return '2.3_high.wmv'
    elif cineIndex == 6:
        return '3.1_high.wmv'
    elif cineIndex == 7:
        return '3.2_high.wmv'
    elif cineIndex == 8:
        return '3.3_high.wmv'
    elif cineIndex == 9:
        return '4.1_high.wmv'
    elif cineIndex == 10:
        return '4.2_high.wmv'
    elif cineIndex == 11:
        return '4.3_high.wmv'
    elif cineIndex == 12:
        return '5.1_high.wmv'
    elif cineIndex == 13:
        return '5.2_high.wmv'
    elif cineIndex == 14:
        return '5.3_high.wmv'
    elif cineIndex == 15:
        return '6.1_high.wmv'
    elif cineIndex == 16:
        return '6.2_high.wmv'
    elif cineIndex == 17:
        return '6.3_high.wmv'
    elif cineIndex == 18:
        return '7.1_high.wmv'
    elif cineIndex == 19:
        return '7.2_high.wmv'
    elif cineIndex == 20:
        return '7.3_high.wmv'
    elif cineIndex == 21:
        return '8.1_high.wmv'
    elif cineIndex == 22:
        return '8.2_high.wmv'
    elif cineIndex == 23:
        return '8.3_high.wmv'
    elif cineIndex == 24:
        return '9.1_high.wmv'
    elif cineIndex == 25:
        return '9.2_high.wmv'
    elif cineIndex == 26:
        return '9.3_high.wmv'
    elif cineIndex == 32:
        return 'cin11_3.html'
    elif cineIndex == 33:
        return 'cin12_1.html'
    return 'DCU'
    return


DEFAULT_VENDOR_PRICE = 1000

def RecycleInfo(itemTypeID):
    if not itemTypeID:
        return 0
    recycle = discovery.getGameObjectPropValue(itemTypeID, 'VendorPrice')
    if recycle is None:
        recycle = DEFAULT_VENDOR_PRICE
    return int(recycle / 20.0)
    return


RWStringLookup = {'DEFAULT': [ST.ID_BLUEPILL_DIALOGUE_GENERIC_0, ST.ID_BLUEPILL_DIALOGUE_GENERIC_1, ST.ID_BLUEPILL_DIALOGUE_GENERIC_2, ST.ID_BLUEPILL_DIALOGUE_GENERIC_3, ST.ID_BLUEPILL_DIALOGUE_GENERIC_4, ST.ID_BLUEPILL_DIALOGUE_GENERIC_5, ST.ID_BLUEPILL_DIALOGUE_GENERIC_6, ST.ID_BLUEPILL_DIALOGUE_GENERIC_7, ST.ID_BLUEPILL_DIALOGUE_GENERIC_8, ST.ID_BLUEPILL_DIALOGUE_GENERIC_9, ST.ID_BLUEPILL_DIALOGUE_GENERIC_10, ST.ID_BLUEPILL_DIALOGUE_GENERIC_11, ST.ID_BLUEPILL_DIALOGUE_GENERIC_12, ST.ID_BLUEPILL_DIALOGUE_GENERIC_13, ST.ID_BLUEPILL_DIALOGUE_GENERIC_14, ST.ID_BLUEPILL_DIALOGUE_GENERIC_15, ST.ID_BLUEPILL_DIALOGUE_GENERIC_16, ST.ID_BLUEPILL_DIALOGUE_GENERIC_17, ST.ID_BLUEPILL_DIALOGUE_GENERIC_18, ST.ID_BLUEPILL_DIALOGUE_GENERIC_19, ST.ID_BLUEPILL_DIALOGUE_GENERIC_20, ST.ID_BLUEPILL_DIALOGUE_GENERIC_21, ST.ID_BLUEPILL_DIALOGUE_GENERIC_22, ST.ID_BLUEPILL_DIALOGUE_GENERIC_23, ST.ID_BLUEPILL_DIALOGUE_GENERIC_24, ST.ID_BLUEPILL_DIALOGUE_GENERIC_25, ST.ID_BLUEPILL_DIALOGUE_GENERIC_26, ST.ID_BLUEPILL_DIALOGUE_GENERIC_27, ST.ID_BLUEPILL_DIALOGUE_GENERIC_28, ST.ID_BLUEPILL_DIALOGUE_GENERIC_29, ST.ID_BLUEPILL_DIALOGUE_GENERIC_30, ST.ID_BLUEPILL_DIALOGUE_GENERIC_31, ST.ID_BLUEPILL_DIALOGUE_GENERIC_32, ST.ID_BLUEPILL_DIALOGUE_GENERIC_33, ST.ID_BLUEPILL_DIALOGUE_GENERIC_34, ST.ID_BLUEPILL_DIALOGUE_GENERIC_35, ST.ID_BLUEPILL_DIALOGUE_GENERIC_36, ST.ID_BLUEPILL_DIALOGUE_GENERIC_37, ST.ID_BLUEPILL_DIALOGUE_GENERIC_38, ST.ID_BLUEPILL_DIALOGUE_GENERIC_39, ST.ID_BLUEPILL_DIALOGUE_GENERIC_40, ST.ID_BLUEPILL_DIALOGUE_GENERIC_41, ST.ID_BLUEPILL_DIALOGUE_GENERIC_42, ST.ID_BLUEPILL_DIALOGUE_GENERIC_43, ST.ID_BLUEPILL_DIALOGUE_GENERIC_44, ST.ID_BLUEPILL_DIALOGUE_GENERIC_45, ST.ID_BLUEPILL_DIALOGUE_GENERIC_46, ST.ID_BLUEPILL_DIALOGUE_GENERIC_47, ST.ID_BLUEPILL_DIALOGUE_GENERIC_48, ST.ID_BLUEPILL_DIALOGUE_GENERIC_49, ST.ID_BLUEPILL_DIALOGUE_GENERIC_50, ST.ID_BLUEPILL_DIALOGUE_GENERIC_51, ST.ID_BLUEPILL_DIALOGUE_GENERIC_52, ST.ID_BLUEPILL_DIALOGUE_GENERIC_53, ST.ID_BLUEPILL_DIALOGUE_GENERIC_54, ST.ID_BLUEPILL_DIALOGUE_GENERIC_55, ST.ID_BLUEPILL_DIALOGUE_GENERIC_56, ST.ID_BLUEPILL_DIALOGUE_GENERIC_57, ST.ID_BLUEPILL_DIALOGUE_GENERIC_58, ST.ID_BLUEPILL_DIALOGUE_GENERIC_59, ST.ID_BLUEPILL_DIALOGUE_GENERIC_60, ST.ID_BLUEPILL_DIALOGUE_GENERIC_61, ST.ID_BLUEPILL_DIALOGUE_GENERIC_62, ST.ID_BLUEPILL_DIALOGUE_GENERIC_63, ST.ID_BLUEPILL_DIALOGUE_GENERIC_64, ST.ID_BLUEPILL_DIALOGUE_GENERIC_65, ST.ID_BLUEPILL_DIALOGUE_GENERIC_66, ST.ID_BLUEPILL_DIALOGUE_GENERIC_67, ST.ID_BLUEPILL_DIALOGUE_GENERIC_68, ST.ID_BLUEPILL_DIALOGUE_GENERIC_69, ST.ID_BLUEPILL_DIALOGUE_GENERIC_70, ST.ID_BLUEPILL_DIALOGUE_GENERIC_71, ST.ID_BLUEPILL_DIALOGUE_GENERIC_72, ST.ID_BLUEPILL_DIALOGUE_GENERIC_73, ST.ID_BLUEPILL_DIALOGUE_GENERIC_74, ST.ID_BLUEPILL_DIALOGUE_GENERIC_75, ST.ID_BLUEPILL_DIALOGUE_GENERIC_76, ST.ID_BLUEPILL_DIALOGUE_GENERIC_77, ST.ID_BLUEPILL_DIALOGUE_GENERIC_78, ST.ID_BLUEPILL_DIALOGUE_GENERIC_79, ST.ID_BLUEPILL_DIALOGUE_GENERIC_80, ST.ID_BLUEPILL_DIALOGUE_GENERIC_81, ST.ID_BLUEPILL_DIALOGUE_GENERIC_82, ST.ID_BLUEPILL_DIALOGUE_GENERIC_83, ST.ID_BLUEPILL_DIALOGUE_GENERIC_84, ST.ID_BLUEPILL_DIALOGUE_GENERIC_85, ST.ID_BLUEPILL_DIALOGUE_GENERIC_86, ST.ID_BLUEPILL_DIALOGUE_GENERIC_87, ST.ID_BLUEPILL_DIALOGUE_GENERIC_88, ST.ID_BLUEPILL_DIALOGUE_GENERIC_89, ST.ID_BLUEPILL_DIALOGUE_GENERIC_90, ST.ID_BLUEPILL_DIALOGUE_GENERIC_91, ST.ID_BLUEPILL_DIALOGUE_GENERIC_92, ST.ID_BLUEPILL_DIALOGUE_GENERIC_93, ST.ID_BLUEPILL_DIALOGUE_GENERIC_94, ST.ID_BLUEPILL_DIALOGUE_GENERIC_95, ST.ID_BLUEPILL_DIALOGUE_GENERIC_96, ST.ID_BLUEPILL_DIALOGUE_GENERIC_97, ST.ID_BLUEPILL_DIALOGUE_GENERIC_98, ST.ID_BLUEPILL_DIALOGUE_GENERIC_99, ST.ID_BLUEPILL_DIALOGUE_GENERIC_100, ST.ID_BLUEPILL_DIALOGUE_GENERIC_101, ST.ID_BLUEPILL_DIALOGUE_GENERIC_102, ST.ID_BLUEPILL_DIALOGUE_GENERIC_103, ST.ID_BLUEPILL_DIALOGUE_GENERIC_104, ST.ID_BLUEPILL_DIALOGUE_GENERIC_105, ST.ID_BLUEPILL_DIALOGUE_GENERIC_106, ST.ID_BLUEPILL_DIALOGUE_GENERIC_107, ST.ID_BLUEPILL_DIALOGUE_GENERIC_108, ST.ID_BLUEPILL_DIALOGUE_GENERIC_109, ST.ID_BLUEPILL_DIALOGUE_GENERIC_110, ST.ID_BLUEPILL_DIALOGUE_GENERIC_111, ST.ID_BLUEPILL_DIALOGUE_GENERIC_112, ST.ID_BLUEPILL_DIALOGUE_GENERIC_113, ST.ID_BLUEPILL_DIALOGUE_GENERIC_114, ST.ID_BLUEPILL_DIALOGUE_GENERIC_115, ST.ID_BLUEPILL_DIALOGUE_GENERIC_116, ST.ID_BLUEPILL_DIALOGUE_GENERIC_117, ST.ID_BLUEPILL_DIALOGUE_GENERIC_118, ST.ID_BLUEPILL_DIALOGUE_GENERIC_119, ST.ID_BLUEPILL_DIALOGUE_GENERIC_120, ST.ID_BLUEPILL_DIALOGUE_GENERIC_121, ST.ID_BLUEPILL_DIALOGUE_GENERIC_122, ST.ID_BLUEPILL_DIALOGUE_GENERIC_123, ST.ID_BLUEPILL_DIALOGUE_GENERIC_124, ST.ID_BLUEPILL_DIALOGUE_GENERIC_125, ST.ID_BLUEPILL_DIALOGUE_GENERIC_126, ST.ID_BLUEPILL_DIALOGUE_GENERIC_127, ST.ID_BLUEPILL_DIALOGUE_GENERIC_128, ST.ID_BLUEPILL_DIALOGUE_GENERIC_129, ST.ID_BLUEPILL_DIALOGUE_GENERIC_130, ST.ID_BLUEPILL_DIALOGUE_GENERIC_131, ST.ID_BLUEPILL_DIALOGUE_GENERIC_132, ST.ID_BLUEPILL_DIALOGUE_GENERIC_133, ST.ID_BLUEPILL_DIALOGUE_GENERIC_134, ST.ID_BLUEPILL_DIALOGUE_GENERIC_135, ST.ID_BLUEPILL_DIALOGUE_GENERIC_136, ST.ID_BLUEPILL_DIALOGUE_GENERIC_137, ST.ID_BLUEPILL_DIALOGUE_GENERIC_138, ST.ID_BLUEPILL_DIALOGUE_GENERIC_139, ST.ID_BLUEPILL_DIALOGUE_GENERIC_140, ST.ID_BLUEPILL_DIALOGUE_GENERIC_141, ST.ID_BLUEPILL_DIALOGUE_GENERIC_142, ST.ID_BLUEPILL_DIALOGUE_GENERIC_143, ST.ID_BLUEPILL_DIALOGUE_GENERIC_144, ST.ID_BLUEPILL_DIALOGUE_GENERIC_145, ST.ID_BLUEPILL_DIALOGUE_GENERIC_146, ST.ID_BLUEPILL_DIALOGUE_GENERIC_147, ST.ID_BLUEPILL_DIALOGUE_GENERIC_148, ST.ID_BLUEPILL_DIALOGUE_GENERIC_149, ST.ID_BLUEPILL_DIALOGUE_GENERIC_150, ST.ID_BLUEPILL_DIALOGUE_GENERIC_151, ST.ID_BLUEPILL_DIALOGUE_GENERIC_152, ST.ID_BLUEPILL_DIALOGUE_GENERIC_153, ST.ID_BLUEPILL_DIALOGUE_GENERIC_154, ST.ID_BLUEPILL_DIALOGUE_GENERIC_155, ST.ID_BLUEPILL_DIALOGUE_GENERIC_156, ST.ID_BLUEPILL_DIALOGUE_GENERIC_157, ST.ID_BLUEPILL_DIALOGUE_GENERIC_158, ST.ID_BLUEPILL_DIALOGUE_GENERIC_159, ST.ID_BLUEPILL_DIALOGUE_GENERIC_160, ST.ID_BLUEPILL_DIALOGUE_GENERIC_161, ST.ID_BLUEPILL_DIALOGUE_GENERIC_162, ST.ID_BLUEPILL_DIALOGUE_GENERIC_163, ST.ID_BLUEPILL_DIALOGUE_GENERIC_164, ST.ID_BLUEPILL_DIALOGUE_GENERIC_165, ST.ID_BLUEPILL_DIALOGUE_GENERIC_166, ST.ID_BLUEPILL_DIALOGUE_GENERIC_167, ST.ID_BLUEPILL_DIALOGUE_GENERIC_168, ST.ID_BLUEPILL_DIALOGUE_GENERIC_169, ST.ID_BLUEPILL_DIALOGUE_GENERIC_170, ST.ID_BLUEPILL_DIALOGUE_GENERIC_171, ST.ID_BLUEPILL_DIALOGUE_GENERIC_172, ST.ID_BLUEPILL_DIALOGUE_GENERIC_173, ST.ID_BLUEPILL_DIALOGUE_GENERIC_174, ST.ID_BLUEPILL_DIALOGUE_GENERIC_175, ST.ID_BLUEPILL_DIALOGUE_GENERIC_176, ST.ID_BLUEPILL_DIALOGUE_GENERIC_177, ST.ID_BLUEPILL_DIALOGUE_GENERIC_178, ST.ID_BLUEPILL_DIALOGUE_GENERIC_179, ST.ID_BLUEPILL_DIALOGUE_GENERIC_180, ST.ID_BLUEPILL_DIALOGUE_GENERIC_181, ST.ID_BLUEPILL_DIALOGUE_GENERIC_182, ST.ID_BLUEPILL_DIALOGUE_GENERIC_183, ST.ID_BLUEPILL_DIALOGUE_GENERIC_184, ST.ID_BLUEPILL_DIALOGUE_GENERIC_185, ST.ID_BLUEPILL_DIALOGUE_GENERIC_186, ST.ID_BLUEPILL_DIALOGUE_GENERIC_187, ST.ID_BLUEPILL_DIALOGUE_GENERIC_188, ST.ID_BLUEPILL_DIALOGUE_GENERIC_189, ST.ID_BLUEPILL_DIALOGUE_GENERIC_190, ST.ID_BLUEPILL_DIALOGUE_GENERIC_191, ST.ID_BLUEPILL_DIALOGUE_GENERIC_192, ST.ID_BLUEPILL_DIALOGUE_GENERIC_193, ST.ID_BLUEPILL_DIALOGUE_GENERIC_194, ST.ID_BLUEPILL_DIALOGUE_GENERIC_195, ST.ID_BLUEPILL_DIALOGUE_GENERIC_196, ST.ID_BLUEPILL_DIALOGUE_GENERIC_197, ST.ID_BLUEPILL_DIALOGUE_GENERIC_198, ST.ID_BLUEPILL_DIALOGUE_GENERIC_199, ST.ID_BLUEPILL_DIALOGUE_GENERIC_200, ST.ID_BLUEPILL_DIALOGUE_GENERIC_201, ST.ID_BLUEPILL_DIALOGUE_GENERIC_202, ST.ID_BLUEPILL_DIALOGUE_GENERIC_203, ST.ID_BLUEPILL_DIALOGUE_GENERIC_204, ST.ID_BLUEPILL_DIALOGUE_GENERIC_205, ST.ID_BLUEPILL_DIALOGUE_GENERIC_206, ST.ID_BLUEPILL_DIALOGUE_GENERIC_207, ST.ID_BLUEPILL_DIALOGUE_GENERIC_208, ST.ID_BLUEPILL_DIALOGUE_GENERIC_209, ST.ID_BLUEPILL_DIALOGUE_GENERIC_210, ST.ID_BLUEPILL_DIALOGUE_GENERIC_211, ST.ID_BLUEPILL_DIALOGUE_GENERIC_212, ST.ID_BLUEPILL_DIALOGUE_GENERIC_213, ST.ID_BLUEPILL_DIALOGUE_GENERIC_214, ST.ID_BLUEPILL_DIALOGUE_GENERIC_215, ST.ID_BLUEPILL_DIALOGUE_GENERIC_216, ST.ID_BLUEPILL_DIALOGUE_GENERIC_217, ST.ID_BLUEPILL_DIALOGUE_GENERIC_218, ST.ID_BLUEPILL_DIALOGUE_GENERIC_219, ST.ID_BLUEPILL_DIALOGUE_GENERIC_220, ST.ID_BLUEPILL_DIALOGUE_GENERIC_221, ST.ID_BLUEPILL_DIALOGUE_GENERIC_222, ST.ID_BLUEPILL_DIALOGUE_GENERIC_223, ST.ID_BLUEPILL_DIALOGUE_GENERIC_224, ST.ID_BLUEPILL_DIALOGUE_GENERIC_225, ST.ID_BLUEPILL_DIALOGUE_GENERIC_226, ST.ID_BLUEPILL_DIALOGUE_GENERIC_227, ST.ID_BLUEPILL_DIALOGUE_GENERIC_228, ST.ID_BLUEPILL_DIALOGUE_GENERIC_229, ST.ID_BLUEPILL_DIALOGUE_GENERIC_230, ST.ID_BLUEPILL_DIALOGUE_GENERIC_231, ST.ID_BLUEPILL_DIALOGUE_GENERIC_232, ST.ID_BLUEPILL_DIALOGUE_GENERIC_233, ST.ID_BLUEPILL_DIALOGUE_GENERIC_234, ST.ID_BLUEPILL_DIALOGUE_GENERIC_235, ST.ID_BLUEPILL_DIALOGUE_GENERIC_236, ST.ID_BLUEPILL_DIALOGUE_GENERIC_237, ST.ID_BLUEPILL_DIALOGUE_GENERIC_238, ST.ID_BLUEPILL_DIALOGUE_GENERIC_239, ST.ID_BLUEPILL_DIALOGUE_GENERIC_240, ST.ID_BLUEPILL_DIALOGUE_GENERIC_241, ST.ID_BLUEPILL_DIALOGUE_GENERIC_242, ST.ID_BLUEPILL_DIALOGUE_GENERIC_243, ST.ID_BLUEPILL_DIALOGUE_GENERIC_244, ST.ID_BLUEPILL_DIALOGUE_GENERIC_245, ST.ID_BLUEPILL_DIALOGUE_GENERIC_246, ST.ID_BLUEPILL_DIALOGUE_GENERIC_247, ST.ID_BLUEPILL_DIALOGUE_GENERIC_248], 'DEV_EE': [ST.ID_BLUEPILL_DIALOGUE_DEV_EE_0, ST.ID_BLUEPILL_DIALOGUE_DEV_EE_1]}

def GetRWTalkerString(typeStr):
    talkerStringID = ST.ID_ERROR
    if randint(1, 1000) == 1:
        pass
    if typeStr in RWStringLookup:
        stringList = RWStringLookup[typeStr]
        idx = randint(0, len(stringList) - 1)
        talkerStringID = stringList[idx]
    return talkerStringID
    return


def FormatObjectProperties(gameobj):
    props = gameobj.getProps()
    props.sort()
    d = [
     '************* START Client version of object **************']
    d.append('GameObject_Type ' + str(gameobj.type))
    for prop in props:
        if not '/' in prop:
            d.append(prop + ': ' + str(getattr(gameobj, prop)))

    d.append('************* END Client version of object **************')
    return d
    return


start_msg = '-- Loaded CLHooks '
discovery.outputDebugString(start_msg)
discovery.serverPrint(start_msg)
return

# okay decompiling work/monolith_pyc\clhooks.pyc
