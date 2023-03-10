using Minerva.Module;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Base class of all repeat services
    /// </summary>
    public abstract class RepeatService : Service
    {
        public int interval;
        public RangeInt randomDeviation;

        private int currentFrame;

        public override bool IsReady => currentFrame >= interval;
        public override void UpdateTimer()
        {
            currentFrame++;
        }
    }
}
