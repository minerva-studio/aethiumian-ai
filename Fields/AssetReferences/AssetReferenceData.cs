using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// class store the key and assets that AssetReference points to
    /// </summary>
    [Serializable]
    public class AssetReferenceData
    {
        [SerializeField] private UnityEngine.Object asset;
        [SerializeField] private UUID uuid;

        public UUID UUID => uuid;
        public UnityEngine.Object Asset => asset;

        public AssetReferenceData()
        {
            this.asset = null;
            this.uuid = Guid.Empty;
        }

        public AssetReferenceData(UnityEngine.Object asset)
        {
            this.asset = asset;
            this.uuid = AssetReferenceBase.GetUUID(asset);
        }

        public void UpdateUUID()
        {
            uuid = asset.Exist() ? AssetReferenceBase.GetUUID(asset) : UUID.Empty;
        }
    }
}
