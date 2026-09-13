using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Product-level movement goal selection, serialized by coordinators.</summary>
    public enum MovementGoal
    {
        Default = 0,
        Confront,
        Proximity,
        FiringPosition,
    }

    /// <summary>Metric used when measuring the axial gap between two AABBs.</summary>
    public enum DistanceMetric
    {
        Euclidean = 0,
        Manhattan,
        Chebyshev,
    }

    /// <summary>Planner geometry independent of product-level movement goal names.</summary>
    public enum NavigationGoalGeometry
    {
        GroundRange,
        Proximity,
        Retreat,
    }

}
