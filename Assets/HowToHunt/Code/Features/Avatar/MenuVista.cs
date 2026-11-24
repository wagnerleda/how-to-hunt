using HowToHunt.Core;
using HowToHunt.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace HowToHunt.Avatar
{
    /// <summary>
    /// The main menu's background (owner, 2026-11-24): the Plains at sunset seen from behind a hunter sitting on a rock
    /// with a rifle (RestingHunter). While no party is running it owns the main camera (a slow drifting shot) and turns
    /// the light to a low orange sun with warm ambient and fog; the moment a party starts it hands everything back as
    /// the level set it (the hunter goes, the daylight returns). Placed in the scene by GrayboxSceneBuilder; the
    /// menu's pollen and vignette (MenuBackdrop) sit on top. No allocations per frame.
    /// </summary>
    public sealed class MenuVista : MonoBehaviour
    {
        [SerializeField] private Transform _shot;              // the camera's resting pose
        [SerializeField] private GameObject _hunter;            // shown only on the menu
        [SerializeField] private float _fov = 42f;
        [SerializeField] private Vector3 _sunDirection = new Vector3(1f, -0.1f, 0f); // the light's travel: from the low sun into the scene
        [SerializeField] private Color _sunColor = new Color(1f, 0.56f, 0.3f);
        [SerializeField] private float _sunIntensity = 1.25f;
        [SerializeField] private Color _ambientSky = new Color(0.55f, 0.46f, 0.58f);
        [SerializeField] private Color _ambientEquator = new Color(0.9f, 0.58f, 0.42f);
        [SerializeField] private Color _ambientGround = new Color(0.3f, 0.22f, 0.2f);
        [SerializeField] private Color _fog = new Color(0.96f, 0.64f, 0.44f);
        [SerializeField] private float _fogStart = 120f, _fogEnd = 700f;
        [SerializeField] private float _atmosphere = 2.1f, _skyExposure = 1.35f, _sunSize = 0.07f;

        private static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness"), ExposureId = Shader.PropertyToID("_Exposure"), SunSizeId = Shader.PropertyToID("_SunSize");
        private const float DriftMetres = 0.35f, DriftDegrees = 1.2f, DriftPeriod = 26f, FarClip = 900f;

        // The level's own light, restored when a party starts.
        private bool _saved, _applied;
        private Quaternion _dayRotation;
        private Color _dayColor, _dayAmbientSky, _dayEquator, _dayGround, _dayFog;
        private float _dayIntensity, _dayFogStart, _dayFogEnd;
        private AmbientMode _dayAmbientMode;
        private Material _daySky, _sunsetSky;

        public void Configure(Transform shot, GameObject hunter, Vector3 sunDirection)
        {
            _shot = shot;
            _hunter = hunter;
            _sunDirection = sunDirection;
        }

        private static bool InSession => NetSession.State == SessionState.Hosting || NetSession.State == SessionState.Connected;

        private void OnDisable() => Apply(false);

        private void OnDestroy()
        {
            if (_sunsetSky != null) Destroy(_sunsetSky);
        }

        private void LateUpdate()
        {
            bool menu = !InSession;
            Apply(menu);
            if (!menu || _shot == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            // A slow drift so the picture breathes (still with reduce motion).
            float t = Time.unscaledTime * Mathf.PI * 2f / DriftPeriod, k = UiFeel.Calm ? 0f : 1f;
            Vector3 drift = _shot.right * (Mathf.Sin(t) * DriftMetres * k) + _shot.up * (Mathf.Sin(t * 0.7f + 1f) * DriftMetres * 0.3f * k);
            Quaternion turn = Quaternion.Euler(Mathf.Sin(t * 0.8f) * DriftDegrees * 0.3f * k, Mathf.Sin(t + 0.6f) * DriftDegrees * k, 0f);
            cam.transform.SetPositionAndRotation(_shot.position + drift, _shot.rotation * turn);
            cam.fieldOfView = _fov;
            if (cam.farClipPlane < FarClip) cam.farClipPlane = FarClip;
        }

        /// <summary>Sunset on (the menu) or the level's daylight back (a party).</summary>
        private void Apply(bool sunset)
        {
            if (_hunter != null && _hunter.activeSelf != sunset) _hunter.SetActive(sunset);
            // No HUD over the menu's picture (coins, hotbar): it belongs to a hunter in a party.
            Canvas hud = Hud.Root.GetComponent<Canvas>();
            if (hud.enabled == sunset) hud.enabled = !sunset;
            if (sunset == _applied) return;
            Light sun = RenderSettings.sun;
            if (sunset)
            {
                if (!_saved && sun != null)
                {
                    _saved = true;
                    _dayRotation = sun.transform.rotation;
                    _dayColor = sun.color;
                    _dayIntensity = sun.intensity;
                    _dayAmbientMode = RenderSettings.ambientMode;
                    _dayAmbientSky = RenderSettings.ambientSkyColor;
                    _daySky = RenderSettings.skybox;
                    _dayEquator = RenderSettings.ambientEquatorColor;
                    _dayGround = RenderSettings.ambientGroundColor;
                    _dayFog = RenderSettings.fogColor;
                    _dayFogStart = RenderSettings.fogStartDistance;
                    _dayFogEnd = RenderSettings.fogEndDistance;
                }
                if (sun != null)
                {
                    sun.transform.rotation = Quaternion.LookRotation(_sunDirection.normalized);
                    sun.color = _sunColor;
                    sun.intensity = _sunIntensity;
                }
                // A thicker, brighter atmosphere: the procedural sky glows orange around the low sun (a copy, so the
                // level's sky material is never changed).
                if (_daySky != null && _daySky.HasProperty(AtmosphereId))
                {
                    if (_sunsetSky == null) _sunsetSky = new Material(_daySky) { name = "Menu Sunset Sky", hideFlags = HideFlags.DontSave };
                    _sunsetSky.SetFloat(AtmosphereId, _atmosphere);
                    if (_sunsetSky.HasProperty(ExposureId)) _sunsetSky.SetFloat(ExposureId, _skyExposure);
                    if (_sunsetSky.HasProperty(SunSizeId)) _sunsetSky.SetFloat(SunSizeId, _sunSize);
                    RenderSettings.skybox = _sunsetSky;
                }
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = _ambientSky;
                RenderSettings.ambientEquatorColor = _ambientEquator;
                RenderSettings.ambientGroundColor = _ambientGround;
                RenderSettings.fogColor = _fog;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = _fogEnd;
            }
            else if (_saved)
            {
                if (sun != null)
                {
                    sun.transform.rotation = _dayRotation;
                    sun.color = _dayColor;
                    sun.intensity = _dayIntensity;
                }
                RenderSettings.ambientMode = _dayAmbientMode;
                RenderSettings.ambientSkyColor = _dayAmbientSky;
                if (_daySky != null) RenderSettings.skybox = _daySky;
                RenderSettings.ambientEquatorColor = _dayEquator;
                RenderSettings.ambientGroundColor = _dayGround;
                RenderSettings.fogColor = _dayFog;
                RenderSettings.fogStartDistance = _dayFogStart;
                RenderSettings.fogEndDistance = _dayFogEnd;
            }
            _applied = sunset;
            if (RenderSettings.skybox != null) DynamicGI.UpdateEnvironment();
        }
    }
}
