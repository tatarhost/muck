using System;
using BepInEx;
using UnityEngine;

namespace MuckChLab
{
    [BepInPlugin("tatarhost.muckchlab", "muckChLab", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string ConfigKey = "muckChLab.config.v1";
        private const int FeatureCount = 52;

        [Serializable]
        private sealed class Settings
        {
            // Visual (15)
            public bool Crosshair = true;
            public bool CrosshairDot = true;
            public bool ShowFps = true;
            public bool ShowFrameTime = false;
            public bool ShowClock = false;
            public bool ShowDate = false;
            public bool ShowWatermark = true;
            public string WatermarkText = "muckChLab";
            public bool ShowCameraPosition = false;
            public bool ShowCameraRotation = false;
            public bool ShowResolution = false;
            public bool FovOverride = false;
            public float Fov = 90f;
            public bool ScreenTint = false;
            public float TintAlpha = 0.08f;

            // Interface (16)
            public bool HideHudCanvas = false;
            public bool ShowControlHints = true;
            public bool CompactMenu = false;
            public bool DimBackground = true;
            public float MenuOpacity = 0.98f;
            public float MenuScale = 1f;
            public float WindowWidth = 610f;
            public float WindowHeight = 510f;
            public bool RememberTab = true;
            public bool ShowSeparators = true;
            public bool ShowFeatureCount = true;
            public bool MenuOnStart = false;
            public string MenuTitle = "muckChLab";
            public int MenuHotkey = 0;
            public bool OverlayAtBottom = false;
            public bool ShowBuildInfo = true;

            // Graphics (11)
            public int FpsLimit = 144;
            public bool VSync = false;
            public bool AntiAliasing = true;
            public int AntiAliasingLevel = 2;
            public bool Shadows = true;
            public float ShadowDistance = 80f;
            public int TextureLimit = 0;
            public bool Anisotropic = true;
            public float LodBias = 1f;
            public int PixelLights = 4;
            public bool RealtimeProbes = true;

            // Audio (4)
            public float MasterVolume = 1f;
            public bool MuteAudio = false;
            public bool PauseAudioOnMenu = false;
            public bool ShowAudioStatus = true;

            // System (6)
            public bool DebugLogging = false;
            public bool ConfirmReset = true;
            public bool SaveOnExit = false;
            public bool ShowUnityVersion = false;
            public bool ShowStartupHint = true;
            public bool ShowRestoreGraphicsButton = true;
        }

        private sealed class QualitySnapshot
        {
            public int VSync;
            public int AntiAliasing;
            public ShadowQuality Shadows;
            public float ShadowDistance;
            public int TextureLimit;
            public AnisotropicFiltering Anisotropic;
            public float LodBias;
            public int PixelLights;
            public bool RealtimeProbes;
            public int TargetFrameRate;
        }

        private readonly string[] _tabs =
        {
            "Визуал", "Интерфейс", "Графика", "Звук", "Система", "Инфо"
        };

        private Settings _settings = new Settings();
        private QualitySnapshot _originalQuality;
        private Rect _window = new Rect(32f, 32f, 610f, 510f);
        private Vector2 _scroll;
        private Texture2D _pixel;
        private int _tab;
        private bool _menuOpen;
        private bool _confirmResetPending;
        private float _fps;
        private float _fpsTimer;
        private int _frames;
        private float _frameMs;
        private float _startedAt;
        private Camera _fovCamera;
        private float _originalFov;
        private bool _fovCaptured;
        private Canvas _hudCanvas;
        private bool _hudOriginalEnabled;
        private bool _hudChanged;
        private float _originalAudioVolume;
        private bool _originalAudioPause;
        private bool _errorLogged;
        private string _lastError = "";

        private void Awake()
        {
            try
            {
                _startedAt = Time.realtimeSinceStartup;
                _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();

                _originalQuality = CaptureQuality();
                _originalAudioVolume = AudioListener.volume;
                _originalAudioPause = AudioListener.pause;

                LoadConfig(false);
                _menuOpen = _settings.MenuOnStart;

                if (_settings.RememberTab)
                    _tab = Mathf.Clamp(PlayerPrefs.GetInt("muckChLab.lastTab", 0), 0, _tabs.Length - 1);

                Logger.LogInfo("muckChLab loaded. Insert opens the menu.");
            }
            catch (Exception ex)
            {
                LogError("Awake", ex);
            }
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(GetMenuKey()))
                {
                    _menuOpen = !_menuOpen;
                    if (_settings.RememberTab)
                    {
                        PlayerPrefs.SetInt("muckChLab.lastTab", _tab);
                        PlayerPrefs.Save();
                    }
                }

                float dt = Time.unscaledDeltaTime;
                _frameMs = dt * 1000f;
                _frames++;
                _fpsTimer += dt;
                if (_fpsTimer >= 0.5f)
                {
                    _fps = _frames / Mathf.Max(_fpsTimer, 0.001f);
                    _frames = 0;
                    _fpsTimer = 0f;
                }

                ApplyRuntimeSettings();
            }
            catch (Exception ex)
            {
                LogError("Update", ex);
            }
        }

