// CCTVPlaneAtlas.cs
// MASTER/FOLLOWER optimizado, Manual layout, centrado horizontal por fila.
// Fix: la duracion de playback ahora recorre exactamente frameCount frames (maneja wrap correctamente).
// Comentarios en ASCII.

using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CCTVPlaneAtlas : MonoBehaviour
{
    public enum InstanceMode { Master, Follower }

    [Header("Instance Mode")]
    [Tooltip("Master hace render/record/playback. Follower compone su propio atlas copiando desde el Master.")]
    public InstanceMode mode = InstanceMode.Master;

    [Tooltip("Follower: referencia al Master del cual lee el atlas y el mapeo de tiles.")]
    public CCTVPlaneAtlas masterForFollower;

    // Registro estatico
    static readonly List<VirtualSecurityCam> s_sources = new List<VirtualSecurityCam>();
    public static void RegisterSource(VirtualSecurityCam v) { if (v != null && !s_sources.Contains(v)) s_sources.Add(v); }
    public static void UnregisterSource(VirtualSecurityCam v) { if (v != null) s_sources.Remove(v); }

    // Sources
    [Header("Sources (grid order)")]
    [Tooltip("Master: si vacio => auto-llenar desde registro. Follower: usa EXACTAMENTE estas cams (no auto-llenar).")]
    public VirtualSecurityCam[] cameras;

    // Target
    [Header("Target (Plane)")]
    public Renderer targetRenderer;
    [Tooltip("Nombre de la propiedad textura. Vacio = auto resolver.")]
    public string texturePropertyName = "";

    // Layout manual
    [Header("Atlas Grid - Manual")]
    public int columns = 3;
    public int tileWidth = 512;
    public int tileHeight = 288;
    public int paddingX = 8;
    public int paddingY = 8;
    public Color clearColor = Color.black;

    // Render master
    [Header("Render - Master")]
    public bool allowHDR = false;
    public bool allowMSAA = false;
    public bool debugMagenta = false;

    [Header("URP - Master")]
    public int urpRendererIndex = -1;

    [Header("Overlay Top Layer - Master")]
    public LayerMask overlayTopLayer = 0;
    public bool overlayClearDepth = true;

    // Grabacion / playback
    [Header("Recording - Master")]
    public int recordFPS = 10;
    public float recordMaxSeconds = 6f;
    public RenderTextureFormat recordFormat = RenderTextureFormat.ARGB32;

    [Tooltip("Si true, SOLO se graban frames cuando el Player esta visible por esa cam.")]
    public bool onlyRecordWhenTargetVisible = true;

    [Header("Playback - Master")]
    public Transform playbackTriggerTarget;
    public float playbackTriggerDistance = 6f;
    [Tooltip("Si false, el playback del Master avanza siempre (recomendado para followers).")]
    public bool playbackRequiresView = false;
    public float playbackBlackoutSeconds = 1.0f;

    // Performance
    [Header("Performance - Master")]
    public float atlasUpdateFPS = 20f;
    public int maxCamRendersPerFrame = 4;
    public int maxBlitsPerFrame = 6;
    public bool lazyClearAtlas = true;

    [Header("Performance - Follower")]
    public int followerMaxCopiesPerFrame = 12;

    // Arte seguro
    public enum ForceProperty { Auto, BaseMap, MainTex, Custom }

    [Header("Art - Safe Controls (no custom shaders)")]
    public bool forceURPUnlit = false;
    public bool forceUnlitSetup = true;
    public ForceProperty forceTextureProperty = ForceProperty.Auto;
    public string customTextureProperty = "_BaseMap";
    public Color planeTint = Color.white;
    [Min(0f)] public float planeEmissionBoost = 0f;

    [Header("Art - Atlas Output (optional)")]
    public Material blitMaterial = null;
    public float atlasBrightness = 1f;
    public Color atlasColorMultiply = Color.white;

    [Header("Art - Per Tile")]
    public Color tileBlackoutColor = Color.black;

    [Header("Art - RenderTextures")]
    public RenderTextureFormat atlasFormat = RenderTextureFormat.ARGB32;
    public FilterMode atlasFilterMode = FilterMode.Bilinear;
    [Range(0, 16)] public int atlasAniso = 0;

    [Header("Debug")]
    public bool artForceTestPattern = false;

    // Internos
    static Camera s_sharedCam;
    Camera _renderCam;

    RenderTexture _atlas;
    int _rows;
    string _resolvedProp = null;
    MaterialPropertyBlock _mpb;
    float _atlasUpdateTimer = 0f;
    bool _atlasEverCleared = false;

    List<VirtualSecurityCam> _activeMasterCams = new List<VirtualSecurityCam>();

    class CamRuntime
    {
        public RenderTexture tileRT;
        public Quaternion lastRot;
        public bool lastRotValid;
        public float noTargetTimer;

        // Ring buffer
        public RenderTexture[] frames;
        public bool[] frameHasTarget;
        public int frameWrite;
        public int frameCount;
        public int maxFrames;
        public float frameInterval;
        public float frameTimer;
        public bool isRecording;

        // Playback
        public bool playing;
        public bool inBlackout;
        public float blackoutTimer;

        public int playStartIndex;     // NUEVO: indice de inicio del ciclo
        public int playIndex;          // indice actual (ring)
        public int playShownCount;     // NUEVO: cuantas muestras ya se mostraron del ciclo
        public float playTimer;
        public float playFrameInterval;

        public bool tileDirty;
        public int lastShownIndex;
        public bool lastSampleTargetVisible;
    }

    Dictionary<VirtualSecurityCam, CamRuntime> _rt = new Dictionary<VirtualSecurityCam, CamRuntime>();

    // Safe helpers
    public static void SafeDestroyRT(ref RenderTexture rt)
    {
        if (rt == null) return;
        try { if (rt.IsCreated()) rt.Release(); } catch { }
        Object.Destroy(rt);
        rt = null;
    }
    public static void SafeDestroyRT(RenderTexture[] rts)
    {
        if (rts == null) return;
        for (int i = 0; i < rts.Length; i++)
            SafeDestroyRT(ref rts[i]);
    }
    public static void SafeDestroyRT(List<RenderTexture> rts)
    {
        if (rts == null) return;
        for (int i = 0; i < rts.Count; i++)
        {
            var tmp = rts[i];
            SafeDestroyRT(ref tmp);
            rts[i] = null;
        }
        rts.Clear();
    }

    // Public expose
    public RenderTexture GetCurrentAtlas() { return _atlas; }
    public IReadOnlyList<VirtualSecurityCam> GetActiveMasterCameras() { return _activeMasterCams; }

    public bool GetMasterTilePixelRect(VirtualSecurityCam v, out RectInt rect)
    {
        rect = default;
        if (mode != InstanceMode.Master)
            return (masterForFollower != null) && masterForFollower.GetMasterTilePixelRect(v, out rect);

        int count = (_activeMasterCams != null) ? _activeMasterCams.Count : 0;
        int idx = (count > 0) ? _activeMasterCams.IndexOf(v) : -1;
        if (idx < 0 || _atlas == null) return false;

        int col = idx % Mathf.Max(1, columns);
        int row = idx / Mathf.Max(1, columns);
        int tilesInRow = (row == _rows - 1) ? Mathf.Max(1, count - row * Mathf.Max(1, columns)) : Mathf.Max(1, columns);
        tilesInRow = Mathf.Clamp(tilesInRow, 1, Mathf.Max(1, columns));
        int rowPixelWidth = tilesInRow * tileWidth + (tilesInRow + 1) * paddingX;
        int x0 = Mathf.Max(0, (_atlas.width - rowPixelWidth) / 2);
        int px = x0 + paddingX + col * (tileWidth + paddingX);
        int py = paddingY + row * (tileHeight + paddingY);
        rect = new RectInt(px, py, tileWidth, tileHeight);
        return true;
    }

    void Awake()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        if (mode == InstanceMode.Master)
        {
            SetupSharedRenderCamera();
            if (cameras == null || cameras.Length == 0) cameras = s_sources.ToArray();
            RecreateAtlas();
            SafeSetupPlaneMaterial();
            BindAtlasToPlane(_atlas);
            InitPerCamState_Master();
        }
        else
        {
            RecreateAtlas(minimal: true, countOverride: (cameras != null ? cameras.Length : 0));
            SafeSetupPlaneMaterial();
            BindAtlasToPlane(_atlas);
        }
        ApplyPlaneStyling();
    }

    void OnDestroy()
    {
        SafeDestroyRT(ref _atlas);
        foreach (var kv in _rt)
        {
            var cr = kv.Value;
            SafeDestroyRT(ref cr.tileRT);
            SafeDestroyRT(cr.frames);
        }
        _rt.Clear();
        _activeMasterCams.Clear();
    }

    // Shared render camera (master)
    void SetupSharedRenderCamera()
    {
        if (s_sharedCam != null)
        {
            _renderCam = s_sharedCam;
            ConfigureRenderCamera(_renderCam);
            return;
        }
        var go = new GameObject("CCTV_RenderCam_SHARED", typeof(Camera));
        s_sharedCam = go.GetComponent<Camera>();
        _renderCam = s_sharedCam;
        DontDestroyOnLoad(go);
        ConfigureRenderCamera(_renderCam);
    }

    void ConfigureRenderCamera(Camera cam)
    {
        if (cam == null) return;
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

    // Plane material
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

    void BindAtlasToPlane(Texture src)
    {
        if (!targetRenderer || src == null) return;
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
            if (mat.GetTexture(_resolvedProp) != src)
                mat.SetTexture(_resolvedProp, src);
        }
        else
        {
            _resolvedProp = ResolveTextureProperty(mat, texturePropertyName);
            if (!string.IsNullOrEmpty(_resolvedProp) && mat.GetTexture(_resolvedProp) != src)
                mat.SetTexture(_resolvedProp, src);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
    }

    void ApplyPlaneStyling()
    {
        if (!targetRenderer) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(_mpb);

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

    // Atlas
    void RecreateAtlas(bool minimal = false, int countOverride = -1)
    {
        int count = (countOverride >= 0) ? countOverride : (cameras != null ? cameras.Length : 0);
        columns = Mathf.Max(1, columns);
        _rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));

        int w, h;
        if (minimal || count <= 0)
        {
            w = Mathf.Max(16, paddingX * 2 + 16);
            h = Mathf.Max(16, paddingY * 2 + 16);
        }
        else
        {
            w = columns * tileWidth + (columns + 1) * paddingX;
            h = _rows * tileHeight + (_rows + 1) * paddingY;
        }

        if (_atlas != null && _atlas.width == w && _atlas.height == h && _atlas.format == atlasFormat) return;

        SafeDestroyRT(ref _atlas);

        _atlas = new RenderTexture(w, h, 0, atlasFormat, RenderTextureReadWrite.Default);
        _atlas.name = (mode == InstanceMode.Master ? "CCTV_ATLAS_MASTER_" : "CCTV_ATLAS_FOLLOWER_") + w + "x" + h;
        _atlas.filterMode = atlasFilterMode;
        _atlas.anisoLevel = atlasAniso;
        _atlas.wrapMode = TextureWrapMode.Clamp;
        _atlas.useMipMap = false;
        _atlas.autoGenerateMips = false;
        _atlas.antiAliasing = 1;
        _atlas.Create();
        _atlasEverCleared = false;
    }

    Rect TileViewport01_Centered(int idx, int count)
    {
        int cols = Mathf.Max(1, columns);
        int col = idx % cols;
        int row = idx / cols;

        int tilesInRow = (row == _rows - 1) ? Mathf.Max(1, count - row * cols) : cols;
        tilesInRow = Mathf.Clamp(tilesInRow, 1, cols);

        int rowPixelWidth = tilesInRow * tileWidth + (tilesInRow + 1) * paddingX;
        int x0 = Mathf.Max(0, (_atlas.width - rowPixelWidth) / 2);

        int px = x0 + paddingX + col * (tileWidth + paddingX);
        int py = paddingY + row * (tileHeight + paddingY);

        float nx = (float)px / (float)_atlas.width;
        float ny = (float)py / (float)_atlas.height;
        float nw = (float)tileWidth / (float)_atlas.width;
        float nh = (float)tileHeight / (float)_atlas.height;
        return new Rect(nx, ny, nw, nh);
    }

    void ClearAtlasAll()
    {
        if (_atlas == null) return;
        var prev = RenderTexture.active;
        RenderTexture.active = _atlas;
        GL.Viewport(new Rect(0, 0, _atlas.width, _atlas.height));
        GL.Clear(true, true, clearColor);
        RenderTexture.active = prev;
        _atlasEverCleared = true;
    }

    void ClearTileColor(int tileIndex, Color col, int count)
    {
        if (_atlas == null) return;
        Rect vp = TileViewport01_Centered(tileIndex, count);
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

    void BlitTileToAtlas(RenderTexture src, int tileIndex, int count)
    {
        if (_atlas == null || src == null || !src.IsCreated()) return;
        Rect vp = TileViewport01_Centered(tileIndex, count);
        int px = Mathf.RoundToInt(vp.x * _atlas.width);
        int py = Mathf.RoundToInt(vp.y * _atlas.height);
        int pw = Mathf.RoundToInt(vp.width * _atlas.width);
        int ph = Mathf.RoundToInt(vp.height * _atlas.height);

        bool usePost = blitMaterial != null || atlasColorMultiply != Color.white || Mathf.Abs(atlasBrightness - 1f) > 0.0001f;

        var prev = RenderTexture.active;
        RenderTexture.active = _atlas;
        GL.Viewport(new Rect(px, py, pw, ph));

        if (usePost && blitMaterial != null)
        {
            if (blitMaterial.HasProperty("_Color")) blitMaterial.SetColor("_Color", atlasColorMultiply);
            if (blitMaterial.HasProperty("_Brightness")) blitMaterial.SetFloat("_Brightness", atlasBrightness);
            Graphics.Blit(src, (RenderTexture)null, blitMaterial);
        }
        else if (src.width == pw && src.height == ph)
        {
            Graphics.CopyTexture(src, 0, 0, 0, 0, src.width, src.height, _atlas, 0, 0, px, py);
        }
        else
        {
            Graphics.Blit(src, (RenderTexture)null);
        }

        RenderTexture.active = prev;
    }

    // Utils
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

    void EnsureCamerasArray_Master()
    {
        if (cameras == null || cameras.Length == 0) cameras = s_sources.ToArray();
    }

    void LateUpdate()
    {
        if (mode == InstanceMode.Follower)
        {
            LateUpdate_Follower();
            return;
        }

        // MASTER
        EnsureCamerasArray_Master();
        if (_renderCam == null || cameras == null) return;

        bool layoutChanged =
            _atlas == null ||
            _rows != Mathf.Max(1, Mathf.CeilToInt((cameras.Length) / (float)Mathf.Max(1, columns))) ||
            _atlas.width != (columns * tileWidth + (columns + 1) * paddingX) ||
            _atlas.height != (_rows * tileHeight + (_rows + 1) * paddingY);

        if (layoutChanged)
        {
            RecreateAtlas(minimal: cameras.Length == 0, countOverride: cameras.Length);
            InitPerCamState_Master();
        }

        float updateInterval = 1f / Mathf.Max(1f, atlasUpdateFPS);
        _atlasUpdateTimer += Time.deltaTime;
        bool doAtlasWork = _atlasUpdateTimer >= updateInterval;
        if (doAtlasWork) _atlasUpdateTimer -= updateInterval;

        if (!lazyClearAtlas || !_atlasEverCleared)
            ClearAtlasAll();

        float dt = Time.deltaTime;

        bool viewingPlane = !playbackRequiresView;
        if (playbackRequiresView && playbackTriggerTarget != null && targetRenderer != null)
        {
            Vector3 planePos = targetRenderer.bounds.center;
            float d = Vector3.Distance(planePos, playbackTriggerTarget.position);
            viewingPlane = (d <= playbackTriggerDistance);
        }

        int rendersLeft = Mathf.Max(0, maxCamRendersPerFrame);
        int blitsLeft = Mathf.Max(0, maxBlitsPerFrame);
        int masterCount = cameras.Length;

        if (masterCount != _rt.Count) InitPerCamState_Master();

        // RENDER + GRAB
        for (int i = 0; i < masterCount && rendersLeft > 0; i++)
        {
            var v = cameras[i];
            if (v == null) continue;
            if (!_rt.TryGetValue(v, out var cr)) continue;

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
            cr.lastSampleTargetVisible = targetVisible;
            if (!targetVisible) cr.noTargetTimer += dt; else cr.noTargetTimer = 0f;

            bool startByMove = angSpeed >= v.startOnAngularSpeedDegPerSec;
            bool startBySee = targetVisible;

            if (!cr.isRecording && (startByMove || startBySee)) cr.isRecording = true;
            if (cr.isRecording && !targetVisible && cr.noTargetTimer >= Mathf.Max(0.05f, v.stopAfterNoTargetSeconds)) cr.isRecording = false;

            if (!cr.isRecording && !(viewingPlane && doAtlasWork)) continue;

            int maskBase = v.cullingMask.value;
            if (overlayTopLayer != 0) maskBase = maskBase & ~overlayTopLayer.value;
            if (maskBase == 0) maskBase = ~0;

            _renderCam.transform.SetPositionAndRotation(camPos, rot);
            _renderCam.fieldOfView = v.fieldOfView;
            _renderCam.nearClipPlane = Mathf.Max(0.001f, v.nearClip);
            _renderCam.farClipPlane = v.farClip;

            _renderCam.cullingMask = maskBase;
            _renderCam.targetTexture = cr.tileRT;
            _renderCam.clearFlags = CameraClearFlags.SolidColor;
            _renderCam.backgroundColor = clearColor;
            _renderCam.Render();

            if (overlayTopLayer != 0)
            {
                int overlayMask = v.cullingMask.value & overlayTopLayer.value;
                if (overlayMask != 0)
                {
                    _renderCam.cullingMask = overlayMask;
                    _renderCam.clearFlags = overlayClearDepth ? CameraClearFlags.Depth : CameraClearFlags.Nothing;
                    _renderCam.Render();
                }
            }
            _renderCam.targetTexture = null;
            rendersLeft--;

            if (cr.isRecording)
            {
                cr.frameTimer += dt;
                if (cr.frameTimer >= cr.frameInterval)
                {
                    cr.frameTimer -= cr.frameInterval;

                    if (!onlyRecordWhenTargetVisible || cr.lastSampleTargetVisible)
                    {
                        var dst = cr.frames[cr.frameWrite];
                        if (dst != null) Graphics.Blit(cr.tileRT, dst);

                        if (cr.frameHasTarget != null && cr.frameWrite < cr.frameHasTarget.Length)
                            cr.frameHasTarget[cr.frameWrite] = cr.lastSampleTargetVisible;

                        cr.frameWrite = (cr.frameWrite + 1) % cr.maxFrames;
                        cr.frameCount = Mathf.Min(cr.frameCount + 1, cr.maxFrames);
                        cr.tileDirty = true;
                    }
                }
            }
        }

        // PLAYBACK -> ATLAS (duracion correcta con wrap)
        if (viewingPlane && doAtlasWork)
        {
            for (int i = 0; i < masterCount && blitsLeft > 0; i++)
            {
                var v = cameras[i];
                if (v == null) continue;
                if (!_rt.TryGetValue(v, out var cr)) continue;

                bool hasAny = cr.frameCount > 0;
                if (!hasAny)
                {
                    ClearTileColor(i, tileBlackoutColor, masterCount);
                    continue;
                }

                // iniciar ciclo si hace falta
                if (!cr.playing && !cr.inBlackout)
                {
                    cr.playing = true;
                    cr.playStartIndex = Mod(cr.frameWrite - cr.frameCount, cr.maxFrames);
                    cr.playIndex = cr.playStartIndex;
                    cr.playShownCount = 0;
                    cr.playTimer = 0f;
                    cr.lastShownIndex = -1;
                    cr.tileDirty = true;
                }

                if (cr.inBlackout)
                {
                    cr.blackoutTimer += (1f / Mathf.Max(1f, atlasUpdateFPS));
                    ClearTileColor(i, tileBlackoutColor, masterCount);
                    if (cr.blackoutTimer >= Mathf.Max(0.01f, playbackBlackoutSeconds))
                    {
                        cr.blackoutTimer = 0f;
                        cr.inBlackout = false;
                        cr.playing = true;
                        cr.playStartIndex = Mod(cr.frameWrite - cr.frameCount, cr.maxFrames);
                        cr.playIndex = cr.playStartIndex;
                        cr.playShownCount = 0;
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

                        int tries = 0;
                        int next = Mod(cr.playIndex + 1, cr.maxFrames);
                        // saltar frames sin target si hace falta
                        while (tries < cr.frameCount)
                        {
                            bool ok = (cr.frameHasTarget == null) || (next < cr.frameHasTarget.Length ? cr.frameHasTarget[next] : true);
                            if (ok) break;
                            next = Mod(next + 1, cr.maxFrames);
                            tries++;
                        }

                        cr.playIndex = next;
                        cr.playShownCount = Mathf.Min(cr.playShownCount + 1, cr.frameCount);
                        cr.tileDirty = true;

                        // si mostramos frameCount unidades, cerramos el ciclo
                        if (cr.playShownCount >= cr.frameCount)
                        {
                            cr.playing = false;
                            cr.inBlackout = true;
                            cr.blackoutTimer = 0f;
                        }
                    }

                    if (cr.tileDirty && cr.playIndex != cr.lastShownIndex)
                    {
                        BlitTileToAtlas(cr.frames[cr.playIndex], i, masterCount);
                        cr.lastShownIndex = cr.playIndex;
                        cr.tileDirty = false;
                        blitsLeft--;
                    }
                }
            }
        }

        BindAtlasToPlane(_atlas);
        ApplyPlaneStyling();

        if (!_atlasEverCleared && lazyClearAtlas)
            ClearAtlasAll();

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

    // Follower
    void LateUpdate_Follower()
    {
        int followerCount = (cameras != null) ? cameras.Length : 0;

        if (masterForFollower == null || masterForFollower.GetCurrentAtlas() == null)
        {
            RecreateAtlas(minimal: true, countOverride: followerCount);
            BindAtlasToPlane(_atlas);
            ApplyPlaneStyling();
            return;
        }

        RecreateAtlas(minimal: followerCount == 0, countOverride: followerCount);

        if (!_atlasEverCleared)
            ClearAtlasAll();

        var masterAtlas = masterForFollower.GetCurrentAtlas();
        int copiesLeft = Mathf.Max(0, followerMaxCopiesPerFrame);

        if (followerCount == 0)
        {
            ClearAtlasAll();
        }
        else
        {
            for (int i = 0; i < followerCount && copiesLeft > 0; i++)
            {
                var v = cameras[i];
                if (v == null)
                {
                    ClearTileColor(i, tileBlackoutColor, followerCount);
                    continue;
                }

                if (!masterForFollower.GetMasterTilePixelRect(v, out var srcRect))
                {
                    ClearTileColor(i, tileBlackoutColor, followerCount);
                    continue;
                }

                Rect vpDst = TileViewport01_Centered(i, followerCount);
                int dx = Mathf.RoundToInt(vpDst.x * _atlas.width);
                int dy = Mathf.RoundToInt(vpDst.y * _atlas.height);
                int dw = Mathf.RoundToInt(vpDst.width * _atlas.width);
                int dh = Mathf.RoundToInt(vpDst.height * _atlas.height);

                bool copied = false;
                try
                {
                    Graphics.CopyTexture(masterAtlas, 0, 0, srcRect.x, srcRect.y, srcRect.width, srcRect.height, _atlas, 0, 0, dx, dy);
                    copied = true;
                }
                catch { copied = false; }

                if (!copied)
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = _atlas;
                    GL.Viewport(new Rect(dx, dy, dw, dh));
                    if (blitMaterial != null)
                    {
                        if (blitMaterial.HasProperty("_MainTex_ST"))
                        {
                            Vector2 scale = new Vector2((float)srcRect.width / masterAtlas.width, (float)srcRect.height / masterAtlas.height);
                            Vector2 offset = new Vector2((float)srcRect.x / masterAtlas.width, (float)srcRect.y / masterAtlas.height);
                            blitMaterial.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
                        }
                        Graphics.Blit(masterAtlas, (RenderTexture)null, blitMaterial);
                        if (blitMaterial.HasProperty("_MainTex_ST"))
                            blitMaterial.SetVector("_MainTex_ST", new Vector4(1, 1, 0, 0));
                    }
                    else
                    {
                        Graphics.Blit(masterAtlas, (RenderTexture)null);
                    }
                    RenderTexture.active = prev;
                }

                copiesLeft--;
            }
        }

        BindAtlasToPlane(_atlas);
        ApplyPlaneStyling();
    }

    // Init per-cam (master)
    void InitPerCamState_Master()
    {
        _rt.Clear();
        _activeMasterCams.Clear();

        int n = (cameras != null) ? cameras.Length : 0;
        if (n <= 0) return;

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
            cr.frameHasTarget = new bool[pooled];

            for (int k = 0; k < pooled; k++)
                cr.frames[k] = CreateRT(tileWidth, tileHeight, 0, recordFormat);

            cr.frameWrite = 0;
            cr.frameCount = 0;
            cr.isRecording = v.IsRecording;

            cr.playing = false;
            cr.inBlackout = false;
            cr.playStartIndex = 0;
            cr.playIndex = 0;
            cr.playShownCount = 0;

            cr.lastShownIndex = -1;
            cr.tileDirty = true;

            _rt[v] = cr;
            _activeMasterCams.Add(v);
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

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
}
