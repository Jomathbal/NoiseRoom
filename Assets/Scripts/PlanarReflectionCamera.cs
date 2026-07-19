using UnityEngine;
using UnityEngine.Rendering.Universal;

// Rendert die Szene an der Ebene dieses Objekts (Position + transform.up)
// gespiegelt in eine RenderTexture und stellt sie global als
// _PlanarReflectionTex bereit — gesampelt vom Shader
// Custom/RaytraceNoiseSurfaceReflective. Gehört auf das Floor-Objekt.
//
// Funktionsweise:
// - Eine versteckte zweite Kamera bekommt worldToCameraMatrix der Hauptkamera
//   multipliziert mit einer Spiegelmatrix an der Bodenebene.
// - Eine oblique Near-Plane clippt alles unterhalb des Bodens weg
//   (sonst würde der Boden selbst die Spiegelkamera verdecken).
// - Die Spiegelung kehrt die Dreiecks-Windung um; statt GL.invertCulling wird
//   die Projektion zusätzlich in X geflippt (Culling stimmt dann wieder) und
//   der Shader sampelt die Textur mit 1-u.
[ExecuteAlways]
public class PlanarReflectionCamera : MonoBehaviour
{
    [Tooltip("Auflösung der Spiegel-Textur relativ zur Bildschirmauflösung. 0.5 = halbe Auflösung (schneller, leicht unscharf).")]
    [Range(0.25f, 1f)]
    public float resolutionScale = 1f;

    [Tooltip("Welche Layer in der Spiegelung sichtbar sind.")]
    public LayerMask reflectLayers = -1;

    [Tooltip("Schatten auch in der Spiegelung rendern? Aus = schneller, in der Spiegelung fällt das kaum auf.")]
    public bool renderShadows = false;

    [Tooltip("Post-Processing (Bloom etc.) auch in der Spiegelung rendern. Achtung: wendet ALLE aktiven Volume-Effekte auf die Spiegel-Textur an, also auch Vignette und Tonemapping — die wirken dann auf dem Boden doppelt.")]
    public bool renderPostProcessing = true;

    [Tooltip("Skybox in der Spiegelung mitrendern? Aus = leere Bereiche der Spiegelung zeigen stattdessen die Background Color.")]
    public bool reflectSkybox = false;

    [Tooltip("Hintergrundfarbe der Spiegelung, wenn Reflect Skybox aus ist. Fließt über die Reflection Strength in die leeren Bodenbereiche ein.")]
    public Color backgroundColor = Color.black;

    [Tooltip("Hebt die Clip-Ebene minimal an, um Flimmern direkt an der Bodenkante zu vermeiden.")]
    public float clipPlaneOffset = 0.005f;

    Camera _reflectionCamera;
    RenderTexture _reflectionRT;

    static readonly int PlanarReflectionTexID = Shader.PropertyToID("_PlanarReflectionTex");

    void OnDisable() => Cleanup();
    void OnDestroy() => Cleanup();

    void Cleanup()
    {
        if (_reflectionCamera != null)
        {
            if (Application.isPlaying) Destroy(_reflectionCamera.gameObject);
            else DestroyImmediate(_reflectionCamera.gameObject);
            _reflectionCamera = null;
        }
        if (_reflectionRT != null)
        {
            if (Application.isPlaying) Destroy(_reflectionRT);
            else DestroyImmediate(_reflectionRT);
            _reflectionRT = null;
        }
    }

    void LateUpdate()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
            return;

