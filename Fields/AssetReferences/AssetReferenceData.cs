using Minerva.Module;
using System;

namespace Amlos.AI
{
    /// <summary>
    /// class store the key and assets that AssetReference points to
    /// </summary>
    [Serializable]
    public class AssetReferenceData
    {
        public UnityEngine.Object asset;
        public UUID uuid;

        public AssetReferenceData()
        {
            asset = null;
            uuid = Guid.Empty;
        }

        public AssetReferenceData(UnityEngine.Object asset)
        {
            this.asset = asset;
            uuid = AssetReferenceBase.GetUUID(asset);
        }
    }
}
