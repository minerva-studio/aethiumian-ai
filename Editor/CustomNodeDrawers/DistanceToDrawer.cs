using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>Shows the resolved geometry contract for DistanceTo without inferring tree intent.</summary>
    [CustomNodeDrawer(typeof(DistanceTo))]
    public sealed class DistanceToDrawer : DetermineNodeDrawer
    {
        public override void Draw()
        {
            base.Draw();

            if (node is not DistanceTo distanceTo) return;

            string measurement = distanceTo.measurement switch
            {
                DistanceTo.Measurement.Default => "Default (Collider AABB gap when both objects have usable colliders; otherwise Transform position)",
                DistanceTo.Measurement.TransformPosition => "Transform position",
                DistanceTo.Measurement.ColliderBounds => "Collider AABB gap",
                DistanceTo.Measurement.ColliderSurface => "Collider surface gap",
                _ => $"Unknown ({(int)distanceTo.measurement})",
            };
            string comparison = distanceTo.mode switch
            {
                CompareSign.notEquals => "!=",
                CompareSign.less => "<",
                CompareSign.lessOrEquals => "<=",
                CompareSign.equals => "==",
                CompareSign.greaterOrEquals => ">=",
                CompareSign.greater => ">",
                _ => "?",
            };

            EditorGUILayout.HelpBox(
                $"Distance contract: {measurement}\nComparison: distance {comparison} expect",
                MessageType.Info);
        }
    }
}
