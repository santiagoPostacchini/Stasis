using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MirzaBeig.VolumetricFogLite
{
    public class VolumetricFogControllerLite : MonoBehaviour
    {
        [Header("Material del efecto (shader del fog)")]
        public Material material;

        [Header("UI Sliders (opcional)")]
        public Slider slider_raymarchSteps;
        public Slider slider_downsampleLevel;
        public Slider slider_mainLightIntensity;

        [Header("Detección del Renderer Feature")]
        [Tooltip("Si lo asignas, se usa este feature directamente y se evita la búsqueda por reflexión.")]
        public ScriptableRendererFeature manualRendererFeature;

        const string keyword_MAIN_LIGHT_ENABLED = "_MAIN_LIGHT_ENABLED";
        public const string materialPropertyName_raymarchSteps = "_Raymarch_Steps";
        public const string materialPropertyName_mainLightIntensity = "_Main_Light_Intensity";

        public ScriptableRendererFeature rendererFeature { get; private set; }
        public IVolumetricFog volumetricFogCommonInterface { get; private set; }

        // Cache de lista de features via reflexión (distintas versiones URP)
        static readonly BindingFlags k_InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        static FieldInfo s_fi_mRendererFeatures;
        static PropertyInfo s_pi_rendererFeatures;

        void Awake()
        {
            // Engancho sliders si están asignados (evita orden de inicialización frágil).
            if (slider_raymarchSteps)
                slider_raymarchSteps.onValueChanged.AddListener(SetRaymarchSteps);

            if (slider_downsampleLevel)
                slider_downsampleLevel.onValueChanged.AddListener(SetDownsampleLevel);

            if (slider_mainLightIntensity)
                slider_mainLightIntensity.onValueChanged.AddListener(SetMainLightIntensity);
        }

        void Start()
        {
            // Intento inicializar el feature y la interfaz. Si no puedo, desactivo el componente para evitar spam.
            if (!TryInitializeVolumetricFog(out string reason))
            {
                Debug.LogWarning($"[VolumetricFogControllerLite] No se pudo inicializar: {reason}. " +
                                 $"Verifica que el Renderer Feature esté agregado/activo en tu URP Renderer. " +
                                 $"Puedes arrastrarlo al campo 'manualRendererFeature' para evitar la auto-detección.", this);
                enabled = false;
                return;
            }

            // Aplico valores iniciales (si hay sliders; si no, uso valores por defecto defensivos).
            if (slider_raymarchSteps) SetRaymarchSteps(slider_raymarchSteps.value);
            else SetRaymarchSteps(64f);

            if (slider_downsampleLevel) SetDownsampleLevel(slider_downsampleLevel.value);
            else SetDownsampleLevel(1f);

            if (slider_mainLightIntensity) SetMainLightIntensity(slider_mainLightIntensity.value);
            else SetMainLightIntensity(1.0f);
        }

        bool TryInitializeVolumetricFog(out string reason)
        {
            reason = null;

            if (!material)
            {
                reason = "Material no asignado";
                return false;
            }

            // 1) Si el usuario lo asignó manualmente, úsalo.
            if (manualRendererFeature)
            {
                rendererFeature = manualRendererFeature;
                volumetricFogCommonInterface = rendererFeature as IVolumetricFog;

                if (volumetricFogCommonInterface == null)
                {
                    reason = $"El 'manualRendererFeature' ({rendererFeature.name}) no implementa IVolumetricFog";
                    return false;
                }

                if (!rendererFeature.isActive)
                {
                    Debug.LogWarning($"[VolumetricFogControllerLite] El feature '{rendererFeature.name}' está INACTIVO. Actívalo en el Renderer.", rendererFeature);
                }
                return true;
            }

            // 2) Auto-detección a partir del URP Renderer(0)
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                reason = "No hay UniversalRenderPipelineAsset activo (GraphicsSettings.currentRenderPipeline es null o no es URP)";
                return false;
            }

            var renderer = urp.GetRenderer(0);
            if (renderer == null)
            {
                reason = "URP.GetRenderer(0) devolvió null (revisa tu asset de Renderer en el URP Asset)";
                return false;
            }

            // Intentar obtener lista de features por propiedad pública/privada según versión URP
            var features = GetRendererFeatures(renderer);
            if (features == null || features.Count == 0)
            {
                reason = "No se encontraron Renderer Features en el Renderer 0";
                return false;
            }

            // Preferir activo + que implemente IVolumetricFog
            rendererFeature = features.FirstOrDefault(f => f != null && f.isActive && f is IVolumetricFog);

            // Si no hay activo, agarrar cualquiera que implemente la interfaz (para al menos enlazar)
            if (rendererFeature == null)
                rendererFeature = features.FirstOrDefault(f => f != null && f is IVolumetricFog);

            if (rendererFeature == null)
            {
                var names = string.Join(", ", features.Select(f => f ? f.name : "<null>"));
                reason = $"No hay ningún Renderer Feature que implemente IVolumetricFog. Features disponibles: [{names}]";
                return false;
            }

            volumetricFogCommonInterface = rendererFeature as IVolumetricFog;
            if (volumetricFogCommonInterface == null)
            {
                reason = $"El feature detectado '{rendererFeature.name}' no implementa IVolumetricFog (cambio de versión o asset incorrecto).";
                return false;
            }

            if (!rendererFeature.isActive)
            {
                Debug.LogWarning($"[VolumetricFogControllerLite] El feature '{rendererFeature.name}' está INACTIVO. Actívalo en el Renderer.", rendererFeature);
            }

            return true;
        }

        static List<ScriptableRendererFeature> GetRendererFeatures(ScriptableRenderer renderer)
        {
            // En URP recientes existe la propiedad pública rendererFeatures; en otras, está interna/privada.
            // Intentamos ambas rutas para ser compatibles.
            if (s_pi_rendererFeatures == null)
                s_pi_rendererFeatures = typeof(ScriptableRenderer).GetProperty("rendererFeatures", BindingFlags.Public | BindingFlags.Instance)
                                         ?? typeof(ScriptableRenderer).GetProperty("rendererFeatures", k_InstanceNonPublic);

            if (s_pi_rendererFeatures != null)
            {
                var val = s_pi_rendererFeatures.GetValue(renderer) as List<ScriptableRendererFeature>;
                if (val != null) return val;
            }

            if (s_fi_mRendererFeatures == null)
                s_fi_mRendererFeatures = typeof(ScriptableRenderer).GetField("m_RendererFeatures", k_InstanceNonPublic);

            if (s_fi_mRendererFeatures != null)
            {
                var val = s_fi_mRendererFeatures.GetValue(renderer) as List<ScriptableRendererFeature>;
                if (val != null) return val;
            }

            return null;
        }

        // -------------------------
        // Setters públicos (seguro-nulos)
        // -------------------------
        public void SetRaymarchSteps(float value)
        {
            if (!material) return;
            material.SetInt(materialPropertyName_raymarchSteps, Mathf.RoundToInt(value));
        }

        public void SetDownsampleLevel(float value)
        {
            if (volumetricFogCommonInterface == null) return;
            volumetricFogCommonInterface.SetDownsampleLevel(Mathf.RoundToInt(value));
        }

        public void SetMainLightIntensity(float value)
        {
            if (!material) return;

            material.SetFloat(materialPropertyName_mainLightIntensity, value);

            // Toggle keyword
            if (value > 0.0f)
                material.EnableKeyword(keyword_MAIN_LIGHT_ENABLED);
            else
                material.DisableKeyword(keyword_MAIN_LIGHT_ENABLED);
        }
    }
}
