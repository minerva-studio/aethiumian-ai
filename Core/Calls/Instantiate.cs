﻿using Amlos.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Tooltip("Instantiate a prefab to the scene")]
    [Serializable]
    public class Instantiate : Call
    {
        public enum ParentMode
        {
            self,
            parent,
            global,
        }

        public enum OffsetMode
        {
            SelfOffset,
            worldOffset,
        }

        public AssetReference<GameObject> original;
        public ParentMode parentOfObject;
        public OffsetMode offsetMode;
        public VariableField<Vector3> offset;

        public override void Execute()
        {
            GameObject newGameObject;
            newGameObject = UnityEngine.Object.Instantiate(original) as GameObject;

            switch (parentOfObject)
            {
                case ParentMode.self:
                    newGameObject.transform.SetParent(transform);
                    break;
                case ParentMode.parent:
                    newGameObject.transform.SetParent(transform.parent);
                    break;
                case ParentMode.global:
                default:
                    break;
            }
            switch (offsetMode)
            {
                case OffsetMode.SelfOffset:
                    newGameObject.transform.position = transform.position + offset;
                    break;
                case OffsetMode.worldOffset:
                    newGameObject.transform.position = offset;
                    break;
                default:
                    break;
            }

            End(true);
        }
    }
}