using System.Reflection;
using UnityEditor;

using InspectorSerializedPropertyExtensions = Minerva.Module.Editor.SerializedPropertyExtensions;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Local bridge for serialized property reflection supplied by Minerva Inspector.
    /// </summary>
    internal static class AIEditorSerializedPropertyExtensions
    {
        public static object GetAIValue(this SerializedProperty property)
            => InspectorSerializedPropertyExtensions.GetValue(property);

        public static MemberInfo GetAIMemberInfo(this SerializedProperty property)
            => InspectorSerializedPropertyExtensions.GetMemberInfo(property);
    }
}
