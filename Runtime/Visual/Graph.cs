using Amlos.AI.Visual;
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
#endif

/// <summary>
/// Author: Wendell
/// </summary>
namespace Amlos.AI
{
    /// <summary>
    /// Behaviour tree graph
    /// </summary>
    [Serializable]
    public class Graph
    {
        [SerializeReference] public List<GraphNode> graphNodes;
        [SerializeReference] public List<Connection> connections;
    }

}