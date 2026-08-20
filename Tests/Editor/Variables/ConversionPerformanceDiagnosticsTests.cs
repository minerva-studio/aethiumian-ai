using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class ConversionPerformanceDiagnosticsTests
    {
        private const int Iterations = 100_000;

        [Test]
        public void FirstAccessPerformanceDiagnosticReportsColdProbe()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            float result = ImplicitConverter<float>.From(123);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            stopwatch.Stop();

            UnityEngine.Debug.Log(
                $"[ConversionPerformance] first-access probe: {stopwatch.Elapsed.TotalMilliseconds:F3} ms, {allocated} bytes");
            Assert.That(result, Is.EqualTo(123f));
        }

        [Test]
        public void WarmPathPerformanceDiagnosticReportsTypedBaselines()
        {
            VariableField<int> fixedField = 7;
            VariableField dynamicField = new(VariableType.Int);
            dynamicField.ForceSetConstantValue(7);

            _ = ImplicitConverter<float>.From(1);
            _ = VariableUtility.ImplicitConversion<float, int>(1);
            _ = fixedField.GetValue<float>();
            _ = dynamicField.GetValue<float>();

            Stopwatch stopwatch = new();
            float directResult = 0f;
            float utilityResult = 0f;
            float converterResult = 0f;
            float fixedFieldResult = 0f;
            float dynamicFieldResult = 0f;

            double directMilliseconds = MeasureDirect(stopwatch, ref directResult);
            double utilityMilliseconds = MeasureUtility(stopwatch, ref utilityResult);
            double converterMilliseconds = MeasureConverter(stopwatch, ref converterResult);
            double fixedFieldMilliseconds = MeasureFixedField(stopwatch, fixedField, ref fixedFieldResult);
            double dynamicFieldMilliseconds = MeasureDynamicField(stopwatch, dynamicField, ref dynamicFieldResult);

            UnityEngine.Debug.Log(
                $"[ConversionPerformance] warm {Iterations} iterations: " +
                $"direct={directMilliseconds:F3} ms, " +
                $"VariableUtility={utilityMilliseconds:F3} ms, " +
                $"ImplicitConverter={converterMilliseconds:F3} ms, " +
                $"VariableField={fixedFieldMilliseconds:F3} ms, " +
                $"DynamicVariableField={dynamicFieldMilliseconds:F3} ms");

            Assert.That(directResult, Is.Not.EqualTo(0f));
            Assert.That(utilityResult, Is.Not.EqualTo(0f));
            Assert.That(converterResult, Is.Not.EqualTo(0f));
            Assert.That(fixedFieldResult, Is.Not.EqualTo(0f));
            Assert.That(dynamicFieldResult, Is.Not.EqualTo(0f));
        }

        [Test]
        public void TargetScriptAccessorPerformanceDiagnosticReportsCompiledReflectionAndValue()
        {
            TargetScriptValues target = new()
            {
                IntField = 7,
                IntProperty = 11,
                Vector4Field = new Vector4(1f, 2f, 3f, 4f),
            };

            Stopwatch coldWatch = Stopwatch.StartNew();
            long coldBefore = GC.GetAllocatedBytesForCurrentThread();
            TargetScriptVariable coldAccessor = CreateTargetScriptVariable(target, nameof(TargetScriptValues.IntField));
            long coldAllocated = GC.GetAllocatedBytesForCurrentThread() - coldBefore;
            coldWatch.Stop();

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

            int sink = 0;
            double directMilliseconds = MeasureDirectField(target, ref sink);
            double compiledFieldMilliseconds = MeasureCompiledField(compiledField, ref sink);
            double reflectionFieldMilliseconds = MeasureReflectionField(field, target, ref sink);
            double valueMilliseconds = MeasureValue(compiledField, ref sink);
            double wrapperMilliseconds = MeasureWrapper(wrapper, ref sink);
            double compiledPropertyMilliseconds = MeasureCompiledProperty(compiledProperty, ref sink);
            double reflectionPropertyMilliseconds = MeasureReflectionProperty(property, target, ref sink);
            double compiledMethodMilliseconds = MeasureCompiledMethod(compiledMethod, ref sink);
            double reflectionMethodMilliseconds = MeasureReflectionMethod(method, target, ref sink);
            double compiledFieldSetterMilliseconds = MeasureCompiledFieldSetter(compiledField, ref sink);
            double reflectionFieldSetterMilliseconds = MeasureReflectionFieldSetter(field, target, ref sink);
            float vectorSink = 0f;
            double compiledVectorMilliseconds = MeasureCompiledVector(compiledVector, ref vectorSink);
            double reflectionVectorMilliseconds = MeasureReflectionVector(vectorField, target, ref vectorSink);

            UnityEngine.Debug.Log(
                $"[TargetScriptPerformance] cold accessor: {coldWatch.Elapsed.TotalMilliseconds:F3} ms, " +
                $"{coldAllocated} bytes; warm {Iterations} iterations: " +
                $"directField={directMilliseconds:F3} ms, " +
                $"compiledField={compiledFieldMilliseconds:F3} ms, " +
                $"reflectionField={reflectionFieldMilliseconds:F3} ms, " +
                $"Value={valueMilliseconds:F3} ms, " +
                $"wrapper={wrapperMilliseconds:F3} ms, " +
                $"compiledProperty={compiledPropertyMilliseconds:F3} ms, " +
                $"reflectionProperty={reflectionPropertyMilliseconds:F3} ms, " +
                $"compiledMethod={compiledMethodMilliseconds:F3} ms, " +
                $"reflectionMethod={reflectionMethodMilliseconds:F3} ms, " +
                $"compiledFieldSetter={compiledFieldSetterMilliseconds:F3} ms, " +
                $"reflectionFieldSetter={reflectionFieldSetterMilliseconds:F3} ms, " +
                $"compiledVector={compiledVectorMilliseconds:F3} ms, " +
                $"reflectionVector={reflectionVectorMilliseconds:F3} ms");

            Assert.That(coldAccessor.GetValue<int>(), Is.EqualTo(target.IntField));
            Assert.That(sink, Is.Not.EqualTo(0));
            Assert.That(vectorSink, Is.Not.EqualTo(0f));
        }

        private static double MeasureDirect(Stopwatch stopwatch, ref float result)
        {
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) result += i;
            stopwatch.Stop();
            ReportAllocation("direct", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureUtility(Stopwatch stopwatch, ref float result)
        {
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) result += VariableUtility.ImplicitConversion<float, int>(i);
            stopwatch.Stop();
            ReportAllocation("VariableUtility", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureConverter(Stopwatch stopwatch, ref float result)
        {
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) result += ImplicitConverter<float>.From(i);
            stopwatch.Stop();
            ReportAllocation("ImplicitConverter", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureFixedField(Stopwatch stopwatch, VariableField<int> field, ref float result)
        {
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) result += field.GetValue<float>();
            stopwatch.Stop();
            ReportAllocation("VariableField", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureDynamicField(Stopwatch stopwatch, VariableField field, ref float result)
        {
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) result += field.GetValue<float>();
            stopwatch.Stop();
            ReportAllocation("DynamicVariableField", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static void ReportAllocation(string path, long before)
        {
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            UnityEngine.Debug.Log($"[ConversionPerformance] {path} allocations: {allocated} bytes");
        }

        private static TargetScriptVariable CreateTargetScriptVariable(object target, string memberName)
        {
            VariableData data = new("Target script performance value") { Path = memberName };
            return new TargetScriptVariable(data, target);
        }

        private static double MeasureDirectField(TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += target.IntField;
            stopwatch.Stop();
            ReportAllocation("TargetScript directField", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledField(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("TargetScript compiledField", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionField(FieldInfo field, TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)field.GetValue(target);
            stopwatch.Stop();
            ReportAllocation("TargetScript reflectionField", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureValue(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)variable.Value;
            stopwatch.Stop();
            ReportAllocation("TargetScript Value", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureWrapper(VariableField wrapper, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += wrapper.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("TargetScript typed wrapper", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledProperty(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("TargetScript compiledProperty", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionProperty(PropertyInfo property, TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)property.GetValue(target);
            stopwatch.Stop();
            ReportAllocation("TargetScript reflectionProperty", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledMethod(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<int>();
            stopwatch.Stop();
            ReportAllocation("TargetScript compiledMethod", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionMethod(MethodInfo method, TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += (int)method.Invoke(target, null);
            stopwatch.Stop();
            ReportAllocation("TargetScript reflectionMethod", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledFieldSetter(TargetScriptVariable variable, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++)
            {
                variable.SetValue(i);
                sink ^= variable.GetValue<int>();
            }
            stopwatch.Stop();
            ReportAllocation("TargetScript compiledFieldSetter", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionFieldSetter(FieldInfo field, TargetScriptValues target, ref int sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++)
            {
                field.SetValue(target, i);
                sink ^= (int)field.GetValue(target);
            }
            stopwatch.Stop();
            ReportAllocation("TargetScript reflectionFieldSetter", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureCompiledVector(TargetScriptVariable variable, ref float sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += variable.GetValue<Vector4>().x;
            stopwatch.Stop();
            ReportAllocation("TargetScript compiledVector", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureReflectionVector(FieldInfo field, TargetScriptValues target, ref float sink)
        {
            Stopwatch stopwatch = new();
            long before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < Iterations; i++) sink += ((Vector4)field.GetValue(target)).x;
            stopwatch.Stop();
            ReportAllocation("TargetScript reflectionVector", before);
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private sealed class TargetScriptValues
        {
            public int IntField;
            public int IntProperty { get; set; }
            public Vector4 Vector4Field;

            public int ReadInt() => IntField;
        }
    }
}
