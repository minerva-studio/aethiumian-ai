using Amlos.AI;
using Minerva.Module;
using UnityEngine;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(Inverter))]
    public class InverterDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            var inverter = this.node as Inverter; 
            DrawNodeSelection("Next", inverter.node);
        }
    }
}