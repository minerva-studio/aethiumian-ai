using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public static class ArithmeticFunctions
    {
        public static float Add(float a, float b) => a + b;

        public static float Subtract(float a, float b) => a - b;

        public static float Multiply(float a, float b) => a * b;

        public static float Divide(float a, float b) => b == 0f ? 0f : a / b;

        public static bool Greater(float a, float b) => a > b;

        public static bool Less(float a, float b) => a < b;

        public static bool Equal(float a, float b) => Mathf.Approximately(a, b);

        public static float PI() => Mathf.PI;

        public static float Infinity() => Mathf.Infinity;

        public static float NegativeInfinity() => Mathf.NegativeInfinity;

        public static float Deg2Rad() => Mathf.Deg2Rad;

        public static float Rad2Deg() => Mathf.Rad2Deg;

        public static float Epsilon() => Mathf.Epsilon;

        public static float Phase01(float time, float period)
        {
            if (period <= 0f)
            {
                return 0f;
            }

            return Mathf.Repeat(time, period) / period;
        }

        public static float SineWave(float x, float amplitude, float frequency, float phase, float offset)
        {
            return amplitude * Mathf.Sin(frequency * x + phase) + offset;
        }

        public static float CosineWave(float x, float amplitude, float frequency, float phase, float offset)
        {
            return amplitude * Mathf.Cos(frequency * x + phase) + offset;
        }

        public static float SineWave01(float time, float period)
        {
            if (period <= 0f)
            {
                return 0f;
            }

            return (Mathf.Sin(Phase01(time, period) * Mathf.PI * 2f) + 1f) * 0.5f;
        }

        public static float CosineWave01(float time, float period)
        {
            if (period <= 0f)
            {
                return 0f;
            }

            return (Mathf.Cos(Phase01(time, period) * Mathf.PI * 2f) + 1f) * 0.5f;
        }

        public static float TriangleWave01(float time, float period)
        {
            if (period <= 0f)
            {
                return 0f;
            }

            // Build a 0..1..0 triangle from normalized phase.
            return 1f - Mathf.Abs(Phase01(time, period) * 2f - 1f);
        }

        public static bool Pulse(float time, float period, float dutyCycle)
        {
            if (period <= 0f)
            {
                return false;
            }

            return Phase01(time, period) < Mathf.Clamp01(dutyCycle);
        }

        public static float EaseInQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        public static float EaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        public static float EaseInOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
        }

        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float EaseInSine(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
        }

        public static float EaseOutSine(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI * 0.5f);
        }

        public static float EaseInOutSine(float t)
        {
            t = Mathf.Clamp01(t);
            return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        }

        public static float Saturate(float value) => Mathf.Clamp01(value);

        public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
        {
            float t = InverseLerpUnclamped(inMin, inMax, value);
            return Mathf.LerpUnclamped(outMin, outMax, t);
        }

        public static float Remap01(float value, float inMin, float inMax)
        {
            return InverseLerpUnclamped(inMin, inMax, value);
        }

        public static float InverseLerpUnclamped(float a, float b, float value)
        {
            if (Mathf.Approximately(a, b))
            {
                return 0f;
            }

            return (value - a) / (b - a);
        }
    }
}
