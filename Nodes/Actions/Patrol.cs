using Amlos.Core;
using Amlos.AI.PathFinder;
using System;
using System.Collections;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Patrol : Movement
    {
        public VariableField<float> speed;
        public VariableField<float> arrivalErrorBound;
        public VariableField<float> patrolRadius;
        public VariableField<float> timeBeforeNextMove;
        public VariableField<float> patrolDistanceXorY;
        public VariableField<int> numberOfRandomPatrolPoints;

        private Transform[] patrolPoints;
        private int currentPatrolPoint = 0;
        private float currentTime = 0f;

        public override void BeforeExecute()
        {
            if (Behaviour.patrolRandom == type)
            {
                patrolPoints = new Transform[numberOfRandomPatrolPoints];
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    Transform curr = new GameObject().transform;
                    bool newPointNotFound = true;
                    //curr.position = Random.insideUnitCircle * patrolRadius;
                    //curr.position += transform.position;
                    while (newPointNotFound)
                    {
                        curr.position = UnityEngine.Random.insideUnitCircle * patrolRadius;
                        curr.position += transform.position;
                        for (int j = 0; j < i; j++)
                        {
                            if (Vector2.Distance(curr.position, patrolPoints[j].position) < arrivalErrorBound + 0.3f) continue;
                        }
                        newPointNotFound = false;
                    }

                    patrolPoints[i] = curr.transform;
                }
            }
            else
            {
                patrolPoints = new Transform[2];

                Transform p0 = new GameObject().transform;
                Transform p1 = new GameObject().transform;
                p0.position += transform.position;
                p1.position += transform.position;

                if (Behaviour.patrolX == type)
                {
                    p0.position += new Vector3(patrolDistanceXorY, 0, 0);
                    p1.position -= new Vector3(patrolDistanceXorY, 0, 0);
                }
                else if (Behaviour.patrolY == type)
                {
                    p0.position += new Vector3(0, patrolDistanceXorY, 0);
                    p1.position -= new Vector3(0, patrolDistanceXorY, 0);
                }

                patrolPoints[0] = p0.transform;
                patrolPoints[1] = p1.transform;
            }
        }

        public override void FixedUpdate()
        {
            switch (type)
            {
                case Behaviour.patrolRandom:
                case Behaviour.patrolX:
                case Behaviour.patrolY:
                    DoPatrol();
                    break;
                default:
                    break;
            }
        }

        protected override IEnumerator SmartToward(PathProvider provider)
        {
            throw new NotImplementedException();
        }

        protected override void Toward(Vector2 destination)
        {
            throw new NotImplementedException();
        }

        void DoPatrol()
        {
            if (patrolPoints.Length > 0)
            {
                var vec = patrolPoints[currentPatrolPoint].position - behaviourTree.Script.transform.position;
                if (vec.magnitude < arrivalErrorBound)
                {

                    if (currentTime > timeBeforeNextMove)
                    {
                        //Return(true);
                        currentPatrolPoint = (currentPatrolPoint + 1) % patrolPoints.Length;
                        currentTime = 0f;
                    }
                    else
                    {
                        currentTime += Time.fixedDeltaTime;
                    }
                }
                else
                {
                    behaviourTree.Script.GetComponent<Transform>().Translate(vec.normalized * speed * Time.fixedDeltaTime);
                }
            }
        }

    }
}