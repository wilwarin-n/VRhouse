using System;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEngine.SceneManagement;
using UnityEditor;
#endif

namespace com.zibra.liquid.DataStructures
{
    [ExecuteInEditMode]
    public class ZibraLiquidMaterialParameters : MonoBehaviour
    {
#if UNITY_EDITOR
        private static string DEFAULT_FLUID_MATERIAL_GIUD = "212140388bc74764ba19ea2bbe4ce94c";
        private static string DEFAULT_UPSCALE_MATERIAL_GIUD = "374557399a8cb1b499aee6a0cc226496";
#endif

        [Tooltip("Custom fluid material.")]
        public Material FluidMaterial;

        [Tooltip("Custom upscale material. Not used if you don't enable downscale in Liquid instance.")]
        public Material UpscaleMaterial;

        [NonSerialized]
        [Obsolete("RefractionColor is deprecated. Use Color instead.", true)]
        public Color RefractionColor;

        [Tooltip("The color of the liquid body")]
        [FormerlySerializedAs("RefractionColor")]
        public Color Color = new Color(.34f, .85f, .92f, 1.0f);

        [Tooltip("The color of the liquid reflection.")]
        [ColorUsage(true, true)]
#if UNITY_PIPELINE_HDRP
        public Color ReflectionColor = new Color(0.0625f, 0.0625f, 0.0625f, 1.0f);
#else
        public Color ReflectionColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
#endif

        [NonSerialized]
        [HideInInspector]
        [Obsolete(
            "Smoothness is deprecated. Use Roughness instead. Roughness have inverted scale, i.e. Smoothness = 1.0 is equivalent to Roughness = 0.0",
            true)]
        public float Smoothness = 0.96f;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("Smoothness")]
        private float SmoothnessOld = 0.96f;

        [Range(0.0f, 1.0f)]
        public float Roughness = 0.04f;

        [NonSerialized]
        [Obsolete("Metal is deprecated. Use Metalness instead.", true)]
        public float Metal;

        [Tooltip("The metalness of the surface")]
        [FormerlySerializedAs("Metal")]
        [Range(0.0f, 1.0f)]
        public float Metalness = 0.3f;

        [Tooltip("The opacity of the liquid body")]
        [Range(0.0f, 100.0f)]
        public float Opacity = 3.0f;

        [HideInInspector]
        [Obsolete("Shadowing is deprecated. We currently don't have correct shadowing effect.", true)]
        public float Shadowing;

        [NonSerialized]
        [Obsolete("RefractionDistort is deprecated. Use RefractionDistortion instead.", true)]
        public float RefractionDistort;

        [Tooltip("The strength of refraction")]
        [FormerlySerializedAs("RefractionDistort")]
        [Range(0.0f, 1.0f)]
        public float RefractionDistortion = 0.15f;

        [Tooltip("Particle rendering scale compared to the cell size")]
        [Range(0.0f, 4.0f)]
        public float ParticleScale = 1.5f;

        [NonSerialized]
        [Obsolete("Foam is deprecated. Use FoamIntensity instead.", true)]
        public float Foam;

        [FormerlySerializedAs("Foam")]
        [Range(0.0f, 2.0f)]
        public float FoamIntensity = 0.8f;

        [NonSerialized]
        [Obsolete("FoamDensity is deprecated. Use FoamAmount instead.", true)]
        public float FoamDensity;

        [Tooltip("Foam appearance threshold")]
        [FormerlySerializedAs("FoamDensity")]
        [Range(0.0f, 4.0f)]
        public float FoamAmount = 1.0f;

        [Range(0.0001f, 0.1f)]
        public float BlurRadius = 0.04f;

        [HideInInspector]
        [Range(0.0f, 20.0f)]
        public float BilateralWeight = 2.5f;

        [HideInInspector]
        [SerializeField]
        private int ObjectVersion = 1;

#if UNITY_EDITOR
        void OnSceneOpened(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            Debug.Log("Zibra Liquid Material Parameters format was updated. Please resave scene.");
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        [ExecuteInEditMode]
        public void Awake()
        {
            // If Material Parameters is in old format we need to parse old parameters and come up with equivalent new
            // ones
            if (ObjectVersion == 1)
            {
                Roughness = 1 - SmoothnessOld;

                ObjectVersion = 2;
#if UNITY_EDITOR
                // Can't mark object dirty in Awake, since scene is not fully loaded yet
                UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
#endif
            }
        }

#if UNITY_EDITOR
        public void OnDestroy()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        void Reset()
        {
            string DefaultFluidMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_FLUID_MATERIAL_GIUD);
            FluidMaterial = AssetDatabase.LoadAssetAtPath(DefaultFluidMaterialPath, typeof(Material)) as Material;
            string DefaultUpscaleMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_UPSCALE_MATERIAL_GIUD);
            UpscaleMaterial = AssetDatabase.LoadAssetAtPath(DefaultUpscaleMaterialPath, typeof(Material)) as Material;
        }

        void OnValidate()
        {
            if (FluidMaterial == null)
            {
                string DefaultFluidMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_FLUID_MATERIAL_GIUD);
                FluidMaterial = AssetDatabase.LoadAssetAtPath(DefaultFluidMaterialPath, typeof(Material)) as Material;
            }
            if (UpscaleMaterial == null)
            {
                string DefaultUpscaleMaterialPath = AssetDatabase.GUIDToAssetPath(DEFAULT_UPSCALE_MATERIAL_GIUD);
                UpscaleMaterial =
                    AssetDatabase.LoadAssetAtPath(DefaultUpscaleMaterialPath, typeof(Material)) as Material;
            }
        }
#endif
    }
}