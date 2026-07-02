namespace Aethiumian.AI.Randomization
{
    public readonly struct RandomSourceCreateContext
    {
        public readonly RandomSourceScope Scope;
        public readonly int SeedSalt;

        public RandomSourceCreateContext(RandomSourceScope scope, int seedSalt)
        {
            Scope = scope;
            SeedSalt = seedSalt;
        }
    }
}
