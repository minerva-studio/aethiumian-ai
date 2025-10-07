using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Tooltip("Determine transform is outside of screen")]
    public sealed class IsInScreen : Determine
    {
        [Constraint(VariableType.UnityObject, VariableType.Vector2, VariableType.Vector3)]
        [Readable]
        public VariableReference position;

        public override bool GetValue()
        {
            Vector3 position = this.position.PositionValue;
            var camPoint = Camera.main.WorldToScreenPoint(position);
            // out of screen
            return camPoint.x >= 0 && camPoint.y >= 0 && camPoint.x <= Screen.width && camPoint.y <= Screen.height;
        }
    }
}
