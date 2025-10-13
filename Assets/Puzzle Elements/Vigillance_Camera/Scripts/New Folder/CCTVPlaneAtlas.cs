// CCTVPlaneAtlas.cs — SOLO playback en loop (con blackout) cuando el player llega al Plane.
// Graba cuando la cámara rota lo suficiente o cuando el target es visible de verdad.

using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CCTVPlaneAtlas : MonoBehaviour
{
    static readonly List<VirtualSecurityCam> s_sources = new List<VirtualSecurityCam>();
    public static void RegisterSource(VirtualSecurityCam v) { if (!s_sources.Contains(v)) s_sources.Add(v); }
    public static void UnregisterSource(VirtualSecurityCam v) { s_sources.Remove(v); }

    [Header("Sources (grid order)")]
    public VirtualSecurityCam[] cameras;

    [Header("Target (Plane)")]
    public Renderer targetRenderer;
    public string texturePropertyName = "";

    [Header("Atlas Grid")]
    public int columns = 3;
    public int tileWidth = 512;
    public int tileHeight = 288;
    public int paddingX = 8;
    public int paddingY = 8;
    public Color clearColor = Color.black;

    [Header("Render")]
    public bool allowHDR = false;
    public bool allowMSAA = false;
    public bool debugMagenta = false;

    [Header("URP")]
    public int urpRendererIndex = -1;

    [Header("Overlay Top Layer")]
    public LayerMask overlayTopLayer = 0;
    public bool overlayClearDepth = true;

    [Header("Recording")]
    public int recordFPS = 10;
    public float recordMaxSeconds = 6f;
    public RenderTextureFormat recordFormat = RenderTextureFormat.ARGB32;

    [Header("Playback")]
    public Transform playbackTriggerTarget;
    public float playbackTriggerDistance = 6f;
    public float playbackBlackoutSeconds = 1.0f;

    Camera _renderCam;
    RenderTexture _atlas;
    int _rows;
    string _resolvedProp = null;

    class CamRuntime
    {
        public RenderTexture tileRT;
        public Quaternion lastRot;
        public bool lastRotValid;
        public float noTargetTimer;

        public List<RenderTexture> frames;
        public float frameTimer;
        public float frameInterval;
        public int maxFrames;
        public bool isRecording;

        public bool playing;
        public bool inBlackout;
        public float blackoutTimer;
        public int playIndex;
        public float playTimer;
        public float playFrameInterval;
    }

    Dictionary<VirtualSecurityCam, CamRuntime> _rt = new Dictionary<VirtualSecurityCam, CamRuntime>();

    void Awake()
    {
        SetupRenderCamera();
        RecreateAtlas();
        BindAtlasToPlane();

        if (cameras == null || cameras.Length == 0) cameras = s_sources.ToArray();
        InitPerCamState();
    }

    void OnDestroy()
    {
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
                for (int i = 0; i < cr.frames.Count; i++)
                {
                    var f = cr.frames[i];
                    if (f != null) { try { if (f.IsCreated()) f.Release(); } catch { } Destroy(f); }
                }
            }
        }
        _rt.Clear();
    }

    void InitPerCamState()
    {
        _rt.Clear();
        int n = cameras != null ? cameras.Length : 0;
        for (int i = 0; i < n; i++)
        {
            var v = cameras[i];
            if (v == null) continue;

            CamRuntime cr = new CamRuntime();
            cr.tileRT = CreateRT(tileWidth, tileHeight, 24, recordFormat);
            cr.frames = new List<RenderTexture>();
            cr.frameInterval = 1f / Mathf.Max(1, recordFPS);
            cr.maxFrames = Mathf.Max(1, Mathf.FloorToInt(recordMaxSeconds * Mathf.Max(1, recordFPS)));
            cr.playFrameInterval = cr.frameInterval;
            cr.isRecording = v.IsRecording;
            cr.playing = false;
            _rt[v] = cr;
        }
    }

    RenderTexture CreateRT(int w, int h, int depth, RenderTextureFormat fmt)
    {
        var rt = new RenderTexture(w, h, depth, fmt, RenderTextureReadWrite.Default);
        rt.name = "CCTV_TileRT_" + w + "x" + h + "_" + fmt;
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.antiAliasing = 1;
        rt.Create();
        return rt;
    }

    void SetupRenderCamera()
    {
        var go = new GameObject("CCTV_RenderCam", typeof(Camera));
        go.transform.SetParent(transform, false);
        _renderCam = go.GetComponent<Camera>();
        _renderCam.enabled = false;
        _renderCam.backgroundColor = clearColor;
        _renderCam.clearFlags = CameraClearFlags.Nothing;
        _renderCam.allowHDR = allowHDR;
        _renderCam.allowMSAA = allowMSAA;
        _renderCam.stereoTargetEye = StereoTargetEyeMask.None;
        _renderCam.depth = -100;

        var urp = go.GetComponent<UniversalAdditionalCameraData>();
        if (!urp) urp = go.AddComponent<UniversalAdditionalCameraData>();
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
            if (_atlas.width == w && _atlas.height == h) return;
            try { if (_atlas.IsCreated()) _atlas.Release(); } catch { }
            Destroy(_atlas);
        }

        _atlas = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        _atlas.name = "CCTV_ATLAS_" + w + "x" + h;
        _atlas.filterMode = FilterMode.Bilinear;
        _atlas.wrapMode = TextureWrapMode.Clamp;
        _atlas.useMipMap = false;
        _atlas.autoGenerateMips = false;
        _atlas.antiAliasing = 1;
        _atlas.Create();
    }

    void BindAtlasToPlane()
    {
        if (!targetRenderer || _atlas == null) return;
        var mat = targetRenderer.material;
        if (mat == null) return;

        _resolvedProp = ResolveTextureProperty(mat, texturePropertyName);
        if (!string.IsNullOrEmpty(_resolvedProp)) mat.SetTexture(_resolvedProp, _atlas);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
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

    void ClearAtlasAll()
    {
        var prev = RenderTexture.active;
        RenderTexture.active = _atlas;
        GL.Viewport(new Rect(0, 0, _atlas.width, _atlas.height));
        GL.Clear(true, true, clearColor);
        RenderTexture.active = prev;
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
        if (cameras == null || cameras.Length == 0) cameras = s_sources.ToArray();
    }

    void LateUpdate()
    {
        EnsureCamerasArray();
        if (_renderCam == null || cameras == null) return;

        RecreateAtlas();
        if (cameras.Length != _rt.Count) InitPerCamState();

        ClearAtlasAll();

        float dt = Time.deltaTime;
        bool viewingPlane = false;
        if (playbackTriggerTarget != null && targetRenderer != null)
        {
            Vector3 planePos = targetRenderer.bounds.center;
            float d = Vector3.Distance(planePos, playbackTriggerTarget.position);
            viewingPlane = (d <= playbackTriggerDistance);
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            var v = cameras[i];
            if (v == null) continue;

            CamRuntime cr;
            if (!_rt.TryGetValue(v, out cr))
            {
                cr = new CamRuntime();
                cr.tileRT = CreateRT(tileWidth, tileHeight, 24, recordFormat);
                cr.frames = new List<RenderTexture>();
                cr.frameInterval = 1f / Mathf.Max(1, recordFPS);
                cr.maxFrames = Mathf.Max(1, Mathf.FloorToInt(recordMaxSeconds * Mathf.Max(1, recordFPS)));
                cr.playFrameInterval = cr.frameInterval;
                _rt[v] = cr;
            }

            Transform p = v.Pivot;
            Vector3 desiredFwd = v.GetDesiredForward();
            if (desiredFwd.sqrMagnitude < 1e-6f) desiredFwd = p.forward;

            Quaternion rot = v.lockHorizon
                ? Quaternion.LookRotation(desiredFwd, Vector3.up)
                : (p.rotation * Quaternion.Euler(v.rotationOffsetEuler));

            Vector3 camPos = p.position;
            Vector3 camFwd = (rot * Vector3.forward);

            float angSpeed = 0f;
            if (cr.lastRotValid) angSpeed = AngularSpeedDegPerSec(cr.lastRot, rot, Mathf.Max(0.0001f, dt));
            cr.lastRot = rot;
            cr.lastRotValid = true;

            bool targetVisible = IsTargetVisible(v, camPos, camFwd);
            if (!targetVisible) cr.noTargetTimer += dt; else cr.noTargetTimer = 0f;

            bool startByMove = angSpeed >= v.startOnAngularSpeedDegPerSec;
            bool startBySee = targetVisible;

            if (!cr.isRecording && (startByMove || startBySee)) cr.isRecording = true;
            if (cr.isRecording && !targetVisible && cr.noTargetTimer >= Mathf.Max(0.05f, v.stopAfterNoTargetSeconds)) cr.isRecording = false;

            // Render live a tileRT para alimentar la grabación (aunque no se muestre)
            int maskBase = v.cullingMask.value;
            if (overlayTopLayer != 0) maskBase = maskBase & ~overlayTopLayer.value;
            if (maskBase == 0) maskBase = ~0;

            _renderCam.transform.SetPositionAndRotation(camPos, rot);
            _renderCam.fieldOfView = v.fieldOfView;
            _renderCam.nearClipPlane = Mathf.Max(0.001f, v.nearClip);
            _renderCam.farClipPlane = v.farClip;

            _renderCam.cullingMask = maskBase;
            _renderCam.targetTexture = cr.tileRT;

            var oldFlags = _renderCam.clearFlags;
            _renderCam.clearFlags = CameraClearFlags.SolidColor;
            _renderCam.backgroundColor = clearColor;
            _renderCam.Render();

            _renderCam.clearFlags = CameraClearFlags.Nothing;
            _renderCam.Render();

            if (overlayTopLayer != 0)
            {
                int overlayMask = v.cullingMask.value & overlayTopLayer.value;
                if (overlayMask != 0)
                {
                    _renderCam.cullingMask = overlayMask;
                    _renderCam.clearFlags = overlayClearDepth ? CameraClearFlags.Depth : CameraClearFlags.Nothing;
                    _renderCam.Render();
                    _renderCam.clearFlags = CameraClearFlags.Nothing;
                    _renderCam.Render();
                }
            }

            _renderCam.clearFlags = oldFlags;
            _renderCam.targetTexture = null;

            // Grabación a FPS fijo
            if (cr.isRecording)
            {
                cr.frameTimer += dt;
                if (cr.frameTimer >= cr.frameInterval)
                {
                    cr.frameTimer -= cr.frameInterval;
                    RenderTexture frame = CreateRT(tileWidth, tileHeight, 0, recordFormat);
                    Graphics.Blit(cr.tileRT, frame);
                    cr.frames.Add(frame);
                    if (cr.frames.Count > cr.maxFrames)
                    {
                        var old = cr.frames[0];
                        cr.frames.RemoveAt(0);
                        if (old != null) { try { if (old.IsCreated()) old.Release(); } catch { } Destroy(old); }
                    }
                }
            }

            // Salida: SOLO cuando el player está viendo el Plane y SOLO playback
            if (!viewingPlane)
            {
                ClearTileColor(i, Color.black);
                continue;
            }

            bool hasClip = (cr.frames != null && cr.frames.Count > 0);
            if (!hasClip)
            {
                ClearTileColor(i, Color.black);
                continue;
            }

            if (!cr.playing && !cr.inBlackout)
            {
                cr.playing = true;
                cr.playIndex = 0;
                cr.playTimer = 0f;
            }

            if (cr.inBlackout)
            {
                cr.blackoutTimer += dt;
                ClearTileColor(i, Color.black);
                if (cr.blackoutTimer >= Mathf.Max(0.01f, playbackBlackoutSeconds))
                {
                    cr.blackoutTimer = 0f;
                    cr.inBlackout = false;
                    cr.playing = true;
                    cr.playIndex = 0;
                    cr.playTimer = 0f;
                }
            }
            else
            {
                cr.playTimer += dt;
                if (cr.playTimer >= cr.playFrameInterval)
                {
                    cr.playTimer -= cr.playFrameInterval;
                    cr.playIndex++;
                    if (cr.playIndex >= cr.frames.Count)
                    {
                        cr.playing = false;
                        cr.inBlackout = true;
                        cr.blackoutTimer = 0f;
                        cr.playIndex = 0; // preparar próximo loop desde el inicio
                    }
                }

                int idx = Mathf.Clamp(cr.playIndex, 0, cr.frames.Count - 1);
                BlitTileToAtlas(cr.frames[idx], i);
            }
        }

        if (targetRenderer != null)
        {
            var mat = targetRenderer.material;
            if (mat != null)
            {
                if (string.IsNullOrEmpty(_resolvedProp) || !mat.HasProperty(_resolvedProp))
                    _resolvedProp = ResolveTextureProperty(mat, texturePropertyName);
                if (!string.IsNullOrEmpty(_resolvedProp) && mat.GetTexture(_resolvedProp) != _atlas)
                    mat.SetTexture(_resolvedProp, _atlas);
            }
        }
    }
}
