using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Immutable physics-layer configuration shared by NavWorld capture and ground traversal.
    /// It limits candidate colliders only; query direction and hit handling still distinguish obstacles from support.
    /// </summary>
    public readonly struct NavigationPhysicsLayers
    {
        /// <summary>Gets the layers captured as solid navigation geometry.</summary>
        public LayerMask SolidLayers { get; }

        /// <summary>Gets the layers captured as one-way navigation geometry.</summary>
        public LayerMask OneWayLayers { get; }

        /// <summary>Creates an immutable layer configuration without applying defaults or normalization.</summary>
        public NavigationPhysicsLayers(LayerMask solidLayers, LayerMask oneWayLayers)
        {
            SolidLayers = solidLayers;
            OneWayLayers = oneWayLayers;
        }

        /// <summary>Creates a fresh terrain query filter from the configured layer union.</summary>
        public ContactFilter2D CreateTerrainFilter()
        {
            return new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = SolidLayers | OneWayLayers,
                useTriggers = false,
            };
        }
    }
}