        private void ApplyRuntimeSettings()
        {
            try
            {
                Application.targetFrameRate = Mathf.Clamp(_settings.FpsLimit, 30, 300);
                QualitySettings.vSyncCount = _settings.VSync ? 1 : 0;
                QualitySettings.antiAliasing = _settings.AntiAliasing ? _settings.AntiAliasingLevel : 0;
                QualitySettings.shadows = _settings.Shadows ? ShadowQuality.All : ShadowQuality.Disable;
                QualitySettings.shadowDistance = Mathf.Clamp(_settings.ShadowDistance, 0f, 300f);
                QualitySettings.masterTextureLimit = Mathf.Clamp(_settings.TextureLimit, 0, 3);
                QualitySettings.anisotropicFiltering = _settings.Anisotropic
                    ? AnisotropicFiltering.ForceEnable
                    : AnisotropicFiltering.Disable;
                QualitySettings.lodBias = Mathf.Clamp(_settings.LodBias, 0.1f, 5f);
                QualitySettings.pixelLightCount = Mathf.Clamp(_settings.PixelLights, 0, 16);
                QualitySettings.realtimeReflectionProbes = _settings.RealtimeProbes;
            }
            catch (Exception ex)
            {
                LogError("Graphics settings", ex);
            }

            try
            {
                AudioListener.volume = _settings.MuteAudio ? 0f : Mathf.Clamp01(_settings.MasterVolume);
                AudioListener.pause = _settings.PauseAudioOnMenu && _menuOpen
                    ? true
                    : _originalAudioPause;
            }
            catch (Exception ex)
            {
                LogError("Audio settings", ex);
            }

            try
            {
                ApplyFov();
                ApplyHudCanvas();
            }
            catch (Exception ex)
            {
                LogError("Camera/HUD settings", ex);
            }
        }

        private void ApplyFov()
        {
            Camera cam = Camera.main;
            if (_settings.FovOverride && cam != null)
            {
                if (!_fovCaptured || _fovCamera != cam)
                {
                    _fovCamera = cam;
                    _originalFov = cam.fieldOfView;
                    _fovCaptured = true;
                }

                cam.fieldOfView = Mathf.Clamp(_settings.Fov, 60f, 120f);
            }
            else if (_fovCaptured)
            {
                if (_fovCamera != null)
                    _fovCamera.fieldOfView = _originalFov;

                _fovCamera = null;
                _fovCaptured = false;
            }
        }

        private void ApplyHudCanvas()
        {
            if (_settings.HideHudCanvas)
            {
                if (_hudCanvas == null)
                {
                    _hudCanvas = FindObjectOfType<Canvas>();
                    if (_hudCanvas != null)
                    {
                        _hudOriginalEnabled = _hudCanvas.enabled;
                        _hudChanged = true;
                    }
                }

                if (_hudCanvas != null)
                    _hudCanvas.enabled = false;
            }
            else if (_hudChanged)
            {
                if (_hudCanvas != null)
                    _hudCanvas.enabled = _hudOriginalEnabled;

                _hudCanvas = null;
                _hudChanged = false;
            }
        }

