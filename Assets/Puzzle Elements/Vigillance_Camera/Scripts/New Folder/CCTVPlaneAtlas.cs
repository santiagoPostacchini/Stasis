using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CCTVPlaneAtlas : MonoBehaviour
{
    // ======= Roles de instancia =======
    public enum InstanceMode { Master, Follower }

    [Header("Instance Mode")]
    [Tooltip("Master hace todo el render/record/playback. Follower solo muestra el atlas de un Master.")]
    public InstanceMode mode = InstanceMode.Master;

    [Tooltip("Solo para Follower: referencia al Master cuyo atlas vas a mostrar.")]
    public CCTVPlaneAtlas masterForFollower;

    // ======= Fuentes (solo Master) =======
    static readonly List<VirtualSecurityCam> s_sources = new List<VirtualSecurityCam>();
    public static void RegisterSource(VirtualSecurityCam v) { if (!s_sources.Contains(v)) s_sources.Add(v); }
    public static void UnregisterSource(VirtualSecurityCam v) { s_sources.Remove(v); }

    [Header("Sources (grid order) - Master")]
    [Tooltip("Dejar vacio = usa todas las VirtualSecurityCam registradas.")]
    public VirtualSecurityCam[] cameras;

    // ======= Target (ambos: Master/Follower) =======
    [Header("Target (Plane)")]
    public Renderer targetRenderer;

    [Tooltip("Texture property para bindear el atlas. Vacio = auto.")]
    public string texturePropertyName = "";

    // ======= Atlas (solo Master) =======
    [Header("Atlas Grid - Master")]
    public int columns = 3;
    public int tileWidth = 512;
    public int tileHeight = 288;
    public int paddingX = 8;
    public int paddingY = 8;
    public Color clearColor = Color.black;

    // ======= Render cam compartida (solo Master crea/usa) =======
    [Header("Render Camera (Shared)")]
    [Tooltip("Nombre del GameObject de la camara compartida (unica para toda la escena).")]
    public string sharedCameraGOName = "CCTV_RenderCam_SHARED";

    [Tooltip("Renderer de URP a usar (-1 = default pipeline).")]
    public int urpRendererIndex = -1;

    [Tooltip("HDR/ MSAA del render interno")]
    public bool allowHDR = false;
    public bool allowMSAA = false;

    [Header("Overlay Top Layer - Master")]
    public LayerMask overlayTopLayer = 0;
    public bool overlayClearDepth = true;

    // ======= Record / Playback (solo Master) =======
    [Header("Recording - Master")]
    public int recordFPS = 10;
    public float recordMaxSeconds = 6f;
    public RenderTextureFormat recordFormat = RenderTextureFormat.ARGB32;

    [Header("Playback - Master")]
    public Transform playbackTriggerTarget;
    public float playbackTriggerDistance = 6f;
    public float playbackBlackoutSeconds = 1.0f;

    // ======= Art Safe Controls (ambos) =======
    public enum ForceProperty { Auto, BaseMap, MainTex, Custom }

    [Header("Art - Safe Controls (sin shaders custom)")]
    [Tooltip("Fuerza URP/Unlit para que la luz no oscurezca el atlas.")]
    public bool forceURPUnlit = false;

    [Tooltip("Si esta activo, deja BaseColor=white y desactiva sombras en el plane.")]
    public bool forceUnlitSetup = true;

    [Tooltip("Propiedad de textura a la que se bindea el atlas.")]
    public ForceProperty forceTextureProperty = ForceProperty.Auto;

    [Tooltip("Usada si forceTextureProperty=Custom.")]
    public string customTextureProperty = "_BaseMap";

    [Tooltip("Tint simple via MPB.")]
    public Color planeTint = Color.white;

    [Tooltip("Boost de emision si el shader lo soporta (_EmissionColor).")]
    [Min(0f)] public float planeEmissionBoost = 0f;

    [Header("Debug (opcional)")]
    [Tooltip("Rellena el atlas con un patron de test (Master) o chequea binding (Follower).")]
    public bool artForceTestPattern = false;

    [Header("Art - RenderTextures (Master)")]
    public RenderTextureFormat atlasFormat = RenderTextureFormat.ARGB32;
    public FilterMode atlasFilterMode = FilterMode.Bilinear;
    [Range(0, 16)] public int atlasAniso = 0;

    // ======= Performance (solo Master) =======
    [Header("Performance - Master")]
    [Tooltip("Cadencia de trabajo sobre el atlas (Hz). Menor = menos costo.")]
    public float atlasUpdateFPS = 20f;

    [Tooltip("Renderizar solo si el player esta cerca del plane.")]
    public bool renderOnlyWhenVisible = true;

    [Tooltip("Maximo de camaras scene->tileRT por frame.")]
    public int maxCamRendersPerFrame = 2;

    [Tooltip("Maximo de tiles copiados al atlas por frame.")]
    public int maxBlitsPerFrame = 3;

    [Tooltip("No limpiar el atlas cada frame; solo cuando haga falta.")]
    public bool lazyClearAtlas = true;

    // ======= Internals compartidos =======
    Camera _renderCam;            // Solo la usa el Master
    static Camera s_sharedCam;    // Singleton de camara real

    RenderTexture _atlas; // Master: lo crea y escribe. Follower: lee del master.
    int _rows;
    string _resolvedProp = null;
    MaterialPropertyBlock _mpb;

    float _atlasUpdateTimer = 0f;
    bool _atlasEverCleared = false;

    // ======= Estado por camara virtual (solo Master) =======
    class CamRuntime
    {
        public RenderTexture tileRT;
        public Quaternion lastRot;
        public bool lastRotValid;
        public float noTargetTimer;

        public RenderTexture[] frames; // ring buffer
        public int frameWrite;
        public int frameCount;
        public int maxFrames;
        public float frameInterval;
        public float frameTimer;
        public bool isRecording;

        public bool playing;
        public bool inBlackout;
        public float blackoutTimer;
        public int playIndex;
        public float playTimer;
        public float playFrameInterval;

        public bool tileDirty;
        public int lastShownIndex;
    }

    Dictionary<VirtualSecurityCam, CamRuntime> _rt = new Dictionary<VirtualSecurityCam, CamRuntime>();

    // ======= API publica para Followers =======
    public RenderTexture GetCurrentAtlas() { return _atlas; }

    // ========================= LIFECYCLE =========================

    void Awake()
    {
        if (mode == InstanceMode.Master)
        {
            SetupSharedRenderCamera();
            RecreateAtlas();
        }

        SafeSetupPlaneMaterial();
        BindAtlasToPlane();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        if (mode == InstanceMode.Master)
        {
            if (cameras == null || cameras.Length == 0) cameras = s_sources.ToArray();
            InitPerCamState();
            _atlasUpdateTimer = 0f;
        }

        ApplyPlaneStyling();
    }

    void OnDestroy()
    {
        if (mode == InstanceMode.Master)
        {
            // Atlas y buffers
            if (_atlas != null)
            {
                try { if (_atlas.IsCreated()) _atlas.Release(); } catch { }
                Destroy(_atlas);
                _atlas = null;
            }

            foreach (var kv in _rt)
            {
                var cr = kv.Value;
                if (cr.tileRT != null) { try { if (cr.tileRT.IsCreated()) cr.tileRT.Release(); } catch { } Destroy(cr.tileRT); }
                if (cr.frames != null)
                {
                    for (int i = 0; i < cr.frames.Length; i++)
                    {
                        var f = cr.frames[i];
                        if (f != null) { try { if (f.IsCreated()) f.Release(); } catch { } Destroy(f); }
                    }
                }
            }
            _rt.Clear();
            // OJO: la camara compartida NO se destruye (queda viva para otros masters futuros)
        }
    }

    // ========================= SETUP =========================

    void SetupSharedRenderCamera()
    {
        // Reusar o crear una unica camara compartida (estatica)
        if (s_sharedCam != null)
        {
            _renderCam = s_sharedCam;
            ConfigureRenderCamera(_renderCam);
            return;
        }

        var existing = GameObject.Find(sharedCameraGOName);
        if (existing != null && existing.TryGetComponent<Camera>(out var foundCam))
        {
            s_sharedCam = foundCam;
            _renderCam = s_sharedCam;
            ConfigureRenderCamera(_renderCam);
            return;
        }

        var go = new GameObject(sharedCameraGOName, typeof(Camera));
        s_sharedCam = go.GetComponent<Camera>();
        _renderCam = s_sharedCam;
        DontDestroyOnLoad(go); // persiste entre escenas si lo necesitas
        ConfigureRenderCamera(_renderCam);
    }

    void ConfigureRenderCamera(Camera cam)
    {
        cam.enabled = false;
        cam.backgroundColor = clearColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.allowHDR = allowHDR;
        cam.allowMSAA = allowMSAA;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        cam.depth = -100;

        var urp = cam.GetComponent<UniversalAdditionalCameraData>();
        if (!urp) urp = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        urp.renderType = CameraRenderType.Base;
        if (urpRendererIndex >= 0) urp.SetRenderer(urpRendererIndex);
        urp.antialiasing = AntialiasingMode.None;
        urp.renderPostProcessing = false;
        urp.requiresColorTexture = false;
        urp.requiresDepthTexture = false;
        urp.stopNaN = false;
        urp.dithering = false;
    }

    void RecreateAtlas()
    {
        int count = (cameras != null) ? cameras.Length : 0;
        columns = Mathf.Max(1, columns);
        _rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

        int w = columns * tileWidth + (columns + 1) * paddingX;
        int h = _rows * tileHeight + (_rows + 1) * paddingY;

        if (_atlas != null)
        {
            if (_atlas.width == w && _atlas.height == h && _atlas.format == atlasFormat) return;
            try { if (_atlas.IsCreated()) _atlas.Release(); } catch { }
            Destroy(_atlas);
        }

        _atlas = new RenderTexture(w, h, 0, atlasFormat, RenderTextureReadWrite.Default);
        _atlas.name = "CCTV_ATLAS_" + w + "x" + h;
        _atlas.filterMode = atlasFilterMode;
        _atlas.anisoLevel = atlasAniso;
        _atlas.wrapMode = TextureWrapMode.Clamp;
        _atlas.useMipMap = false;
        _atlas.autoGenerateMips = false;
        _atlas.antiAliasing = 1;
        _atlas.Create();
        _atlasEverCleared = false;
    }

    void SafeSetupPlaneMaterial()
    {
        if (!targetRenderer) return;

        if (forceURPUnlit)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null)
            {
                var mat = targetRenderer.sharedMaterial;
                if (mat == null || mat.shader != sh)
                    targetRenderer.material = new Material(sh);

                if (forceUnlitSetup)
                {
                    targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    targetRenderer.receiveShadows = false;

                    var m = targetRenderer.material;
                    if (m != null)
                    {
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                    }
                }
            }
        }
    }

    void BindAtlasToPlane()
    {
        if (!targetRenderer) return;

        // Master: linkea su propio atlas; Follower: linkea atlas del master
        var sourceAtlas = (mode == InstanceMode.Master) ? _atlas
            : (masterForFollower != null ? masterForFollower.GetCurrentAtlas() : null);

        if (sourceAtlas == null) return;

        var mat = targetRenderer.material;
        if (mat == null) return;

        string prop = "";
        switch (forceTextureProperty)
        {
            case ForceProperty.BaseMap: prop = "_BaseMap"; break;
            case ForceProperty.MainTex: prop = "_MainTex"; break;
            case ForceProperty.Custom: prop = customTextureProperty; break;
            default: prop = ResolveTextureProperty(mat, texturePropertyName); break;
        }

        if (!string.IsNullOrEmpty(prop) && mat.HasProperty(prop))
        {
            _resolvedProp = prop;
            if (mat.GetTexture(_resolvedProp) != sourceAtlas)
                mat.SetTexture(_resolvedProp, sourceAtlas);
        }
        else
        {
            _resolvedProp = ResolveTextureProperty(mat, texturePropertyName);
            if (!string.IsNullOrEmpty(_resolvedProp) && mat.GetTexture(_resolvedProp) != sourceAtlas)
                mat.SetTexture(_resolvedProp, sourceAtlas);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
    }

    void ApplyPlaneStyling()
    {
        if (!targetRenderer) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(_mpb);

        // reafirma la textura via MPB (por si el material cambia en runtime)
        var srcAtlas = (mode == InstanceMode.Master) ? _atlas
            : (masterForFollower != null ? masterForFollower.GetCurrentAtlas() : null);
        if (!string.IsNullOrEmpty(_resolvedProp) && srcAtlas != null)
            _mpb.SetTexture(_resolvedProp, srcAtlas);

        if (planeTint != Color.white)
        {
            var sm = targetRenderer.sharedMaterial;
            if (sm != null && sm.HasProperty("_BaseColor")) _mpb.SetColor("_BaseColor", planeTint);
            if (sm != null && sm.HasProperty("_Color")) _mpb.SetColor("_Color", planeTint);
        }

        if (planeEmissionBoost > 0f)
        {
            Color e = planeTint * Mathf.LinearToGammaSpace(planeEmissionBoost);
            _mpb.SetColor("_EmissionColor", e);
        }

        targetRenderer.SetPropertyBlock(_mpb);
    }

    string ResolveTextureProperty(Material m, string preferred)
    {
        if (!string.IsNullOrEmpty(preferred) && m.HasProperty(preferred)) return preferred;
        if (m.HasProperty("_BaseMap")) return "_BaseMap";
        if (m.HasProperty("_MainTex")) return "_MainTex";
        if (m.HasProperty("_BaseColorMap")) return "_BaseColorMap";
        if (m.HasProperty("_AlbedoMap")) return "_AlbedoMap";
        return "";
    }

    Rect TileViewport01(int idx)
    {
        int col = idx % columns;
        int row = idx / columns;
        int x = paddingX + col * (tileWidth + paddingX);
        int y = paddingY + row * (tileHeight + paddingY);
        float nx = (float)x / (float)_atlas.width;
        float ny = (float)y / (float)_atlas.height;
        float nw = (float)tileWidth / (float)_atlas.width;
        float nh = (float)tileHeight / (float)_atlas.height;
        return new Rect(nx, ny, nw, nh);
    }

    void ClearAtlasAll()
    {
        var prev = RenderTexture.active;
        RenderTexture.active = _atlas;
        GL.Viewport(new Rect(0, 0, _atlas.width, _atlas.height));
        GL.Clear(true, true, clearColor);
        RenderTexture.active = prev;
        _atlasEverCleared = true;
    }

    void ClearTileColor(int tileIndex, Color col)
    {
        Rect vp = TileViewport01(tileIndex);
        int px = Mathf.RoundToInt(vp.x * _atlas.width);
        int py = Mathf.RoundToInt(vp.y * _atlas.height);
        int pw = Mathf.RoundToInt(vp.width * _atlas.width);
        int ph = Mathf.RoundToInt(vp.height * _atlas.height);

        var prev = RenderTexture.active;
        RenderTexture.active = _atlas;
        GL.Viewport(new Rect(px, py, pw, ph));
        GL.Clear(true, true, col);
        RenderTexture.active = prev;
    }

    void BlitTileToAtlas(RenderTexture src, int tileIndex)
    {
        if (src == null || !src.IsCreated()) return;
        Rect vp = TileViewport01(tileIndex);
        int px = Mathf.RoundToInt(vp.x * _atlas.width);
        int py = Mathf.RoundToInt(vp.y * _atlas.height);
        int pw = Mathf.RoundToInt(vp.width * _atlas.width);
        int ph = Mathf.RoundToInt(vp.height * _atlas.height);

        if (src.width == pw && src.height == ph)
        {
            Graphics.CopyTexture(src, 0, 0, 0, 0, src.width, src.height, _atlas, 0, 0, px, py);
        }
        else
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _atlas;
            GL.Viewport(new Rect(px, py, pw, ph));
            Graphics.Blit(src, (RenderTexture)null);
            RenderTexture.active = prev;
        }
    }

    static bool IsTargetVisible(VirtualSecurityCam v, Vector3 camPos, Vector3 camFwd)
    {
        if (v == null || v.detectionTarget == null) return false;
        Vector3 toT = v.detectionTarget.position - camPos;
        float dist = toT.magnitude;
        if (dist < v.nearClip || dist > v.farClip) return false;
        float ang = Vector3.Angle(camFwd, toT);
        if (ang > Mathf.Max(1f, v.maxViewAngle)) return false;

        RaycastHit hit;
        if (Physics.Raycast(camPos, toT.normalized, out hit, dist, v.visibilityBlockers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && hit.collider.transform != v.detectionTarget) return false;
        }
        return true;
    }

    static float AngularSpeedDegPerSec(Quaternion last, Quaternion current, float dt)
    {
        if (dt <= 0f) return 0f;
        float angle = Quaternion.Angle(last, current);
        return angle / dt;
    }

    void EnsureCamerasArray()
    {
        if (mode == InstanceMode.Master)
        {
            if (cameras == null || cameras.Length == 0) cameras = s_sources.ToArray();
        }
    }

    // ========================= UPDATE =========================
    void LateUpdate()
    {
        // Followers: solo binding + estilo; NADA de render/grab/playback
        if (mode == InstanceMode.Follower)
        {
            BindAtlasToPlane();
            ApplyPlaneStyling();

            if (artForceTestPattern && targetRenderer != null)
            {
                // Solo verifica que el binding cambie de textura en runtime
                // No escribe nada, porque el Follower no tiene atlas propio.
            }
            return;
        }

        // Master
        EnsureCamerasArray();
        if (_renderCam == null || cameras == null) return;

        RecreateAtlas(); // barato, early-out si no cambia

        // Throttle de trabajo del atlas
        float updateInterval = 1f / Mathf.Max(1f, atlasUpdateFPS);
        _atlasUpdateTimer += Time.deltaTime;
        bool doAtlasWork = _atlasUpdateTimer >= updateInterval;
        if (doAtlasWork) _atlasUpdateTimer -= updateInterval;

        // Visibilidad (para no renderizar innecesario)
        bool viewingPlane = true;
        if (renderOnlyWhenVisible && playbackTriggerTarget != null && targetRenderer != null)
        {
            Vector3 planePos = targetRenderer.bounds.center;
            float d = Vector3.Distance(planePos, playbackTriggerTarget.position);
            viewingPlane = (d <= playbackTriggerDistance);
        }

        // Clear inicial/lazy
        if (!lazyClearAtlas || !_atlasEverCleared)
            ClearAtlasAll();

        // Presupuestos
        int rendersLeft = maxCamRendersPerFrame;
        int blitsLeft = maxBlitsPerFrame;

        float dt = Time.deltaTime;
        if (cameras.Length != _rt.Count) InitPerCamState();

        // 1) UPDATE/RENDER de fuentes (solo si hace falta)
        for (int i = 0; i < cameras.Length && rendersLeft > 0; i++)
        {
            var v = cameras[i];
            if (v == null) continue;
            if (!_rt.TryGetValue(v, out var cr)) continue;

            // Si no grabamos y tampoco vamos a reproducir ahora, saltear render
            if (!cr.isRecording && !(viewingPlane && doAtlasWork)) continue;

            // ROT/SEE
            Transform p = v.Pivot;
            Vector3 desiredFwd = v.GetDesiredForward();
            if (desiredFwd.sqrMagnitude < 1e-6f) desiredFwd = p.forward;

            Quaternion rot = v.lockHorizon ? Quaternion.LookRotation(desiredFwd, Vector3.up)
                                           : (p.rotation * Quaternion.Euler(v.rotationOffsetEuler));

            Vector3 camPos = p.position;
            Vector3 camFwd = (rot * Vector3.forward);

            float angSpeed = 0f;
            if (cr.lastRotValid) angSpeed = AngularSpeedDegPerSec(cr.lastRot, rot, Mathf.Max(0.0001f, dt));
            cr.lastRot = rot; cr.lastRotValid = true;

            bool targetVisible = IsTargetVisible(v, camPos, camFwd);
            if (!targetVisible) cr.noTargetTimer += dt; else cr.noTargetTimer = 0f;

            bool startByMove = angSpeed >= v.startOnAngularSpeedDegPerSec;
            bool startBySee = targetVisible;

            if (!cr.isRecording && (startByMove || startBySee)) cr.isRecording = true;
            if (cr.isRecording && !targetVisible && cr.noTargetTimer >= Mathf.Max(0.05f, v.stopAfterNoTargetSeconds)) cr.isRecording = false;

            // Si no grabamos y no vamos a reproducir, saltear render
            if (!cr.isRecording && !(viewingPlane && doAtlasWork)) continue;

            // Render a tileRT (una sola pasada base + overlay opcional)
            int maskBase = v.cullingMask.value;
            if (overlayTopLayer != 0) maskBase = maskBase & ~overlayTopLayer.value;
            if (maskBase == 0) maskBase = ~0;

            _renderCam.transform.SetPositionAndRotation(camPos, rot);
            _renderCam.fieldOfView = v.fieldOfView;
            _renderCam.nearClipPlane = Mathf.Max(0.001f, v.nearClip);
            _renderCam.farClipPlane = v.farClip;

            _renderCam.cullingMask = maskBase;
            _renderCam.targetTexture = cr.tileRT;
            _renderCam.Render();

            if (overlayTopLayer != 0)
            {
                int overlayMask = v.cullingMask.value & overlayTopLayer.value;
                if (overlayMask != 0)
                {
                    _renderCam.cullingMask = overlayMask;
                    _renderCam.clearFlags = overlayClearDepth ? CameraClearFlags.Depth : CameraClearFlags.Nothing;
                    _renderCam.Render();
                    _renderCam.clearFlags = CameraClearFlags.SolidColor;
                }
            }
            _renderCam.targetTexture = null;
            rendersLeft--;

            // GRAB ring-buffer
            if (cr.isRecording)
            {
                cr.frameTimer += dt;
                if (cr.frameTimer >= cr.frameInterval)
                {
                    cr.frameTimer -= cr.frameInterval;
                    var dst = cr.frames[cr.frameWrite];
                    Graphics.Blit(cr.tileRT, dst);
                    cr.frameWrite = (cr.frameWrite + 1) % cr.maxFrames;
                    cr.frameCount = Mathf.Min(cr.frameCount + 1, cr.maxFrames);
                    cr.tileDirty = true;
                }
            }
        }

        // 2) PLAYBACK -> ATLAS (solo si el plane esta visible y a la cadencia)
        if (viewingPlane && doAtlasWork)
        {
            for (int i = 0; i < cameras.Length && blitsLeft > 0; i++)
            {
                var v = cameras[i];
                if (v == null) continue;
                if (!_rt.TryGetValue(v, out var cr)) continue;

                bool hasClip = cr.frameCount > 0;
                if (!hasClip)
                {
                    continue; // lazy clear ya cubre el fondo
                }

                if (!cr.playing && !cr.inBlackout)
                {
                    cr.playing = true;
                    cr.playIndex = Mathf.Clamp(cr.frameWrite - cr.frameCount, 0, cr.maxFrames - 1);
                    cr.playTimer = 0f;
                    cr.lastShownIndex = -1;
                    cr.tileDirty = true;
                }

                if (cr.inBlackout)
                {
                    cr.blackoutTimer += (1f / Mathf.Max(1f, atlasUpdateFPS));
                    if (cr.blackoutTimer >= Mathf.Max(0.01f, playbackBlackoutSeconds))
                    {
                        cr.blackoutTimer = 0f;
                        cr.inBlackout = false;
                        cr.playing = true;
                        cr.playIndex = Mathf.Clamp(cr.frameWrite - cr.frameCount, 0, cr.maxFrames - 1);
                        cr.playTimer = 0f;
                        cr.lastShownIndex = -1;
                        cr.tileDirty = true;
                    }
                }
                else
                {
                    cr.playTimer += (1f / Mathf.Max(1f, atlasUpdateFPS));
                    if (cr.playTimer >= cr.playFrameInterval)
                    {
                        cr.playTimer -= cr.playFrameInterval;
                        cr.playIndex++;
                        if (cr.playIndex >= cr.frameWrite)
                        {
                            cr.playing = false;
                            cr.inBlackout = true;
                            cr.blackoutTimer = 0f;
                            cr.playIndex = Mathf.Clamp(cr.frameWrite - cr.frameCount, 0, cr.maxFrames - 1);
                            cr.tileDirty = true;
                        }
                        else cr.tileDirty = true;
                    }

                    int ringIdx = ((cr.playIndex % cr.maxFrames) + cr.maxFrames) % cr.maxFrames;
                    if (cr.tileDirty && ringIdx != cr.lastShownIndex)
                    {
                        BlitTileToAtlas(cr.frames[ringIdx], i);
                        cr.lastShownIndex = ringIdx;
                        cr.tileDirty = false;
                        blitsLeft--;
                    }
                }
            }
        }

        // Binding/estilo liviano
        BindAtlasToPlane();
        ApplyPlaneStyling();

        if (!_atlasEverCleared && lazyClearAtlas)
            ClearAtlasAll();

        // Patron de test (solo Master)
        if (artForceTestPattern)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _atlas;
            GL.Viewport(new Rect(0, 0, _atlas.width, _atlas.height));
            GL.Clear(true, true, Color.black);
            Texture2D tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            tmp.SetPixels(new Color[] { Color.red, Color.green, Color.blue, Color.yellow });
            tmp.Apply();
            Graphics.Blit(tmp, _atlas);
            RenderTexture.active = prev;
            Destroy(tmp);
        }
    }

    // ========================= INIT POR CAM =========================
    void InitPerCamState()
    {
        _rt.Clear();
        int n = cameras != null ? cameras.Length : 0;
        int pooled = Mathf.Max(1, Mathf.FloorToInt(recordMaxSeconds * Mathf.Max(1, recordFPS)));

        for (int i = 0; i < n; i++)
        {
            var v = cameras[i];
            if (v == null) continue;

            var cr = new CamRuntime();
            cr.tileRT = CreateRT(tileWidth, tileHeight, 16, recordFormat);
            cr.frameInterval = 1f / Mathf.Max(1, recordFPS);
            cr.playFrameInterval = cr.frameInterval;
            cr.maxFrames = pooled;

            cr.frames = new RenderTexture[pooled];
            for (int k = 0; k < pooled; k++)
                cr.frames[k] = CreateRT(tileWidth, tileHeight, 0, recordFormat);

            cr.frameWrite = 0;
            cr.frameCount = 0;
            cr.isRecording = v.IsRecording;
            cr.playing = false;
            cr.inBlackout = false;
            cr.lastShownIndex = -1;
            cr.tileDirty = true;

            _rt[v] = cr;
        }
    }

    RenderTexture CreateRT(int w, int h, int depth, RenderTextureFormat fmt)
    {
        var rt = new RenderTexture(w, h, depth, fmt, RenderTextureReadWrite.Default);
        rt.name = "CCTV_RT_" + w + "x" + h + "_" + fmt;
        rt.filterMode = atlasFilterMode;
        rt.anisoLevel = atlasAniso;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.antiAliasing = 1;
        rt.Create();
        return rt;
    }
}
