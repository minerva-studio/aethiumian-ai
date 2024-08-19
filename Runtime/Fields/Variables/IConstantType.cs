using UnityEngine;

namespace Amlos.AI.Variables
{
    public interface IConstantType<T>
    {
        T Value { get; }
    }

    public interface IStringConstant : IConstantType<string>
    {
        string ConstantStringValue { get; }
        string IConstantType<string>.Value => ConstantStringValue;
    }

    public interface IIntegerConstant : IConstantType<int>
    {
        int ConstantIntValue { get; }
        int IConstantType<int>.Value => ConstantIntValue;
    }

    public interface IBoolConstant : IConstantType<bool>
    {
        bool ConstantBoolValue { get; }
        bool IConstantType<bool>.Value => ConstantBoolValue;
    }

    public interface IFloatConstant : IConstantType<float>
    {
        float ConstantFloatValue { get; }
        float IConstantType<float>.Value => ConstantFloatValue;
    }

    public interface IVector2Constant : IConstantType<Vector2>
    {
        Vector2 ConstantVector2Value { get; }
        Vector2 IConstantType<Vector2>.Value => ConstantVector2Value;
    }

    public interface IVector3Constant : IConstantType<Vector3>
    {
        Vector3 ConstantVector3Value { get; }
        Vector3 IConstantType<Vector3>.Value => ConstantVector3Value;
    }

    public interface IVector4Constant : IConstantType<Vector4>
    {
        Vector4 ConstantVector4Value { get; }
        Vector4 IConstantType<Vector4>.Value => ConstantVector4Value;
    }

    public interface IColorConstant : IConstantType<Color>
    {
        Color ConstantColorValue { get; }
        Color IConstantType<Color>.Value => ConstantColorValue;
    }

    public interface IUnityObjectConstant : IConstantType<UnityEngine.Object>
    {
        UnityEngine.Object ConstantUnityObjectValue { get; }
        UnityEngine.Object IConstantType<UnityEngine.Object>.Value => ConstantUnityObjectValue;
    }
}
