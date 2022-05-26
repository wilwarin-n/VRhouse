﻿using System;
using System.Collections.Generic;
using UnityEngine;
using com.zibra.liquid.Solver;

namespace com.zibra.liquid.Manipulators
{
    public class ManipulatorCompare : Comparer<Manipulator>
    {
        // Compares manipulator type ID
        public override int Compare(Manipulator x, Manipulator y)
        {
            int result = x.ManipType.CompareTo(y.ManipType);
            if (result != 0)
            {
                return result;
            }
            return x.GetHashCode().CompareTo(y.GetHashCode());
        }
    }

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class Manipulator : MonoBehaviour
    {
#if UNITY_EDITOR
        // Used to update editors
        public event Action onChanged;
        public void NotifyChange()
        {
            if (onChanged != null)
            {
                onChanged.Invoke();
            }
        }
#endif

        [SerializeField]
        [HideInInspector]
        public float[] ConstAdditionalData = new float[0]; // Data to send to a compute buffer once

        [HideInInspector]
        public ManipulatorType ManipType = 0;

        [HideInInspector]
        public bool isInitialized = false;

        [SerializeField]
        [HideInInspector]
        public Vector4 AdditionalData;

        public static readonly List<Manipulator> AllManipulators = new List<Manipulator>();

        [NonSerialized]
        [HideInInspector]
        public Matrix4x4 PreviousTransform;

        public enum ManipulatorType
        {
            None,
            Emitter,
            Void,
            ForceField,
            AnalyticCollider,
            NeuralCollider,
            Detector,
            Portal,
            TypeNum
        }

        protected void OnEnable()
        {
            if (!AllManipulators?.Contains(this) ?? false)
            {
                AllManipulators.Add(this);
            }
        }

        protected void OnDisable()
        {
            if (AllManipulators?.Contains(this) ?? false)
            {
                AllManipulators.Remove(this);
            }
        }

        private void Update()
        {
            PreviousTransform = transform.localToWorldMatrix;
        }

        private void Start()
        {
            PreviousTransform = transform.localToWorldMatrix;
        }

        public virtual void InitializeConstData()
        {
        }

        virtual public Matrix4x4 GetTransform()
        {
            return transform.localToWorldMatrix;
        }

        virtual public Quaternion GetRotation()
        {
            return transform.rotation;
        }

        virtual public Vector3 GetPosition()
        {
            return transform.position;
        }

        virtual public Vector3 GetScale()
        {
            return transform.lossyScale;
        }

#if UNITY_EDITOR
        protected void OnDestroy()
        {
            ZibraLiquid[] components = FindObjectsOfType<ZibraLiquid>();
            foreach (var liquidInstance in components)
            {
                liquidInstance.RemoveManipulator(this);
            }
        }
#endif
    }
}
