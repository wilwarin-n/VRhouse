#if ZIBRA_LIQUID_PAID_VERSION

using com.zibra.liquid.DataStructures;
using com.zibra.liquid.Solver;
using com.zibra.liquid.Utilities;
using com.zibra.liquid;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.zibra.liquid.SDFObjects
{
    [ExecuteInEditMode] // Careful! This makes script execute in edit mode.
    // Use "EditorApplication.isPlaying" for play mode only check.
    // Encase this check and "using UnityEditor" in "#if UNITY_EDITOR" preprocessor directive to prevent build errors
    [AddComponentMenu("Zibra/Zibra Neural Collider")]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class ZibraNeuralCollider : SDFCollider
    {
        private const int EMBED_COORDINATES_COUNT = 3;
        private const int SDF_APPROX_DIMENSION = 40;
        private const int SDF_GRID_DIMENSION = 32;
        private const int GRID_SIZE = SDF_GRID_DIMENSION * SDF_GRID_DIMENSION * SDF_GRID_DIMENSION;
        private const int SDF_APPX_SIZE = SDF_APPROX_DIMENSION * SDF_APPROX_DIMENSION * SDF_APPROX_DIMENSION;
        private const int EMBEDDING_SIZE = 29;
        private const int ALIGNED_EMBEDDING_SIZE = 32;
        private const int VOXEL_COUNT_ALIGNMENT = 32;

        private int VoxelCount;

        [SerializeField]
        public Vector3 BoundingBoxMin;
        [SerializeField]
        public Vector3 BoundingBoxMax;

        [SerializeField]
        public VoxelRepresentation currentRepresentation = null;

        public VoxelRepresentation CurrentRepresentation => currentRepresentation;
        [HideInInspector]
        public bool HasRepresentation;

        private VoxelEmbedding[] VoxelInfo;

        public void CreateRepresentation()
        {
            VoxelInfo = UnpackRepresentation();

            if (VoxelInfo != null)
            {
                HasRepresentation = true;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.grey;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(0.5f * (BoundingBoxMin + BoundingBoxMax), (BoundingBoxMin - BoundingBoxMax));
        }

        void OnDrawGizmos()
        {
            OnDrawGizmosSelected();
        }

        public override void InitializeConstData()
        {
            ColliderIndex = AllColliders.IndexOf(this);
            HasRepresentation = false;
            CreateRepresentation();

            int alignedVoxelCount = ((VoxelCount + (VOXEL_COUNT_ALIGNMENT - 1)) & (~(VOXEL_COUNT_ALIGNMENT - 1)));
            int arraySize = alignedVoxelCount + VoxelCount * ALIGNED_EMBEDDING_SIZE + SDF_APPX_SIZE;
            Array.Resize<float>(ref ConstAdditionalData, arraySize);

            for (var i = 0; i < VoxelCount; i++)
            {
                VoxelEmbedding cur = VoxelInfo[i];

                Array.Copy(cur.embedding, 0, ConstAdditionalData, i * ALIGNED_EMBEDDING_SIZE + alignedVoxelCount,
                           EMBEDDING_SIZE);
                ConstAdditionalData[i] = cur.coords.x + SDF_GRID_DIMENSION * cur.coords.y +
                                         SDF_GRID_DIMENSION * SDF_GRID_DIMENSION * cur.coords.z;
            }
        }

        public void Initialize()
        {
            ManipType = ManipulatorType.NeuralCollider;

            if (!isInitialized) // if has not been initialized at all
            {
                ColliderIndex = AllColliders.IndexOf(this);
                HasRepresentation = false;
                CreateRepresentation();

                colliderParams.CurrentID = ColliderIndex;
                colliderParams.VoxelCount = VoxelCount;
                colliderParams.BBoxMin = BoundingBoxMin;
                colliderParams.BBoxMax = BoundingBoxMax;
                colliderParams.colliderIndex = ColliderIndex;

                AdditionalData.x = (float)chosenSDFType;
                AdditionalData.y = (float)VoxelCount;
            }
        }

        // on game start
        protected void Start()
        {
            colliderParams = new ColliderParams();
            NativeDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(colliderParams));
            ManipType = ManipulatorType.NeuralCollider;
            Initialize();
        }

#if UNITY_EDITOR

        public Mesh GetMesh()
        {
            Renderer currentRenderer = GetComponent<Renderer>();

            if (currentRenderer == null)
            {
#if UNITY_EDITOR
                EditorUtility.DisplayDialog(
                    "Zibra Liquid Mesh Error",
                    "Render component absent on this object. " +
                        "Add this component only to objects with MeshFilter or SkinnedMeshRenderer components",
                    "Ok");
#endif
                return null;
            }

            if (currentRenderer is MeshRenderer meshRenderer)
            {
                var MeshFilter = meshRenderer.GetComponent<MeshFilter>();

                if (MeshFilter == null)
                {
#if UNITY_EDITOR
                    EditorUtility.DisplayDialog(
                        "Zibra Liquid Mesh Error",
                        "MeshFilter absent on this object. MeshRenderer requires MeshFilter to operate correctly.",
                        "Ok");
#endif
                    return null;
                }

                if (MeshFilter.sharedMesh == null)
                {
#if UNITY_EDITOR
                    EditorUtility.DisplayDialog(
                        "Zibra Liquid Mesh Error",
                        "No mesh found on this object. Attach mesh to the MeshFilter before generating representation.",
                        "Ok");
#endif
                    return null;
                }

                return MeshFilter.sharedMesh;
            }

            if (currentRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                var mesh = new Mesh();
                skinnedMeshRenderer.BakeMesh(mesh);

                return mesh;
            }

#if UNITY_EDITOR
            EditorUtility.DisplayDialog(
                "Zibra Liquid Mesh Error",
                "Unsupported Renderer type. Only MeshRenderer and SkinnedMeshRenderer are supported at the moment.",
                "Ok");
#endif
            return null;
        }
#endif

        public VoxelEmbedding[] UnpackRepresentation()
        {
            if (currentRepresentation == null || string.IsNullOrEmpty(currentRepresentation.embeds) ||
                string.IsNullOrEmpty(currentRepresentation.vox_ids))
            {
                return null;
            }

            var embeddings = currentRepresentation.embeds.StringToFloat();
            var voxIds = currentRepresentation.vox_ids.StringToInt();

            VoxelCount = voxIds.Length / EMBED_COORDINATES_COUNT;

            if (currentRepresentation.shape == 0 || currentRepresentation.shape != EMBEDDING_SIZE ||
                (embeddings.Length % currentRepresentation.shape) != 0 ||
                (voxIds.Length % EMBED_COORDINATES_COUNT) != 0 ||
                (embeddings.Length / currentRepresentation.shape) != (voxIds.Length / EMBED_COORDINATES_COUNT))
            {
                Debug.LogError("Incorrect data format after parsing base64 strings");
                return null;
            }

            var unpackedRepresentation = new VoxelEmbedding[VoxelCount];

            for (var i = 0; i < unpackedRepresentation.Length; i++)
            {
                var currentEmbedding = new float[currentRepresentation.shape];
                Array.Copy(embeddings, i * currentRepresentation.shape, currentEmbedding, 0,
                           currentRepresentation.shape);
                unpackedRepresentation[i] =
                    new VoxelEmbedding() { coords = new Vector3Int(voxIds[i * EMBED_COORDINATES_COUNT],
                                                                   voxIds[i * EMBED_COORDINATES_COUNT + 1],
                                                                   voxIds[i * EMBED_COORDINATES_COUNT + 2]),
                                           embedding = currentEmbedding };
            }

            return unpackedRepresentation;
        }

        public override ulong GetMemoryFootrpint()
        {
            ulong result = 0;
            if (currentRepresentation.vox_ids == null)
                return result;

            result += GRID_SIZE * 4 * sizeof(int);       // VoxelPositions
            result += 2 * GRID_SIZE * 4 * sizeof(float); // VoxelIDGrid
            VoxelCount = currentRepresentation.vox_ids.StringToInt().Length / EMBED_COORDINATES_COUNT;
            result += (ulong)(EMBEDDING_SIZE * VoxelCount * sizeof(float)); // VoxelEmbeddings

            return result;
        }
    }
}

#endif