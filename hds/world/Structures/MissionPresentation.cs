using System;
namespace hds
{
    public static class MissionPresentation
    {
        // OURS visible fallback.80a1 and its flags remain untouched; bit4's native history is not general chat.
        public static string ChatFallback(bool hostile,bool interactive,string speaker,string text) =>
            hostile && !interactive && !string.IsNullOrEmpty(text) ? speaker+": "+text : null;
        public static bool QaAllowed(string dev,string database,uint character,bool onWorld,bool unavailable) =>
            character==110 && QaLoadout.Allowed(dev,database,character,onWorld,unavailable);
    }
}
