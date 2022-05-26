using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using com.zibra.liquid.Solver;

namespace com.zibra.liquid.SDFObjects
{
    /// <summary>
    /// An analytical ZibraFluid SDF collider
    /// </summary>
    [AddComponentMenu("Zibra/Zibra Analytic Collider")]
    public class ZibraAnalyticSDFCollider : SDFCollider
    {
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            switch (chosenSDFType)
            {
            case SDFType.Sphere:
                Gizmos.DrawWireSphere(new Vector3(0, 0, 0), 0.5f);
                break;
            case SDFType.Box:
                Gizmos.DrawWireCube(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
                break;
            case SDFType.Capsule:
                Utilities.GizmosHelper.DrawWireCapsule(GetPosition(), GetRotation(), 0.5f * GetScale().x,
                                                       0.5f * GetScale().y, Color.cyan);
                break;
            case SDFType.Torus:
                Utilities.GizmosHelper.DrawWireTorus(GetPosition(), GetRotation(), 0.5f * GetScale().x, GetScale().y,
                                                     Color.cyan);
                break;
            case SDFType.Cylinder:
                Utilities.GizmosHelper.DrawWireCylinder(GetPosition(), GetRotation(), 0.5f * GetScale().x, GetScale().y,
                                                        Color.cyan);
                break;
            }
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }
#endif
        public void Start()
        {
            ManipType = ManipulatorType.AnalyticCollider;
            AdditionalData.x = (int)chosenSDFType;
        }
    }
}