        private void OnGUI()
        {
            try
            {
                DrawOverlays();
                if (!_menuOpen)
                    return;

                Color oldGuiColor = GUI.color;
                Color oldBackground = GUI.backgroundColor;
                Matrix4x4 oldMatrix = GUI.matrix;

                if (_settings.DimBackground)
                    DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f));

                float scale = Mathf.Clamp(_settings.MenuScale, 0.75f, 1.35f);
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp(_settings.MenuOpacity, 0.45f, 1f));
                GUI.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 0.98f);

                _window.width = Mathf.Clamp(_settings.WindowWidth, 480f, 850f);
                _window.height = Mathf.Clamp(_settings.WindowHeight, 390f, 760f);
                _window = GUI.Window(731920, _window, DrawWindow, _settings.MenuTitle);

                GUI.matrix = oldMatrix;
                GUI.color = oldGuiColor;
                GUI.backgroundColor = oldBackground;
            }
            catch (Exception ex)
            {
                LogError("OnGUI", ex);
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool selected = _tab == i;
                GUI.backgroundColor = selected
                    ? new Color(0.25f, 0.48f, 0.72f, 1f)
                    : new Color(0.18f, 0.18f, 0.20f, 1f);

                if (GUILayout.Button(_tabs[i], GUILayout.Height(_settings.CompactMenu ? 24f : 30f)))
                {
                    _tab = i;
                    if (_settings.RememberTab)
                    {
                        PlayerPrefs.SetInt("muckChLab.lastTab", _tab);
                        PlayerPrefs.Save();
                    }
                }
            }
            GUI.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 0.98f);
            GUILayout.EndHorizontal();

            if (_settings.ShowSeparators)
                GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1f));

            _scroll = GUILayout.BeginScrollView(_scroll, false, true);
            switch (_tab)
            {
                case 0: DrawVisualTab(); break;
                case 1: DrawInterfaceTab(); break;
                case 2: DrawGraphicsTab(); break;
                case 3: DrawAudioTab(); break;
                case 4: DrawSystemTab(); break;
                default: DrawInfoTab(); break;
            }
            GUILayout.EndScrollView();

            if (_settings.ShowFeatureCount)
                GUILayout.Label(FeatureCount + " visual / system settings  |  v1.0.0");

            if (_settings.ShowControlHints)
                GUILayout.Label("Insert / F1 / F2 / Home — открыть меню. Перетаскивай окно за заголовок.");

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _window.width, 24));
        }

        private void DrawVisualTab()
        {
            ToggleLine("Прицел", ref _settings.Crosshair);
            ToggleLine("Точка в центре прицела", ref _settings.CrosshairDot);
            ToggleLine("Счётчик FPS", ref _settings.ShowFps);
            ToggleLine("Время кадра (мс)", ref _settings.ShowFrameTime);
            ToggleLine("Часы", ref _settings.ShowClock);
            ToggleLine("Дата", ref _settings.ShowDate);
            ToggleLine("Водяной знак", ref _settings.ShowWatermark);
            GUILayout.Label("Текст водяного знака");
            _settings.WatermarkText = GUILayout.TextField(_settings.WatermarkText, 28);
            ToggleLine("Позиция камеры", ref _settings.ShowCameraPosition);
            ToggleLine("Поворот камеры", ref _settings.ShowCameraRotation);
            ToggleLine("Разрешение экрана", ref _settings.ShowResolution);
            ToggleLine("Переопределять FOV", ref _settings.FovOverride);
            _settings.Fov = SliderLine("FOV", _settings.Fov, 60f, 120f, "0");
            ToggleLine("Цветовой фильтр экрана", ref _settings.ScreenTint);
            _settings.TintAlpha = SliderLine("Интенсивность фильтра", _settings.TintAlpha, 0f, 0.25f, "0.00");
        }

        private void DrawInterfaceTab()
        {
            ToggleLine("Скрывать Canvas HUD", ref _settings.HideHudCanvas);
            ToggleLine("Подсказки управления", ref _settings.ShowControlHints);
            ToggleLine("Компактное меню", ref _settings.CompactMenu);
            ToggleLine("Затемнять фон меню", ref _settings.DimBackground);
            _settings.MenuOpacity = SliderLine("Непрозрачность меню", _settings.MenuOpacity, 0.45f, 1f, "0.00");
            _settings.MenuScale = SliderLine("Масштаб меню", _settings.MenuScale, 0.75f, 1.35f, "0.00");
            _settings.WindowWidth = SliderLine("Ширина окна", _settings.WindowWidth, 480f, 850f, "0");
            _settings.WindowHeight = SliderLine("Высота окна", _settings.WindowHeight, 390f, 760f, "0");

            if (GUILayout.Button("Переместить меню в центр"))
            {
                _window.x = (Screen.width - _window.width) * 0.5f;
                _window.y = (Screen.height - _window.height) * 0.5f;
            }

            ToggleLine("Запоминать вкладку", ref _settings.RememberTab);
            ToggleLine("Разделители", ref _settings.ShowSeparators);
            ToggleLine("Счётчик функций", ref _settings.ShowFeatureCount);
            ToggleLine("Открывать меню при запуске", ref _settings.MenuOnStart);
            GUILayout.Label("Заголовок меню");
            _settings.MenuTitle = GUILayout.TextField(_settings.MenuTitle, 28);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Клавиша меню: " + GetMenuKey(), GUILayout.Width(190f));
            if (GUILayout.Button("Сменить клавишу", GUILayout.Width(150f)))
                _settings.MenuHotkey = (_settings.MenuHotkey + 1) % 4;
            GUILayout.EndHorizontal();
            ToggleLine("Статусная строка снизу", ref _settings.OverlayAtBottom);
        }

        private void DrawGraphicsTab()
        {
            _settings.FpsLimit = Mathf.RoundToInt(SliderLine("Лимит FPS", _settings.FpsLimit, 30f, 300f, "0"));
            ToggleLine("VSync", ref _settings.VSync);
            ToggleLine("Сглаживание (MSAA)", ref _settings.AntiAliasing);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Уровень MSAA: " + _settings.AntiAliasingLevel);
            if (GUILayout.Button("Изменить", GUILayout.Width(100f)))
                _settings.AntiAliasingLevel = NextValue(_settings.AntiAliasingLevel, new[] { 2, 4, 8 });
            GUILayout.EndHorizontal();
            ToggleLine("Тени", ref _settings.Shadows);
            _settings.ShadowDistance = SliderLine("Дальность теней", _settings.ShadowDistance, 0f, 300f, "0");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Качество текстур: " + _settings.TextureLimit);
            if (GUILayout.Button("Изменить", GUILayout.Width(100f)))
                _settings.TextureLimit = (_settings.TextureLimit + 1) % 4;
            GUILayout.EndHorizontal();
            ToggleLine("Анизотропная фильтрация", ref _settings.Anisotropic);
            _settings.LodBias = SliderLine("LOD Bias", _settings.LodBias, 0.1f, 5f, "0.0");
            _settings.PixelLights = Mathf.RoundToInt(SliderLine("Pixel Lights", _settings.PixelLights, 0f, 16f, "0"));
            ToggleLine("Realtime Reflection Probes", ref _settings.RealtimeProbes);
        }

        private void DrawAudioTab()
        {
            _settings.MasterVolume = SliderLine("Громкость", _settings.MasterVolume, 0f, 1f, "0.00");
            ToggleLine("Без звука", ref _settings.MuteAudio);
            ToggleLine("Приостанавливать звук при открытом меню", ref _settings.PauseAudioOnMenu);
            ToggleLine("Показывать состояние звука", ref _settings.ShowAudioStatus);
            GUILayout.Label("Текущая громкость: " + (_settings.MuteAudio ? "0%" : Mathf.RoundToInt(_settings.MasterVolume * 100f) + "%"));
        }

        private void DrawSystemTab()
        {
            if (GUILayout.Button("Сохранить конфигурацию"))
                SaveConfig();

            if (GUILayout.Button("Загрузить конфигурацию"))
                LoadConfig(true);

            if (GUILayout.Button("Сбросить конфигурацию"))
            {
                if (!_settings.ConfirmReset)
                    ResetConfig();
                else
                    _confirmResetPending = true;
            }

            if (_confirmResetPending)
            {
                GUILayout.Label("Точно сбросить все настройки?");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Да, сбросить"))
                    ResetConfig();
                if (GUILayout.Button("Отмена"))
                    _confirmResetPending = false;
                GUILayout.EndHorizontal();
            }

            if (_settings.ShowRestoreGraphicsButton && GUILayout.Button("Восстановить исходные настройки графики"))
                RestoreGraphics();

            ToggleLine("Подробные логи", ref _settings.DebugLogging);
            ToggleLine("Сохранять конфиг при выходе", ref _settings.SaveOnExit);
            ToggleLine("Показывать версию Unity", ref _settings.ShowUnityVersion);
            ToggleLine("Подсказка при запуске", ref _settings.ShowStartupHint);
            ToggleLine("Показывать build info", ref _settings.ShowBuildInfo);
            ToggleLine("Подтверждать сброс настроек", ref _settings.ConfirmReset);
            GUILayout.Label("Конфиг хранится в Unity PlayerPrefs.");
        }

        private void DrawInfoTab()
        {
            GUILayout.Label("muckChLab — локальный визуальный и системный мод");
            GUILayout.Label("Версия: 1.0.0");
            GUILayout.Label("BepInEx: 5.4.23.x");
            GUILayout.Label("Целевая версия Unity: 2019.4");
            GUILayout.Label("Меню: " + GetMenuKey());
            GUILayout.Label("FPS: " + _fps.ToString("0.0"));
            GUILayout.Label("Время работы: " + FormatUptime(Time.realtimeSinceStartup - _startedAt));
            GUILayout.Label("Unity: " + Application.unityVersion);
            GUILayout.Label("Экран: " + Screen.width + " x " + Screen.height);
            GUILayout.Label("Активная вкладка: " + _tabs[_tab]);
            GUILayout.Label("Включено настроек: " + CountEnabledSettings());
            GUILayout.Label("Игровые классы Muck не вызываются: используются API Unity и BepInEx.");
            if (!string.IsNullOrEmpty(_lastError))
                GUILayout.Label("Последняя ошибка: " + _lastError);
        }

        private void DrawOverlays()
        {
            if (_settings.ScreenTint)
                DrawRect(new Rect(0, 0, Screen.width, Screen.height),
                    new Color(0.18f, 0.30f, 0.42f, Mathf.Clamp(_settings.TintAlpha, 0f, 0.25f)));

            if (_settings.Crosshair)
            {
                float cx = Screen.width * 0.5f;
                float cy = Screen.height * 0.5f;
                DrawRect(new Rect(cx - 9f, cy - 1f, 6f, 2f), Color.white);
                DrawRect(new Rect(cx + 3f, cy - 1f, 6f, 2f), Color.white);
                DrawRect(new Rect(cx - 1f, cy - 9f, 2f, 6f), Color.white);
                DrawRect(new Rect(cx - 1f, cy + 3f, 2f, 6f), Color.white);
                if (_settings.CrosshairDot)
                    DrawRect(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), Color.white);
            }

            string overlay = BuildOverlayText();
            if (!string.IsNullOrEmpty(overlay))
            {
                float y = _settings.OverlayAtBottom ? Screen.height - 24f : 8f;
                GUI.Label(new Rect(8f, y, Screen.width - 16f, 20f), overlay);
            }

            if (_settings.ShowWatermark)
                GUI.Label(new Rect(Screen.width - 220f, 8f, 210f, 24f), _settings.WatermarkText);

            if (_settings.ShowBuildInfo)
                GUI.Label(new Rect(8f, Screen.height - 64f, 210f, 18f), "muckChLab 1.0.0");

            if (_settings.ShowAudioStatus)
                GUI.Label(new Rect(8f, Screen.height - 44f, 180f, 20f),
                    _settings.MuteAudio ? "AUDIO: MUTED" : "AUDIO: " + Mathf.RoundToInt(_settings.MasterVolume * 100f) + "%");

            if (_settings.ShowStartupHint && Time.realtimeSinceStartup - _startedAt < 12f)
                GUI.Label(new Rect(8f, Screen.height - 24f, 300f, 18f), "muckChLab: press " + GetMenuKey() + " for menu");
        }

        private string BuildOverlayText()
        {
            string result = "";
            if (_settings.ShowFps) result += "FPS " + _fps.ToString("0") + "  ";
            if (_settings.ShowFrameTime) result += _frameMs.ToString("0.0") + " ms  ";
            if (_settings.ShowClock) result += DateTime.Now.ToString("HH:mm:ss") + "  ";
            if (_settings.ShowDate) result += DateTime.Now.ToString("yyyy-MM-dd") + "  ";
            if (_settings.ShowResolution) result += Screen.width + "x" + Screen.height + "  ";

            Camera cam = Camera.main;
            if (cam != null && _settings.ShowCameraPosition)
            {
                Vector3 p = cam.transform.position;
                result += "CAM " + p.x.ToString("0.0") + ", " + p.y.ToString("0.0") + ", " + p.z.ToString("0.0") + "  ";
            }

            if (cam != null && _settings.ShowCameraRotation)
            {
                Vector3 r = cam.transform.eulerAngles;
                result += "ROT " + r.x.ToString("0") + ", " + r.y.ToString("0") + ", " + r.z.ToString("0") + "  ";
            }

            if (_settings.ShowUnityVersion)
                result += "Unity " + Application.unityVersion + "  ";

            return result;
        }

        private int CountEnabledSettings()
        {
            int count = 0;
            if (_settings.Crosshair) count++;
            if (_settings.CrosshairDot) count++;
            if (_settings.ShowFps) count++;
            if (_settings.ShowFrameTime) count++;
            if (_settings.ShowClock) count++;
            if (_settings.ShowDate) count++;
            if (_settings.ShowWatermark) count++;
            if (_settings.ShowCameraPosition) count++;
            if (_settings.ShowCameraRotation) count++;
            if (_settings.ShowResolution) count++;
            if (_settings.FovOverride) count++;
            if (_settings.ScreenTint) count++;
            if (_settings.HideHudCanvas) count++;
            if (_settings.ShowControlHints) count++;
            if (_settings.CompactMenu) count++;
            if (_settings.DimBackground) count++;
            if (_settings.VSync) count++;
            if (_settings.AntiAliasing) count++;
            if (_settings.Shadows) count++;
            if (_settings.Anisotropic) count++;
            if (_settings.RealtimeProbes) count++;
            if (_settings.MuteAudio) count++;
            if (_settings.PauseAudioOnMenu) count++;
            return count;
        }

        private void DrawRect(Rect rect, Color color)
        {
            if (_pixel == null)
                return;

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }

        private static void ToggleLine(string label, ref bool value)
        {
            value = GUILayout.Toggle(value, label);
        }

        private static float SliderLine(string label, float value, float min, float max, string format)
        {
            GUILayout.Label(label + ": " + value.ToString(format));
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private KeyCode GetMenuKey()
        {
            switch (_settings.MenuHotkey)
            {
                case 1: return KeyCode.F1;
                case 2: return KeyCode.F2;
                case 3: return KeyCode.Home;
                default: return KeyCode.Insert;
            }
        }

        private static int NextValue(int current, int[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] == current)
                    return values[(i + 1) % values.Length];

            return values[0];
        }

        private QualitySnapshot CaptureQuality()
        {
            return new QualitySnapshot
            {
                VSync = QualitySettings.vSyncCount,
                AntiAliasing = QualitySettings.antiAliasing,
                Shadows = QualitySettings.shadows,
                ShadowDistance = QualitySettings.shadowDistance,
                TextureLimit = QualitySettings.masterTextureLimit,
                Anisotropic = QualitySettings.anisotropicFiltering,
                LodBias = QualitySettings.lodBias,
                PixelLights = QualitySettings.pixelLightCount,
                RealtimeProbes = QualitySettings.realtimeReflectionProbes,
                TargetFrameRate = Application.targetFrameRate
            };
        }

        private void RestoreGraphics()
        {
            try
            {
                if (_originalQuality == null)
                    return;

                _settings.VSync = _originalQuality.VSync > 0;
                _settings.AntiAliasing = _originalQuality.AntiAliasing > 0;
                _settings.AntiAliasingLevel = NextValue(2, new[] { 2, 4, 8 });
                if (_originalQuality.AntiAliasing == 4 || _originalQuality.AntiAliasing == 8)
                    _settings.AntiAliasingLevel = _originalQuality.AntiAliasing;
                _settings.Shadows = _originalQuality.Shadows != ShadowQuality.Disable;
                _settings.ShadowDistance = _originalQuality.ShadowDistance;
                _settings.TextureLimit = _originalQuality.TextureLimit;
                _settings.Anisotropic = _originalQuality.Anisotropic != AnisotropicFiltering.Disable;
                _settings.LodBias = _originalQuality.LodBias;
                _settings.PixelLights = _originalQuality.PixelLights;
                _settings.RealtimeProbes = _originalQuality.RealtimeProbes;
                _settings.FpsLimit = Mathf.Clamp(_originalQuality.TargetFrameRate, 30, 300);
                Logger.LogInfo("Original graphics settings restored as muckChLab settings.");
            }
            catch (Exception ex)
            {
                LogError("Restore graphics", ex);
            }
        }

        private void ResetConfig()
        {
            _settings = new Settings();
            _confirmResetPending = false;
            if (_fovCaptured && _fovCamera != null)
                _fovCamera.fieldOfView = _originalFov;
            _fovCaptured = false;
            ApplyHudCanvas();
            Logger.LogInfo("Configuration reset.");
        }

        private void SaveConfig()
        {
            try
            {
                PlayerPrefs.SetString(ConfigKey, JsonUtility.ToJson(_settings));
                PlayerPrefs.Save();
                Logger.LogInfo("Configuration saved.");
            }
            catch (Exception ex)
            {
                LogError("Save config", ex);
            }
        }

        private void LoadConfig(bool log)
        {
            try
            {
                if (!PlayerPrefs.HasKey(ConfigKey))
                    return;

                Settings loaded = JsonUtility.FromJson<Settings>(PlayerPrefs.GetString(ConfigKey));
                if (loaded != null)
                    _settings = loaded;

                if (log)
                    Logger.LogInfo("Configuration loaded.");
            }
            catch (Exception ex)
            {
                LogError("Load config", ex);
            }
        }

        private static string FormatUptime(float seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ((int)span.TotalHours).ToString("00") + ":" +
                   span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
        }

        private void LogError(string context, Exception ex)
        {
            _lastError = context + ": " + ex.GetType().Name;
            if (!_errorLogged || _settings.DebugLogging)
            {
                Logger.LogError(context + " failed: " + ex);
                _errorLogged = true;
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_hudChanged && _hudCanvas != null)
                    _hudCanvas.enabled = _hudOriginalEnabled;

                if (_fovCaptured && _fovCamera != null)
                    _fovCamera.fieldOfView = _originalFov;

                RestoreGraphics();
                AudioListener.volume = _originalAudioVolume;
                AudioListener.pause = _originalAudioPause;

                if (_settings.SaveOnExit)
                    SaveConfig();

                if (_pixel != null)
                    Destroy(_pixel);
            }
            catch (Exception ex)
            {
                LogError("OnDestroy", ex);
            }
        }
    }
}