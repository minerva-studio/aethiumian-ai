using UnityEngine;

namespace Amlos.AI.Variables
{
    public interface IVariable<T>
    {
        T Value { get; }
    }

    public interface IStringVariable : IVariable<string>
    {
        string StringValue { get; }
        string IVariable<string>.Value => StringValue;
    }

    public interface IIntegerVariable : IVariable<int>
    {
        int IntValue { get; }
        int IVariable<int>.Value => IntValue;
    }

    public interface IBoolVariable : IVariable<bool>
    {
        bool BoolValue { get; }
        bool IVariable<bool>.Value => BoolValue;
    }

    public interface IFloatVariable : IVariable<float>
    {
        float FloatValue { get; }
        float IVariable<float>.Value => FloatValue;
    }

    public interface IVector2Variable : IVariable<Vector2>
    {
        Vector2 Vector2Value { get; }
        Vector2 IVariable<Vector2>.Value => Vector2Value;
    }

    public interface IVector3Variable : IVariable<Vector3>
    {
        Vector3 Vector3Value { get; }
        Vector3 IVariable<Vector3>.Value => Vector3Value;
    }

    public interface IVector4Variable : IVariable<Vector4>
    {
        Vector4 Vector4Value { get; }
        Vector4 IVariable<Vector4>.Value => Vector4Value;
    }

    public interface IColorVariable : IVariable<Color>
    {
        Color ColorValue { get; }
        Color IVariable<Color>.Value => ColorValue;
    }

    public interface IUnityObjectVariable : IVariable<UnityEngine.Object>
    {
        UnityEngine.Object UnityObjectValue { get; }
        UnityEngine.Object IVariable<UnityEngine.Object>.Value => UnityObjectValue;
    }
}
