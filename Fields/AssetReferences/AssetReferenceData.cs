using Minerva.Module;
using System;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.References
{
    /// <summary>
    /// class store the key and assets that AssetReference points to
    /// </summary>
    [Serializable]
    public class AssetReferenceData
    {
        [SerializeField] private UnityEngine.Object asset;
        [SerializeField] private UUID uuid;
        public bool isFromVariable;

        public UUID UUID => uuid;
        public UnityEngine.Object Asset => asset;

        public AssetReferenceData()
        {
            asset = null;
            uuid = Guid.Empty;
        }

        public AssetReferenceData(UnityEngine.Object asset)
        {
            this.asset = asset;
            uuid = GetUUID(asset);
        }

        public void UpdateUUID()
        {
            uuid = asset.Exist() ? GetUUID(asset) : UUID.Empty;
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
            try
            {
                return new Guid(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)));
            }
            catch (Exception)
            {
                return Guid.Empty;
            }
#else
            return UUID.Empty;
#endif 
        }

        /// <summary>
        /// get asset by a uuid
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="asset"></param>
        /// <returns></returns>
        public static UnityEngine.Object GetAsset(UUID asset) => GetAsset<UnityEngine.Object>(asset);
        public static TObject GetAsset<TObject>(UUID uuid) where TObject : UnityEngine.Object
        {
#if UNITY_EDITOR
            var guid = new GUID(((Guid)uuid).ToString().Replace("-", ""));
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object @object = AssetDatabase.LoadAssetAtPath(assetPath, typeof(TObject));
            //Debug.Log(((Guid)uuid).ToString().Replace("-", ""));
            //Debug.Log(guid);
            //Debug.Log(assetPath);
            //Debug.Log(@object);
            return @object as TObject;
#else
            return null;
#endif

        }
    }
}