        EnsureResources(mainCam);
        UpdateReflectionCamera(mainCam);
        Shader.SetGlobalTexture(PlanarReflectionTexID, _reflectionRT);
    }

    void EnsureResources(Camera mainCam)
    {
        int width  = Mathf.Max(1, Mathf.RoundToInt(mainCam.pixelWidth * resolutionScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(mainCam.pixelHeight * resolutionScale));

        if (_reflectionRT == null || _reflectionRT.width != width || _reflectionRT.height != height)
        {
            if (_reflectionRT != null)
            {
                if (_reflectionCamera != null)
                    _reflectionCamera.targetTexture = null;
                if (Application.isPlaying) Destroy(_reflectionRT);
                else DestroyImmediate(_reflectionRT);
            }
            _reflectionRT = new RenderTexture(width, height, 32, RenderTextureFormat.DefaultHDR)
            {
                name = "PlanarReflectionRT"
            };
        }

        if (_reflectionCamera == null)
        {
            var go = new GameObject("Planar Reflection Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _reflectionCamera = go.AddComponent<Camera>();
            _reflectionCamera.allowMSAA = false;
            _reflectionCamera.useOcclusionCulling = false; // gespiegelte Matrizen vertragen sich nicht mit Occlusion Culling

            var camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.requiresColorOption = CameraOverrideOption.Off;
            camData.requiresDepthOption = CameraOverrideOption.Off;
        }

        var data = _reflectionCamera.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
        {
            data.renderShadows = renderShadows;
            data.renderPostProcessing = renderPostProcessing;
        }

        _reflectionCamera.targetTexture = _reflectionRT;
        _reflectionCamera.cullingMask = reflectLayers;
        _reflectionCamera.depth = mainCam.depth - 10f; // rendert automatisch vor der Hauptkamera
    }

    void UpdateReflectionCamera(Camera mainCam)
    {
        Vector3 planeNormal = transform.up;
        Vector3 planePos = transform.position + planeNormal * clipPlaneOffset;

        if (reflectSkybox)
        {
            _reflectionCamera.clearFlags      = mainCam.clearFlags;
            _reflectionCamera.backgroundColor = mainCam.backgroundColor;
        }
        else
        {
            _reflectionCamera.clearFlags      = CameraClearFlags.SolidColor;
            _reflectionCamera.backgroundColor = backgroundColor;
        }
        _reflectionCamera.orthographic    = mainCam.orthographic;
        _reflectionCamera.fieldOfView     = mainCam.fieldOfView;
        _reflectionCamera.aspect          = mainCam.aspect;
        _reflectionCamera.nearClipPlane   = mainCam.nearClipPlane;
        _reflectionCamera.farClipPlane    = mainCam.farClipPlane;

        float d = -Vector3.Dot(planeNormal, planePos);
        Matrix4x4 reflection = CalculateReflectionMatrix(
            new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, d));

        // Transform mitspiegeln (für konsistentes Culling/Lighting), die
        // eigentliche Spiegelung steckt in der worldToCameraMatrix.
        _reflectionCamera.transform.position = reflection.MultiplyPoint(mainCam.transform.position);
        Vector3 fwd = Vector3.Reflect(mainCam.transform.forward, planeNormal);
        Vector3 up  = Vector3.Reflect(mainCam.transform.up, planeNormal);
        _reflectionCamera.transform.rotation = Quaternion.LookRotation(fwd, up);

        _reflectionCamera.worldToCameraMatrix = mainCam.worldToCameraMatrix * reflection;

        // Oblique Near-Plane: clippt alles unterhalb der Spiegelebene
        Vector4 clipPlaneCS = CameraSpacePlane(_reflectionCamera.worldToCameraMatrix, planePos, planeNormal);
        _reflectionCamera.projectionMatrix = mainCam.projectionMatrix;
        Matrix4x4 oblique = _reflectionCamera.CalculateObliqueMatrix(clipPlaneCS);

        // X-Flip: kompensiert die umgekehrte Windung der Spiegelmatrix,
        // damit normales Backface-Culling korrekt bleibt (Shader: 1-u).
        _reflectionCamera.projectionMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)) * oblique;
    }

    // Spiegelmatrix an der Ebene (plane.xyz = Normale, plane.w = Distanz)
    static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        var m = Matrix4x4.identity;
        m.m00 = 1f - 2f * plane.x * plane.x;
        m.m01 = -2f * plane.x * plane.y;
        m.m02 = -2f * plane.x * plane.z;
        m.m03 = -2f * plane.w * plane.x;
        m.m10 = -2f * plane.y * plane.x;
        m.m11 = 1f - 2f * plane.y * plane.y;
        m.m12 = -2f * plane.y * plane.z;
        m.m13 = -2f * plane.w * plane.y;
        m.m20 = -2f * plane.z * plane.x;
        m.m21 = -2f * plane.z * plane.y;
        m.m22 = 1f - 2f * plane.z * plane.z;
        m.m23 = -2f * plane.w * plane.z;
        return m;
    }

    // Ebene in den Kameraraum der Spiegelkamera transformieren
    static Vector4 CameraSpacePlane(Matrix4x4 worldToCamera, Vector3 pos, Vector3 normal)
    {
        Vector3 cpos = worldToCamera.MultiplyPoint(pos);
        Vector3 cnormal = worldToCamera.MultiplyVector(normal).normalized;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }
}
