using Amlos.AI.PathFinder;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Base class for all actions involving movement of entities
    /// </summary>
    [Serializable]
    public abstract class Movement : Action
    {
        public enum Behaviour
        {
            /// <summary> directly toward to a gameObject </summary>
            [Tooltip("Directly toward to a gameObject")]
            trace,
            /// <summary> random destination around a center </summary>
            [Tooltip("Random destination around a center")]
            wander,
            /// <summary> a fixed destination </summary>
            [Tooltip("A fixed destination")]
            fixedDestination,
        }

        public enum WanderMode
        {
            selfCentered,
            absoluteCentered,
        }

        public enum PathMode
        {
            [Tooltip("Directly move toward the destination")]
            simple,
            [Tooltip("Use path finder to calculate the precise path to go to the destination")]
            smart
        }


        /// <summary> Distance that determine whether entity failed to follow a path </summary>
        protected const float LOST_FROM_PATH_DISTANCE = 3;
        /// <summary> Chord length that determine whether entity is moving toward wrong direction </summary>
        protected const float MIN_DIRECTION_DIFF = 0.5f;
        /// <summary> Maximum error of distance between entity and arrival point </summary>
        protected const float NEGLIGIBLE_DISTANCE = 0.5f;

        [Obsolete("Not Used Anymore", true)]
        private static Dictionary<GameObject, ObstacleDetector> obstacleDetectors;
        [Obsolete("Not Used Anymore", true)]
        public ObstacleDetector obstacleDetector;

        public VariableField<int> maxIdleDuration = 5;
        public PathMode path;
        public Behaviour type;


        [TypeLimit(VariableType.UnityObject)]
        [DisplayIf(nameof(type), Behaviour.trace)] public VariableField tracing;

        /// <summary>
        /// the fixed destination for fixedDestination Behavior
        /// <!-- TODO variable name same as the parameter of Toward() -->
        /// </summary>
        [TypeLimit(VariableType.Vector2, VariableType.Vector3)]
        [DisplayIf(nameof(type), Behaviour.fixedDestination)] public VariableField destination;

        [DisplayIf(nameof(type), Behaviour.wander)] public WanderMode wanderMode;
        [TypeLimit(VariableType.Vector2, VariableType.Vector3)]
        [DisplayIf(nameof(type), Behaviour.wander)]
        [DisplayIf(nameof(wanderMode), WanderMode.absoluteCentered)] public VariableField centerOfWander;
        [DisplayIf(nameof(type), Behaviour.wander)] public VariableField<float> wanderDistance;


        protected Vector2Int wanderPosition;
        protected Vector2 lastPosition;
        protected float idleDuration;
        protected GameObject tracingObject;



        /// <summary>
        /// is simple movement? (without pathfinder)
        /// </summary>
        public bool isBlind => path == PathMode.simple;
        public bool isSmart => path == PathMode.smart;

        protected Rigidbody2D RigidBody => behaviourTree.Script.GetComponent<Rigidbody2D>();
        protected Collider2D Collider => behaviourTree.Script.GetComponent<Collider2D>();

        protected Vector2 tracingPosition => tracingObject.transform.position;
        protected Vector2Int fixedPlayerPosition => Vector2Int.FloorToInt(tracingPosition);

        protected Vector2 selfPosition => transform.position;
        protected Vector2Int fixedSelfPosition => Vector2Int.FloorToInt(selfPosition);

        protected Vector2 DisplacementToTargetObject => tracingPosition - selfPosition;
        protected Vector2 DisplacementToWanderPosition => wanderPosition - selfPosition;
        protected Vector2 DisplacementToDestination => this.destination.Vector2Value - selfPosition;




        public override void Initialize()
        {
        }

        public override void BeforeExecute()
        {
            tracingObject = type == Behaviour.trace ? tracing.GameObjectValue : null;
            wanderPosition = GetWanderLocation();
            idleDuration = 0;
        }

        /// <summary>
        /// Do not use update for movement because update will still execute when the game frozed
        /// </summary>
        public sealed override void Update() {  /*nothing*/  }
        /// <summary>
        /// Do not use late update for movement because update will still execute when the game frozed
        /// </summary>
        public sealed override void LateUpdate() {  /*nothing*/  }

        /// <summary>
        /// the simple movement
        /// </summary>
        /// <param name="destination">the destination/param>
        protected abstract void Toward(Vector2 destination);

        /// <summary>
        /// the smart movement
        /// </summary> 
        /// <param name="provider">the path provider provide the path to move toward certain point</param>
        /// <returns></returns>
        protected abstract IEnumerator SmartToward(PathProvider provider);


        /// <summary>
        /// determine whether the entity is trying to move but stay at same point too long (like stuck somewhere)
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsIdleTooLong()
        {
            if ((lastPosition - (Vector2)transform.position).magnitude < 0.1)
            {
                idleDuration += Time.fixedDeltaTime;
            }
            else idleDuration = 0;

            //stay too long
            if (maxIdleDuration != 0 && idleDuration > maxIdleDuration)
            {
                return true;
            }
            else lastPosition = transform.position;
            return false;
        }

        /// <summary>
        /// get the obstable detector
        /// </summary>
        /// <returns></returns>
        [Obsolete("Not Used Anymore", true)]
        protected ObstacleDetector GetObstacleDetector()
        {
            obstacleDetectors ??= new();
            if (!obstacleDetectors.ContainsKey(gameObject))
            {
                CreateObstacleDetector();
            }
            return obstacleDetector;
        }

        /// <summary>
        /// create the Obstacle Detector for AI
        /// </summary>
        /// <returns></returns>
        [Obsolete("Not Used Anymore", true)]
        private void CreateObstacleDetector()
        {
            var obj = new GameObject("Obstacle Detector", typeof(ObstacleDetector), typeof(BoxCollider2D));
            var collider = obj.GetComponent<BoxCollider2D>();
            var entity = behaviourTree.Script.transform;


            obj.transform.position = entity.position + Vector3.right * 1.5f;
            collider.isTrigger = true;
            collider.size = new Vector2(0.1f, entity.GetComponent<Collider2D>().bounds.size.y - 0.1f);
            obj.transform.parent = entity;
            obstacleDetector = obj.GetComponent<ObstacleDetector>();
            obstacleDetectors[gameObject] = obstacleDetector;
        }

        /// <summary>
        /// get a valid wander location for the entity
        /// </summary>
        /// <returns></returns>
        protected virtual Vector2Int GetWanderLocation()
        {
            return Vector2Int.zero;
        }
    }
}