using Amlos.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amlos.AI
{
    /// <summary>
    /// Base class of Asset Reference
    /// </summary>
    [Serializable]
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


        /// <summary>
        /// get a uuid for asset
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="asset"></param>
        /// <returns></returns>
        public static UUID GetUUID<TObject>(TObject asset) where TObject : UnityEngine.Object
        {
#if UNITY_EDITOR
            return new Guid(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(asset)));
#else
            return UUID.Empty;
#endif

        }
    }

    /// <summary>
    /// Class that represent an asset in project
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
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
            uuid = GetUUID(asset);
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
    public class AssetReference : AssetReference<UnityEngine.Object>
    {

    }
}
