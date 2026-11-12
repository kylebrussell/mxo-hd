namespace hds
{
    public sealed class MissionCheckpointException : System.InvalidOperationException
    {
        public MissionCheckpointException(string message):base(message){}
    }
    public class MissionAccountUpdate
    {
        public uint ExperienceCredited {get;set;}
        public uint InfoCredited {get;set;}
        public long ExperienceTotal {get;set;}
        public long InfoTotal {get;set;}

    }
    public sealed class MissionCompletion : MissionAccountUpdate
    {
        public CollectorExchange Exchange {get;set;}
        public bool AlreadyCommitted {get;set;}
    }
}
