namespace HeadlessClient;
public static class ConditionalThrowFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,1112,279,282};
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0",
        ["MXO_CONDITIONAL_THROW_QA"]="1"};
}
