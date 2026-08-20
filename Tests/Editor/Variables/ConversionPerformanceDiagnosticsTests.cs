using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Diagnostics;

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
    }
}
