using Minerva.Module;
using System;
using UnityEditor;

namespace Amlos.AI
{
    /// <summary>
    /// Base class of Asset Reference
    /// </summary>
    [Serializable]
    [Obsolete]
    public abstract class AssetReferenceBase : ICloneable
    {
        public UUID uuid;

        /// <summary>
        /// set the actual asset to the asset reference
        /// <br></br>
        /// only used in behaviour tree for initialization
        /// </summary>
        /// <param name="asset"></param>
        public abstract void SetAsset(UnityEngine.Object asset);
        /// <summary>
        /// return the type of asset it is point to
        /// </summary>
        /// <returns></returns>
        public abstract Type GetAssetType();
        /// <summary>
        /// set the reference to the asset
        /// </summary>
        /// <param name="asset"></param>
        public abstract void SetReference(UnityEngine.Object asset);

        public object Clone()
        {
            return MemberwiseClone() as AssetReferenceBase;
        }
    }

    /// <summary>
    /// Class that represent an asset in project
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    [Obsolete("Do not use, use VariableField<> instead")]
    public class AssetReference<T> : AssetReferenceBase where T : UnityEngine.Object
    {
        private T asset;

        public T GetAsset()
        {
            return asset;
        }

        public override void SetAsset(UnityEngine.Object asset)
        {
            this.asset = asset as T;
        }

        public override Type GetAssetType()
        {
            return typeof(T);
        }

        public override void SetReference(UnityEngine.Object asset)
        {
            uuid = AssetReferenceData.GetUUID(asset);
        }

        public static implicit operator T(AssetReference<T> assetReferenceBase)
        {
            return assetReferenceBase.asset;
        }
    }

    /// <summary>
    /// class that represent a generic type of asset
    /// </summary>
    [Serializable]
    [Obsolete("Do not use, use VariableField<> instead")]
    public class AssetReference : AssetReference<UnityEngine.Object>
    {

    }
}
