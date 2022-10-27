using System.Collections.Generic;
using System;
using UnityEngine;
using Amlos.AI.Visual;
#if UNITY_EDITOR
#endif

/// <summary>
/// Author: Wendell
/// </summary>
namespace Amlos.AI
{
    [Serializable]
    public class Graph
    {
        [SerializeReference] public List<GraphNode> graphNodes;
        [SerializeReference] public List<Connection> connections;
    }

}