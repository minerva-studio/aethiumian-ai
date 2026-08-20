using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Aethiumian.AI.PlayMode.Tests
{
    public sealed class TargetScriptVariablePlayModePerformanceTests
    {
        private const int Iterations = 100_000;

        [UnityTest]
        public IEnumerator TargetScriptVariable_RuntimeCompiledAndReflectionDiagnostics()
        {
            GameObject gameObject = new("TargetScriptVariablePlayModePerformance");
            TargetScriptValues target = gameObject.AddComponent<TargetScriptValues>();
            target.IntField = 7;
            target.IntProperty = 11;
            target.Vector4Field = new Vector4(1f, 2f, 3f, 4f);

            try
            {
                TargetScriptVariable compiledField = CreateTargetScriptVariable(target, nameof(TargetScriptValues.IntField));
                TargetScriptVariable compiledProperty = CreateTargetScriptVariable(target, nameof(TargetScriptValues.IntProperty));
                TargetScriptVariable compiledMethod = CreateTargetScriptVariable(target, nameof(TargetScriptValues.ReadInt));
                TargetScriptVariable compiledVector = CreateTargetScriptVariable(target, nameof(TargetScriptValues.Vector4Field));
                VariableField wrapper = new();
                wrapper.SetRuntimeReference(compiledField);

                FieldInfo field = typeof(TargetScriptValues).GetField(nameof(TargetScriptValues.IntField));
                PropertyInfo property = typeof(TargetScriptValues).GetProperty(nameof(TargetScriptValues.IntProperty));
                MethodInfo method = typeof(TargetScriptValues).GetMethod(nameof(TargetScriptValues.ReadInt));
                FieldInfo vectorField = typeof(TargetScriptValues).GetField(nameof(TargetScriptValues.Vector4Field));

                _ = compiledField.GetValue<int>();
                _ = compiledProperty.GetValue<int>();
                _ = compiledMethod.GetValue<int>();
                _ = compiledVector.GetValue<Vector4>();
                _ = wrapper.GetValue<int>();
                _ = compiledField.Value;

                yield return null;

                int sink = 0;
                float vectorSink = 0f;
                double compiledFieldMilliseconds = MeasureCompiledField(compiledField, ref sink);
                double reflectionFieldMilliseconds = MeasureReflectionField(field, target, ref sink);
                double valueMilliseconds = MeasureValue(compiledField, ref sink);
                double wrapperMilliseconds = MeasureWrapper(wrapper, ref sink);
                double compiledPropertyMilliseconds = MeasureCompiledProperty(compiledProperty, ref sink);
                double reflectionPropertyMilliseconds = MeasureReflectionProperty(property, target, ref sink);
                double compiledMethodMilliseconds = MeasureCompiledMethod(compiledMethod, ref sink);
                double reflectionMethodMilliseconds = MeasureReflectionMethod(method, target, ref sink);
                double compiledVectorMilliseconds = MeasureCompiledVector(compiledVector, ref vectorSink);
                double reflectionVectorMilliseconds = MeasureReflectionVector(vectorField, target, ref vectorSink);

                Debug.Log(
                    $"[TargetScriptPerformance/PlayMode] warm {Iterations} iterations: " +
                    $"compiledField={compiledFieldMilliseconds:F3} ms, " +
                    $"reflectionField={reflectionFieldMilliseconds:F3} ms, " +
                    $"Value={valueMilliseconds:F3} ms, " +
                    $"wrapper={wrapperMilliseconds:F3} ms, " +
                    $"compiledProperty={compiledPropertyMilliseconds:F3} ms, " +
                    $"reflectionProperty={reflectionPropertyMilliseconds:F3} ms, " +
                    $"compiledMethod={compiledMethodMilliseconds:F3} ms, " +
                    $"reflectionMethod={reflectionMethodMilliseconds:F3} ms, " +
                    $"compiledVector={compiledVectorMilliseconds:F3} ms, " +
                    $"reflectionVector={reflectionVectorMilliseconds:F3} ms");

                Assert.That(compiledField.GetValue<int>(), Is.EqualTo(target.IntField));
                Assert.That(compiledProperty.GetValue<int>(), Is.EqualTo(target.IntProperty));
                Assert.That(compiledMethod.GetValue<int>(), Is.EqualTo(target.IntField));
                Assert.That(compiledVector.GetValue<Vector4>(), Is.EqualTo(target.Vector4Field));
                Assert.That(sink, Is.Not.EqualTo(0));
                Assert.That(vectorSink, Is.Not.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        private static TargetScriptVariable CreateTargetScriptVariable(object target, string memberName)
        {
            VariableData data = new("Target script play mode value") { Path = memberName };
            return new TargetScriptVariable(data, target);
        }

        private static double MeasureCompiledField(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("compiledField", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionField(FieldInfo field, TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)field.GetValue(target);
            stopwatch.Stop();
            ReportAllocation("reflectionField", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureValue(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)variable.Value;
            stopwatch.Stop();
            ReportAllocation("Value", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureWrapper(VariableField wrapper, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += wrapper.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("typedWrapper", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledProperty(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("compiledProperty", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionProperty(PropertyInfo property, TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)property.GetValue(target);
            stopwatch.Stop();
            ReportAllocation("reflectionProperty", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledMethod(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("compiledMethod", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionMethod(MethodInfo method, TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)method.Invoke(target, null);
            stopwatch.Stop();
            ReportAllocation("reflectionMethod", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledVector(TargetScriptVariable variable, ref float sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<Vector4>().x;
            stopwatch.Stop();
            ReportAllocation("compiledVector", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionVector(FieldInfo field, TargetScriptValues target, ref float sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += ((Vector4)field.GetValue(target)).x;
            stopwatch.Stop();
            ReportAllocation("reflectionVector", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static void ReportAllocation(string path, long before)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log($"[TargetScriptPerformance/PlayMode] {path} allocations: {allocated} bytes");
        }

        private sealed class TargetScriptValues : MonoBehaviour
        {
            public int IntField;
            public int IntProperty { get; set; }
            public Vector4 Vector4Field;

            public int ReadInt() => IntField;
        }
    }
}
