using Amlos.AI;
using Minerva.Module;
using UnityEditor;

namespace Amlos.Editor
{
    //[CustomNodeDrawer(typeof(Break))]
    public class BreakDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (this.node is not Break breaker) return;
            breaker.returnTo = (Break.ReturnType)EditorGUILayout.EnumPopup("Return To Parent", breaker.returnTo);
            DrawNodeSelection("Condition", breaker.condition);
            //DrawNodeList("Ignores", breaker.ignoredBranches, breaker);
        }
    }
}