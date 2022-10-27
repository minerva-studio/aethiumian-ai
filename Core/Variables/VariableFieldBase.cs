using Amlos.Core;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public abstract class VariableFieldBase : ICloneable
    {
        [SerializeField] protected UUID uuid;
        [SerializeField] protected Variable variable;

        public abstract VariableType Type { get; set; }
        public abstract object Constant { get; }


        public virtual bool IsConstant { get => uuid == UUID.Empty; }
        public virtual bool IsGeneric => false;
        public bool IsVector => Type == VariableType.Vector2 || Type == VariableType.Vector3;
        public bool IsNumeric => Type == VariableType.Int || Type == VariableType.Float;
        public bool HasReference => uuid != Core.UUID.Empty;
        public Variable Variable => variable;
        public UUID UUID => uuid;


        protected VariableType GetGenericVariableType<T2>()
        {
            T2 a = default;
            switch (a)
            {
                case int:
                    return VariableType.Int;
                case string:
                    return VariableType.String;
                case bool:
                    return VariableType.Bool;
                case float:
                    return VariableType.Float;
                case Vector2:
                    return VariableType.Vector2;
                case Vector3:
                    return VariableType.Vector3;
                default:
                    break;
            }
            return default;
        }



        public void SetReference(VariableData variable)
        {
            if (variable == null)
            {
                this.uuid = UUID.Empty;
            }
            this.uuid = variable.uuid;
        }

        public void SetRuntimeReference(Variable variable)
        {
            this.variable = variable;
        }


        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
