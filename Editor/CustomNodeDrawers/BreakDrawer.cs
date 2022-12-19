using UnityEditor;

namespace Amlos.AI.Editor
{
    //[CustomNodeDrawer(typeof(Break))]
    public class BreakDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Break breaker) return;
            breaker.returnTo = (Break.ReturnType)EditorGUILayout.EnumPopup("Return To Parent", breaker.returnTo);
            DrawNodeReference("Condition", breaker.condition);
            //DrawNodeList("Ignores", breaker.ignoredBranches, breaker);
        }
    }
}