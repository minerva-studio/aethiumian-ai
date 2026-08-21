using System;

namespace Aethiumian.AI.Variables
{
    /// <summary>Represents a fixed four-lane integer value.</summary>
    internal readonly struct ComponentwiseInt4
    {
        public readonly int x;
        public readonly int y;
        public readonly int z;
        public readonly int w;
        /// <summary>Initializes a four-lane integer value.</summary>
        public ComponentwiseInt4(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        /// <summary>Adds all four lanes.</summary>
        public static ComponentwiseInt4 operator +(ComponentwiseInt4 left, ComponentwiseInt4 right)
        {
            return new ComponentwiseInt4(
                left.x + right.x,
                left.y + right.y,
                left.z + right.z,
                left.w + right.w);
        }

        /// <summary>Subtracts all four lanes.</summary>
        public static ComponentwiseInt4 operator -(ComponentwiseInt4 left, ComponentwiseInt4 right)
        {
            return new ComponentwiseInt4(
                left.x - right.x,
                left.y - right.y,
                left.z - right.z,
                left.w - right.w);
        }

        /// <summary>Multiplies all four lanes.</summary>
        public static ComponentwiseInt4 operator *(ComponentwiseInt4 left, ComponentwiseInt4 right)
        {
            return new ComponentwiseInt4(
                left.x * right.x,
                left.y * right.y,
                left.z * right.z,
                left.w * right.w);
        }

        /// <summary>Divides the requested number of lanes without evaluating inactive lanes.</summary>
        public static ComponentwiseInt4 Divide(ComponentwiseInt4 left, ComponentwiseInt4 right, int componentCount)
        {
            if ((uint)(componentCount - 1) > 3u)
            {
                throw new ArgumentOutOfRangeException(nameof(componentCount));
            }

            return componentCount switch
            {
                1 => new ComponentwiseInt4(left.x / right.x, 0, 0, 0),
                2 => new ComponentwiseInt4(left.x / right.x, left.y / right.y, 0, 0),
                3 => new ComponentwiseInt4(left.x / right.x, left.y / right.y, left.z / right.z, 0),
                _ => new ComponentwiseInt4(
                    left.x / right.x,
                    left.y / right.y,
                    left.z / right.z,
                    left.w / right.w),
            };
        }

        /// <summary>Computes the integer component-wise minimum.</summary>
        public static ComponentwiseInt4 Min(ComponentwiseInt4 left, ComponentwiseInt4 right)
        {
            return new ComponentwiseInt4(
                Math.Min(left.x, right.x),
                Math.Min(left.y, right.y),
                Math.Min(left.z, right.z),
                Math.Min(left.w, right.w));
        }

        /// <summary>Computes the integer component-wise maximum.</summary>
        public static ComponentwiseInt4 Max(ComponentwiseInt4 left, ComponentwiseInt4 right)
        {
            return new ComponentwiseInt4(
                Math.Max(left.x, right.x),
                Math.Max(left.y, right.y),
                Math.Max(left.z, right.z),
                Math.Max(left.w, right.w));
        }
    }
}
