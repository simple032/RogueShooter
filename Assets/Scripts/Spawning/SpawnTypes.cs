namespace RogueShooter.Spawning
{
    public struct SpawnMember
    {
        public string KindId;
        public int Count;
    }

    public struct SpawnGroupDef
    {
        public string GroupId;
        public string Intent;
        public int SumP0;
        public SpawnMember[] Members;
    }
}
