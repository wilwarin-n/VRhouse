#if ZIBRA_LIQUID_PAID_VERSION

using com.zibra.liquid.DataStructures;
using com.zibra.liquid.SDFObjects;
using System;
using com.zibra.liquid.Utilities;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace com.zibra.liquid.Editor.SDFObjects
{
    [CustomEditor(typeof(ZibraNeuralCollider))]
    [CanEditMultipleObjects]
    public class ZibraNeuralColliderEditor : UnityEditor.Editor
    {
        // Limits for representation generation web requests
        private const uint REQUEST_TRIANGLE_COUNT_LIMIT = 100000;
        private const uint REQUEST_SIZE_LIMIT = 3 << 20; // 3mb

        static ZibraNeuralColliderEditor EditorInstance;

        class ZibraNeuralColliderGenerator
        {
            public ZibraNeuralColliderGenerator(ZibraNeuralCollider ZibraNeuralCollider)
            {
                this.ZibraNeuralColliderInstance = ZibraNeuralCollider;
            }

            public ZibraNeuralCollider GetCollider()
            {
                return ZibraNeuralColliderInstance;
            }

            private Vector3[] VertexCachedBuffer;
            private Bounds MeshBounds;
            private ZibraNeuralCollider ZibraNeuralColliderInstance;
            private UnityWebRequest CurrentRequest;
            public void CreateMeshBBCube()
            {
                Mesh mesh = ZibraNeuralColliderInstance.GetComponent<MeshFilter>().sharedMesh;

                if (mesh == null)
                {
                    return;
                }

                MeshBounds = mesh.bounds;

                ZibraNeuralColliderInstance.BoundingBoxMax = MeshBounds.max;
                ZibraNeuralColliderInstance.BoundingBoxMin = MeshBounds.min;

                Vector3 lengths = MeshBounds.size;
                float max_length = Math.Max(Math.Max(lengths.x, lengths.y), lengths.z);
                // for every direction (X,Y,Z)
                if (max_length != lengths.x)
                {
                    float delta = max_length -
                                  lengths.x; // compute difference between largest length and current (X,Y or Z) length
                    ZibraNeuralColliderInstance.BoundingBoxMin.x =
                        MeshBounds.min.x - (delta / 2.0f); // pad with half the difference before current min
                    ZibraNeuralColliderInstance.BoundingBoxMax.x =
                        MeshBounds.max.x + (delta / 2.0f); // pad with half the difference behind current max
                }

                if (max_length != lengths.y)
                {
                    float delta = max_length -
                                  lengths.y; // compute difference between largest length and current (X,Y or Z) length
                    ZibraNeuralColliderInstance.BoundingBoxMin.y =
                        MeshBounds.min.y - (delta / 2.0f); // pad with half the difference before current min
                    ZibraNeuralColliderInstance.BoundingBoxMax.y =
                        MeshBounds.max.y + (delta / 2.0f); // pad with half the difference behind current max
                }

                if (max_length != lengths.z)
                {
                    float delta = max_length -
                                  lengths.z; // compute difference between largest length and current (X,Y or Z) length
                    ZibraNeuralColliderInstance.BoundingBoxMin.z =
                        MeshBounds.min.z - (delta / 2.0f); // pad with half the difference before current min
                    ZibraNeuralColliderInstance.BoundingBoxMax.z =
                        MeshBounds.max.z + (delta / 2.0f); // pad with half the difference behind current max
                }

                // Next snippet adresses the problem reported here: https://github.com/Forceflow/cuda_voxelizer/issues/7
                // Suspected cause: If a triangle is axis-aligned and lies perfectly on a voxel edge, it sometimes gets
                // counted / not counted Probably due to a numerical instability (division by zero?) Ugly fix: we pad
                // the bounding box on all sides by 1/10001th of its total length, bringing all triangles ever so
                // slightly off-grid
                Vector3 epsilon = ZibraNeuralColliderInstance.BoundingBoxMax - ZibraNeuralColliderInstance.BoundingBoxMin;
                epsilon /= 10001.0f;
                ZibraNeuralColliderInstance.BoundingBoxMin -= epsilon;
                ZibraNeuralColliderInstance.BoundingBoxMax += epsilon;
            }

            public void Start()
            {
                var mesh = ZibraNeuralColliderInstance.GetMesh();

                if (mesh == null)
                {
                    return;
                }

                if (mesh.triangles.Length / 3 > REQUEST_TRIANGLE_COUNT_LIMIT)
                {
                    string errorMessage =
                        $"Mesh is too large. Can't generate representation. Triangle count should not exceed {REQUEST_TRIANGLE_COUNT_LIMIT} triangles, but current mesh have {mesh.triangles.Length / 3} triangles";
                    EditorUtility.DisplayDialog("ZibraLiquid Error.", errorMessage, "OK");
                    Debug.LogError(errorMessage);
                    return;
                }

                if (!EditorApplication.isPlaying)
                {
                    VertexCachedBuffer = new Vector3[mesh.vertices.Length];
                    Array.Copy(mesh.vertices, VertexCachedBuffer, mesh.vertices.Length);
                }

                var meshRepresentation = new MeshRepresentation { vertices = mesh.vertices.Vector3ToString(),
                                                                  faces = mesh.triangles.IntToString() };

                if (CurrentRequest != null)
                {
                    CurrentRequest.Dispose();
                    CurrentRequest = null;
                }

                var json = JsonUtility.ToJson(meshRepresentation);

                if (json.Length > REQUEST_SIZE_LIMIT)
                {
                    string errorMessage =
                        $"Mesh is too large. Can't generate representation. Please decrease vertex/triangle count. Web request should not exceed {REQUEST_SIZE_LIMIT / (1 << 20):N2}mb, but for current mesh {(float)json.Length / (1 << 20):N2}mb is needed.";
                    EditorUtility.DisplayDialog("ZibraLiquid Error.", errorMessage, "OK");
                    Debug.LogError(errorMessage);
                    return;
                }

                if (ZibraServerAuthenticationManager.GetInstance().IsLicenseKeyValid)
                {
                    string requestURL = ZibraServerAuthenticationManager.GetInstance().GenerationURL;

                    if (requestURL != "")
                    {
                        CurrentRequest = UnityWebRequest.Post(requestURL, json);
                        CurrentRequest.SendWebRequest();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Zibra Liquid Error",
                                                ZibraServerAuthenticationManager.GetInstance().ErrorText, "Ok");
                    Debug.LogError(ZibraServerAuthenticationManager.GetInstance().ErrorText);
                }
            }

            public void Abort()
            {
                CurrentRequest?.Dispose();
            }

            public void Update()
            {
                if (CurrentRequest != null && CurrentRequest.isDone)
                {
                    VoxelRepresentation newRepresentation = null;

#if UNITY_2020_2_OR_NEWER
                    if (CurrentRequest.isDone && CurrentRequest.result == UnityWebRequest.Result.Success)
#else
                    if (CurrentRequest.isDone && !CurrentRequest.isHttpError && !CurrentRequest.isNetworkError)
#endif
                    {
                        var json = CurrentRequest.downloadHandler.text;
                        newRepresentation = JsonUtility.FromJson<VoxelRepresentation>(json);

                        if (string.IsNullOrEmpty(newRepresentation.embeds) ||
                            string.IsNullOrEmpty(newRepresentation.vox_ids) || newRepresentation.shape <= 0)
                        {
                            EditorUtility.DisplayDialog("Zibra Liquid Server Error",
                                                        "Server returned empty result. Connect ZibraLiquid support",
                                                        "Ok");
                            Debug.LogError("Server returned empty result. Connect ZibraLiquid support");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Zibra Liquid Server Error", CurrentRequest.error, "Ok");
                        Debug.LogError(CurrentRequest.downloadHandler.text);
                    }

                    CurrentRequest.Dispose();
                    CurrentRequest = null;

                    ZibraNeuralColliderInstance.currentRepresentation = newRepresentation;
                    CreateMeshBBCube();
                    ZibraNeuralColliderInstance.CreateRepresentation();
                }
            }

            public bool IsFinished()
            {
                return CurrentRequest == null;
            }
        }

        static class GenerationQueue
        {
            static Queue<ZibraNeuralColliderGenerator> CollidersToGenerate = new Queue<ZibraNeuralColliderGenerator>();

            static void Update()
            {
                CollidersToGenerate.Peek().Update();
                if (CollidersToGenerate.Peek().IsFinished())
                {
                    RemoveFromQueue();
                    if (CollidersToGenerate.Count > 0)
                    {
                        CollidersToGenerate.Peek().Start();
                    }
                    if (EditorInstance)
                    {
                        EditorInstance.Repaint();
                    }
                }
            }

            static void RemoveFromQueue()
            {
                CollidersToGenerate.Dequeue();
                if (CollidersToGenerate.Count == 0)
                {
                    EditorApplication.update -= Update;
                }
            }

            static public void AddToQueue(ZibraNeuralColliderGenerator generator)
            {
                if (!CollidersToGenerate.Contains(generator))
                {
                    if (CollidersToGenerate.Count == 0)
                    {
                        EditorApplication.update += Update;
                        generator.Start();
                    }
                    CollidersToGenerate.Enqueue(generator);
                }
            }

            static public void Abort()
            {
                if (CollidersToGenerate.Count > 0)
                {
                    CollidersToGenerate.Peek().Abort();
                    CollidersToGenerate.Clear();
                    EditorApplication.update -= Update;
                }
            }

            static public int GetQueueLength()
            {
                return CollidersToGenerate.Count;
            }

            static public bool IsInQueue(ZibraNeuralCollider collider)
            {
                foreach (var item in CollidersToGenerate)
                {
                    if (item.GetCollider() == collider)
                        return true;
                }
                return false;
            }
        }

        private ZibraNeuralCollider[] ZibraNeuralColliders;

        private SerializedProperty ForceInteraction;
        private SerializedProperty InvertSDF;
        private SerializedProperty FluidFriction;

        protected void Awake()
        {
            ZibraServerAuthenticationManager.GetInstance().Initialize(true);
        }

        protected void OnEnable()
        {
            EditorInstance = this;

            ZibraNeuralColliders = new ZibraNeuralCollider[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                ZibraNeuralColliders[i] = targets[i] as ZibraNeuralCollider;
            }

            ForceInteraction = serializedObject.FindProperty("ForceInteraction");
            InvertSDF = serializedObject.FindProperty("InvertSDF");
            FluidFriction = serializedObject.FindProperty("FluidFriction");
        }

        protected void OnDisable()
        {
            if (EditorInstance == this)
            {
                EditorInstance = null;
            }
        }

        private void GenerateColliders(bool regenerate = false)
        {
            foreach (var instance in ZibraNeuralColliders)
            {
                if (!GenerationQueue.IsInQueue(instance) && (!instance.HasRepresentation || regenerate))
                {
                    GenerationQueue.AddToQueue(new ZibraNeuralColliderGenerator(instance));
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (EditorApplication.isPlaying)
            {
                // Don't allow generation in playmode
            }
            else if (!ZibraServerAuthenticationManager.GetInstance().IsLicenseKeyValid)
            {
                GUILayout.Label("Licence key validation in progress");

                GUILayout.Space(20);
            }
            else
            {
                int toGenerateCount = 0;
                int toRegenerateCount = 0;

                foreach (var instance in ZibraNeuralColliders)
                {
                    if (!GenerationQueue.IsInQueue(instance))
                    {
                        if (instance.HasRepresentation)
                        {
                            toRegenerateCount++;
                        }
                        else
                        {
                            toGenerateCount++;
                        }
                    }
                }

                int inQueueCount = ZibraNeuralColliders.Length - toGenerateCount - toRegenerateCount;
                int fullQueueLength = GenerationQueue.GetQueueLength();
                if (fullQueueLength > 0)
                {
                    if (fullQueueLength != inQueueCount)
                    {
                        if (inQueueCount == 0)
                        {
                            GUILayout.Label($"Generating other colliders. {fullQueueLength} left in total.");
                        }
                        else
                        {
                            GUILayout.Label(
                                $"Generating colliders. {inQueueCount} left out of selected colliders. {fullQueueLength} colliders left in total.");
                        }
                    }
                    else
                    {
                        GUILayout.Label(ZibraNeuralColliders.Length > 1 ? $"Generating colliders. {inQueueCount} left."
                                                                  : "Generating collider.");
                    }
                    if (GUILayout.Button("Abort"))
                    {
                        GenerationQueue.Abort();
                    }

                    GUILayout.Space(10);
                }

                if (toGenerateCount > 0)
                {
                    GUILayout.Label(ZibraNeuralColliders.Length > 1
                                        ? $"{toGenerateCount} colliders doesn't have representation."
                                        : "Collider doesn't have representation.");
                    if (GUILayout.Button(ZibraNeuralColliders.Length > 1 ? "Generate colliders" : "Generate collider"))
                    {
                        GenerateColliders();
                    }
                }

                if (toRegenerateCount > 0)
                {
                    GUILayout.Label(ZibraNeuralColliders.Length > 1 ? $"{toRegenerateCount} colliders already generated."
                                                              : "Collider already generated.");
                    if (GUILayout.Button(ZibraNeuralColliders.Length > 1 ? "Regenerate all selected colliders"
                                                                   : "Regenerate collider"))
                    {
                        GenerateColliders(true);
                    }
                }

                if (toGenerateCount != 0 || toRegenerateCount != 0)
                {
                    GUILayout.Space(10);
                }
            }

            bool isColliderComponentMissing = false;
            foreach (var instance in ZibraNeuralColliders)
            {
                if (instance.GetComponent<Collider>() == null)
                {
                    isColliderComponentMissing = true;
                    break;
                }
            }

            if (isColliderComponentMissing &&
                GUILayout.Button(ZibraNeuralColliders.Length > 1 ? "Add Unity Colliders" : "Add Unity Collider"))
            {
                foreach (var instance in ZibraNeuralColliders)
                {
                    if (instance.GetComponent<Collider>() == null)
                    {
                        instance.gameObject.AddComponent<MeshCollider>();
                    }
                }
            }

            EditorGUILayout.PropertyField(FluidFriction);
            EditorGUILayout.PropertyField(ForceInteraction);
            EditorGUILayout.PropertyField(InvertSDF);

            bool isRigidbodyComponentMissing = false;
            foreach (var instance in ZibraNeuralColliders)
            {
                if (instance.ForceInteraction && instance.GetComponent<Rigidbody>() == null)
                {
                    isRigidbodyComponentMissing = true;
                    break;
                }
            }

            if (isRigidbodyComponentMissing &&
                GUILayout.Button(ZibraNeuralColliders.Length > 1 ? "Add Unity Rigidbodies" : "Add Unity Rigidbody"))
            {
                foreach (var instance in ZibraNeuralColliders)
                {
                    if (instance.ForceInteraction && instance.GetComponent<Rigidbody>() == null)
                    {
                        instance.gameObject.AddComponent<Rigidbody>();
                    }
                }
            }

            ulong totalMemoryFootprint = 0;
            foreach (var instance in ZibraNeuralColliders)
            {
                if (instance.HasRepresentation)
                {
                    totalMemoryFootprint += instance.GetMemoryFootrpint();
                }
            }

            if (totalMemoryFootprint != 0)
            {
                GUILayout.Space(10);

                if (ZibraNeuralColliders.Length > 1)
                {
                    GUILayout.Label("Multiple voxel colliders selected. Showing sum of all selected instances.");
                }
                GUILayout.Label($"Approximate VRAM footprint:{(float)totalMemoryFootprint / (1 << 20):N2}MB");
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
