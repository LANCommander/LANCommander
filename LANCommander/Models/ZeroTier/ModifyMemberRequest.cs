namespace LANCommander.Models.ZeroTier
{
    public class ModifyMemberRequest
    {
        public bool Hidden { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public MemberConfig Config { get; set; }
    }
}
