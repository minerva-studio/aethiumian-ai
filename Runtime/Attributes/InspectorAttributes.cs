using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Aethiumian.AI.Attributes
{
    /// <summary>
    /// Marks a node field as read-only in AI node drawers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ReadOnlyAttribute : Attribute
    {
    }

    /// <summary>
    /// Base attribute for node fields that are shown only when another field matches expected values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public abstract class ConditionalFieldAttribute : Attribute
    {
        public string path = "";
        public object[] expectValues;
        public bool result;

        protected ConditionalFieldAttribute(string path, bool result, params object[] expectValues)
        {
            this.path = path;
            this.result = result;
            this.expectValues = expectValues ?? Array.Empty<object>();
        }

        protected ConditionalFieldAttribute(string listPath) : this(listPath, true, true)
        {
        }

        public bool Matches(object value)
        {
            return expectValues.Any(expect => MatchWithExpect(value, expect)) == result;
        }

        public static bool IsTrue(object obj, FieldInfo field)
        {
            if (obj == null || field == null)
            {
                return true;
            }

            if (!IsDefined(field, typeof(ConditionalFieldAttribute)))
            {
                return true;
            }

            var attrs = (ConditionalFieldAttribute[])GetCustomAttributes(field, typeof(ConditionalFieldAttribute));
            foreach (var attr in attrs)
            {
                FieldInfo dependentField = FindField(obj.GetType(), attr.path);
                if (dependentField == null || !attr.Matches(dependentField.GetValue(obj)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchWithExpect(object value, object expect)
        {
            if (value == null)
            {
                return expect == null;
            }

            if (expect == null)
            {
                return false;
            }

            if (IsEnumComparable(value, expect))
            {
                int intExpect = Convert.ToInt32(expect);
                int intValue = Convert.ToInt32(value);
                bool isFlag = GetCustomAttribute(expect.GetType(), typeof(FlagsAttribute)) != null;
                return isFlag ? (intExpect & intValue) != 0 : intExpect == intValue;
            }

            if (value is UnityEngine.Object obj && expect is bool boolExpect)
            {
                return ((bool)obj) == boolExpect;
            }

            if (double.TryParse(expect.ToString(), out double expectedNumber)
                && double.TryParse(value.ToString(), out double actualNumber))
            {
                return expectedNumber == actualNumber;
            }

            if (expect is IComparable comparableExpect
                && value is IComparable comparableValue
                && expect.GetType() == value.GetType())
            {
                return Comparer.Default.Compare(comparableValue, comparableExpect) == 0;
            }

            return value.Equals(expect);
        }

        private static bool IsEnumComparable(object value, object expect)
        {
            return expect is Enum || expect is int || value is Enum || value is int;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Shows a node field when another field matches the configured values.
    /// </summary>
    public sealed class DisplayIfAttribute : ConditionalFieldAttribute
    {
        public DisplayIfAttribute(string listPath = "", params object[] expectValues) : base(listPath, true, expectValues)
        {
        }

        public DisplayIfAttribute(string listPath, bool result, params object[] expectValues) : base(listPath, result, expectValues)
        {
        }

        public DisplayIfAttribute(string listPath, bool result) : base(listPath, true, result)
        {
        }

        public DisplayIfAttribute(string listPath) : base(listPath)
        {
        }
    }
}
