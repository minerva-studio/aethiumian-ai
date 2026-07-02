namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Base class of all repeat services
    /// </summary>
    public abstract class RepeatService : Service
    {
        public int interval;

        private int currentFrame;

        public override bool IsReady => currentFrame >= interval;

        public override void UpdateTimer()
        {
            currentFrame++;
        }

        public void ResetTimer()
        {
            currentFrame = 0;
        }
    }
}
