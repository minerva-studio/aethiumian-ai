using UnityEngine;

namespace Aethiumian.AI.Randomization
{
    public static class RandomSourceUtility
    {
        public static Vector2 NextVector2(this IRandomSource random, Vector2 min, Vector2 max)
        {
            return new Vector2(
                random.NextFloat(min.x, max.x),
                random.NextFloat(min.y, max.y));
        }

        public static Vector3 NextVector3(this IRandomSource random, Vector3 min, Vector3 max)
        {
            return new Vector3(
                random.NextFloat(min.x, max.x),
                random.NextFloat(min.y, max.y),
                random.NextFloat(min.z, max.z));
        }

        public static Vector2 NextVector2(this IRandomSource random, float xMaxExclusive, float yMaxExclusive)
        {
            return new Vector2(
                random.NextFloat(0f, xMaxExclusive),
                random.NextFloat(0f, yMaxExclusive));
        }

        public static Vector3 NextVector3(this IRandomSource random, float xMaxExclusive, float yMaxExclusive, float zMaxExclusive)
        {
            return new Vector3(
                random.NextFloat(0f, xMaxExclusive),
                random.NextFloat(0f, yMaxExclusive),
                random.NextFloat(0f, zMaxExclusive));
        }

        public static Vector2 NextInsideUnitCircle(this IRandomSource random)
        {
            float angle = random.NextFloat(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(random.NextFloat());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        public static Vector2 NextUnitCircleDirection(this IRandomSource random)
        {
            float angle = random.NextFloat(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
