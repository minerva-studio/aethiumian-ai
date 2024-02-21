using Amlos.AI.Navigation;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
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

        //[Obsolete("Not Used Anymore", true)]
        //private static Dictionary<GameObject, ObstacleDetector> obstacleDetectors;
        //[Obsolete("Not Used Anymore", true)]
        //protected ObstacleDetector obstacleDetector;

        public VariableField<int> maxIdleDuration = 5;
        public PathMode path;
        public Behaviour type;

        public VariableField<float> arrivalErrorBound;

        [Constraint(VariableType.UnityObject)]
        [DisplayIf(nameof(type), Behaviour.trace)] public VariableField tracing;

        /// <summary>
        /// the fixed destination for fixedDestination Behavior
        /// <!-- TODO variable name same as the parameter of Toward() -->
        /// </summary>
        [Constraint(VariableType.Vector2, VariableType.Vector3)]
        [DisplayIf(nameof(type), Behaviour.fixedDestination)] public VariableField destination;

        [DisplayIf(nameof(type), Behaviour.wander)] public WanderMode wanderMode;

        [Constraint(VariableType.Vector2, VariableType.Vector3)]
        [DisplayIf(nameof(type), Behaviour.wander)]
        [DisplayIf(nameof(wanderMode), WanderMode.absoluteCentered)]
        public VariableField centerOfWander;

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

        protected Rigidbody2D RigidBody => gameObject.GetComponent<Rigidbody2D>();
        protected Collider2D Collider => gameObject.GetComponent<Collider2D>();

        protected Vector2 tracingPosition => tracingObject.transform.position;

        /// <summary>
        /// Center of the rigid body (world center of mass)
        /// </summary>
        protected Vector2 centerPosition => GetFootPosition();   // use rb position!
        protected Vector2 footPosition => GetFootPosition();
        protected Vector2Int fixedSelfPosition => Vector2Int.FloorToInt(centerPosition);

        protected Vector2 DisplacementToTargetObject => tracingPosition - centerPosition;
        protected Vector2 DisplacementToWanderPosition => wanderPosition - centerPosition;
        protected Vector2 DisplacementToDestination => this.destination.Vector2Value - centerPosition;



        #region Unity Calls

        public sealed override void Awake()
        {
            tracingObject = type == Behaviour.trace ? tracing.GameObjectValue : null;
            wanderPosition = GetWanderLocation();
            idleDuration = 0;

            InitMovement();
        }

        protected virtual void InitMovement()
        {
        }

        public sealed override void Start()
        {
            if (isSmart)
            {
                float distance = GetDisplacement();
                if (distance < arrivalErrorBound)
                {
                    End(true);
                    return;
                }
                GetPathProvider(out var provider);
                StartSmartMoving(provider);
            }
        }

        public sealed override void FixedUpdate()
        {
            if (IsIdleTooLong())
            {
                Debug.LogWarning(gameObject.name + " wait too long");
                Fail();
                return;
            }

            MovementFixedUpdate();
            if (isSmart) return;
            SimpleMovementUpdate();
        }

        /// <summary>
        /// Update only calls in simplle movement
        /// </summary>
        protected virtual void SimpleMovementUpdate()
        {
            var destination = GetDesintation();
            Toward(destination);
        }

        /// <summary>
        /// Fixed update, always called
        /// </summary>
        protected virtual void MovementFixedUpdate()
        {
        }

        /// <summary>
        /// Do not use update for movement because update will still execute when the game frozed
        /// </summary>
        public sealed override void Update() {  /*nothing*/  }
        /// <summary>
        /// Do not use late update for movement because update will still execute when the game frozed
        /// </summary>
        public sealed override void LateUpdate() {  /*nothing*/  }

        #endregion




        /// <summary>
        /// the simple movement
        /// </summary>
        /// <param name="destination">the destination/param>
        protected abstract void Toward(Vector2 destination);





        #region Smart

        /// <summary>
        /// Get the path provider
        /// </summary>
        /// <param name="provider"></param>
        /// <returns></returns>
        protected virtual void GetPathProvider(out PathProvider provider)
        {
            PathFinder pathFinder = GetPathFinder();
            provider = type switch
            {
                Behaviour.wander => new ToPosition(transform, wanderPosition, pathFinder, arrivalErrorBound),
                Behaviour.fixedDestination => new ToPosition(transform, Vector2Int.RoundToInt(this.destination.Vector2Value), pathFinder, arrivalErrorBound),
                Behaviour.trace => new Tracer(transform, tracingObject.transform, pathFinder, 1, arrivalErrorBound),
                _ => null,
            };
        }

        protected abstract PathFinder GetPathFinder();

        /// <summary>
        /// the smart movement
        /// </summary> 
        protected abstract void StartSmartMoving(PathProvider provider);

        #endregion






        /// <summary>
        /// determine whether the entity is trying to move but stay at same point too long (like stuck somewhere)
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsIdleTooLong()
        {
            if ((lastPosition - (Vector2)transform.position).magnitude < 0.01f)
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
        ///// <returns></returns>
        //[Obsolete("Not Used Anymore", true)]
        //protected ObstacleDetector GetObstacleDetector()
        //{
        //    obstacleDetectors ??= new();
        //    if (!obstacleDetectors.ContainsKey(gameObject))
        //    {
        //        CreateObstacleDetector();
        //    }
        //    return obstacleDetector;
        //}

        ///// <summary>
        ///// create the Obstacle Detector for AI
        ///// </summary>
        ///// <returns></returns>
        //[Obsolete("Not Used Anymore", true)]
        //private void CreateObstacleDetector()
        //{
        //    var obj = new GameObject("Obstacle Detector", typeof(ObstacleDetector), typeof(BoxCollider2D));
        //    var collider = obj.GetComponent<BoxCollider2D>();
        //    var entity = behaviourTree.Script.transform;


        //    obj.transform.position = entity.position + Vector3.right * 1.5f;
        //    collider.isTrigger = true;
        //    collider.size = new Vector2(0.1f, entity.GetComponent<Collider2D>().bounds.size.y - 0.1f);
        //    obj.transform.parent = entity;
        //    obstacleDetector = obj.GetComponent<ObstacleDetector>();
        //    obstacleDetectors[gameObject] = obstacleDetector;
        //}

        /// <summary>
        /// get a valid wander location for the entity
        /// </summary>
        /// <returns></returns>
        protected virtual Vector2Int GetWanderLocation()
        {
            return Vector2Int.zero;
        }




        private Vector2 GetFootPosition()
        {
            var pos = RigidBody.worldCenterOfMass;
            pos.y -= Collider.bounds.size.y / 2;
            return pos;
        }



        /// <summary>
        /// Get the displacement
        /// </summary> 
        /// <returns></returns>
        protected virtual float GetDisplacement()
        {
            var distance = type switch
            {
                Behaviour.wander => DisplacementToWanderPosition.magnitude,
                Behaviour.fixedDestination => DisplacementToDestination.magnitude,
                Behaviour.trace => DisplacementToTargetObject.magnitude,
                _ => 0f,
            };
            return distance;
        }

        protected Vector2 GetDesintation()
        {
            return type switch
            {
                Behaviour.trace => tracingPosition,
                Behaviour.fixedDestination => this.destination.Vector2Value,
                Behaviour.wander => (Vector2)wanderPosition,
                _ => centerPosition,
            };
        }

        /// <summary>
        /// Check is facing wall
        /// </summary>
        /// <param name="wallLayerMask"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        protected bool IsFacingWall(LayerMask wallLayerMask, Vector2 direction)
        {
            direction = new Vector2(Mathf.Abs(direction.x) / direction.x * Collider.bounds.size.x / 2, 0);
            direction.x += Mathf.Abs(direction.x) * 0.2f;

            Vector3 center = Collider.bounds.center;
            Debug.DrawRay(center, direction, Color.green);
            // Debug.Log(this.gameObject.layer); 

            RaycastHit2D hit = Physics2D.Raycast(center, direction, 1, wallLayerMask);
            if (hit.collider != null /*&& hit.collider.gameObject.tag != "Player"*/)
            {
                // Debug.Log("hit collide"); 
                return true;
            }

            Vector2 origin = center;
            origin.y -= Collider.bounds.size.y / 2;
            hit = Physics2D.Raycast(origin, direction, 1, wallLayerMask);
            if (hit.collider != null /*&& hit.collider.gameObject.tag != "Player"*/)
            {
                // Debug.Log("hit collide"); 
                return true;
            }

            origin = center;
            origin.y += Collider.bounds.size.y / 2;
            hit = Physics2D.Raycast(origin, direction, 1, wallLayerMask);
            if (hit.collider != null /*&& hit.collider.gameObject.tag != "Player"*/)
            {
                // Debug.Log("hit collide"); 
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check is facing wall
        /// </summary>
        /// <param name="wallLayerMask"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        protected bool IsFacingWall(LayerMask wallLayerMask, Vector2 direction, int step)
        {
            direction = new Vector2(Mathf.Abs(direction.x) / direction.x * Collider.bounds.size.x / 2, 0);
            direction.x += Mathf.Abs(direction.x) * 0.2f;

            if (step <= 1)
            {
                Vector3 center = Collider.bounds.center;
                Debug.DrawRay(center, direction, Color.green);

                RaycastHit2D hit = Physics2D.Raycast(center, direction, 1, wallLayerMask);
                if (hit.collider != null /*&& hit.collider.gameObject.tag != "Player"*/)
                {
                    // Debug.Log("hit collide"); 
                    return true;
                }
                return false;
            }


            Vector2 start = Collider.bounds.min;
            float stepProgress = Collider.bounds.size.y / (step - 1);

            for (int i = 0; i < step; i++)
            {
                Vector2 center = start;
                center.y += stepProgress * step;
                RaycastHit2D hit = Physics2D.Raycast(center, direction, 1, wallLayerMask);
                if (hit.collider != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check is on the ground, calc by 3 points
        /// </summary>
        /// <param name="groundLayerMask"></param>
        /// <returns></returns>
        protected bool IsOnGround(LayerMask groundLayerMask)
        {
            Vector2 direction = Vector2.down;

            Vector3 center = Collider.bounds.center;
            Debug.DrawRay(center, direction, Color.green);

            RaycastHit2D hit = Physics2D.Raycast(center, direction, 1, groundLayerMask);
            if (hit.collider != null /*&& hit.collider.gameObject.tag != "Player"*/)
            {
                // Debug.Log("hit collide"); 
                return true;
            }

            Vector2 origin = center;
            origin.x -= Collider.bounds.size.x / 2;
            hit = Physics2D.Raycast(origin, direction, 1, groundLayerMask);
            if (hit.collider != null /*&& hit.collider.gameObject.tag != "Player"*/)
            {
                // Debug.Log("hit collide"); 
                return true;
            }

            origin = center;
            origin.x += Collider.bounds.size.x / 2;
            hit = Physics2D.Raycast(origin, direction, 1, groundLayerMask);
            if (hit.collider != null /*&& hit.collider.gameObject.tag != "Player"*/)
            {
                // Debug.Log("hit collide"); 
                return true;
            }
            return false;
        }

        protected bool IsOnGround(LayerMask groundLayerMask, int step = 3)
        {
            Vector2 direction = Vector2.down;
            if (step <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(step));
            }
            // simple case
            if (step == 1)
            {
                Vector3 center = Collider.bounds.center;
                RaycastHit2D hit = Physics2D.Raycast(center, direction, 1, groundLayerMask);
                return hit.collider != null;
            }

            Vector2 start = Collider.bounds.min;
            float stepProgress = Collider.bounds.size.x / (step - 1);

            for (int i = 0; i < step; i++)
            {
                Vector2 center = start;
                center.x += stepProgress * i;
                RaycastHit2D hit = Physics2D.Raycast(center, direction, 1, groundLayerMask);
                Debug.DrawRay(center, direction);
                if (hit.collider != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}