using Amlos.AI.References;
using System;
using UnityEngine;

namespace Amlos.AI.Variables
{
    public delegate VariableType VariableTypeProviderMethod<in T>();

    public interface IVariableTypeProvider<in T>
    {
        VariableType Type { get; }
    }


    public readonly struct VariableTypeProvider :
        IVariableTypeProvider<int>,
        IVariableTypeProvider<UnityEngine.LayerMask>,
        IVariableTypeProvider<Enum>,
        IVariableTypeProvider<string>,
        IVariableTypeProvider<bool>,
        IVariableTypeProvider<float>,
        IVariableTypeProvider<Vector2>,
        IVariableTypeProvider<Vector3>,
        IVariableTypeProvider<Vector4>,
        IVariableTypeProvider<Color>,
        IVariableTypeProvider<UnityEngine.Object>,
        IVariableTypeProvider<NodeProgress>
    {
        public readonly static VariableTypeProvider Default = new();

        readonly VariableType IVariableTypeProvider<int>.Type => VariableType.Int;
        readonly VariableType IVariableTypeProvider<Enum>.Type => VariableType.Int;
        readonly VariableType IVariableTypeProvider<LayerMask>.Type => VariableType.Int;
        readonly VariableType IVariableTypeProvider<string>.Type => VariableType.String;
        readonly VariableType IVariableTypeProvider<bool>.Type => VariableType.Bool;
        readonly VariableType IVariableTypeProvider<float>.Type => VariableType.Float;
        readonly VariableType IVariableTypeProvider<Vector2>.Type => VariableType.Vector2;
        readonly VariableType IVariableTypeProvider<Vector3>.Type => VariableType.Vector3;
        readonly VariableType IVariableTypeProvider<Vector4>.Type => VariableType.Vector4;
        readonly VariableType IVariableTypeProvider<Color>.Type => VariableType.Vector4;
        readonly VariableType IVariableTypeProvider<UnityEngine.Object>.Type => VariableType.UnityObject;
        readonly VariableType IVariableTypeProvider<NodeProgress>.Type => VariableType.Node;


        public static VariableType GetVariableType<T>()
        {
            if (Default is IVariableTypeProvider<T> provider) return provider.Type;
            if (typeof(T).IsEnum) return VariableType.Int;
            return VariableType.Generic;
        }
    }

    public readonly struct VariableTypeProvider<T>
    {
        public static VariableType Type => VariableTypeProvider.GetVariableType<T>();
    }
}
