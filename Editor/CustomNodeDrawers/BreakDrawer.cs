using Minerva.Module.Editor;
using UnityEditor;

namespace Amlos.AI.Editor
{
    //[CustomNodeDrawer(typeof(Break))]
    public class BreakDrawer : NodeDrawerBase
    {
        private static IntervalMode intervalMode;
        private static float time;

        public enum IntervalMode
        {
            frame,
            realTime
        }

        public override void Draw()
        {
            if (node is not Break breaker) return;
            breaker.returnTo = (Break.ReturnType)EditorGUILayout.EnumPopup("Return To Parent", breaker.returnTo);
            DrawNodeReference("Condition", breaker.condition);
            //DrawNodeList("Ignores", breaker.ignoredBranches, breaker);
        }

        public static void DrawTimer(Break service)
        {
            intervalMode = (IntervalMode)EditorGUILayout.EnumPopup("Interval Mode", intervalMode);
            switch (intervalMode)
            {
                case IntervalMode.frame:
                    service.interval = EditorGUILayout.IntField("Interval (frames)", service.interval);
                    if (service.interval < 0) service.interval = 0;
                    time = service.interval / 60f;
                    break;
                case IntervalMode.realTime:
                    time = EditorGUILayout.FloatField("Time (seconds)", time);
                    if (time < 0) time = 0;
                    service.interval = (int)(time * 60);
                    break;
                default:
                    break;
            }
            service.randomDeviation = EditorFieldDrawers.DrawRangeField("Deviation", service.randomDeviation);
        }
    }
}