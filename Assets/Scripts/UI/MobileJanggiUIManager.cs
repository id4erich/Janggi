using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Janggi
{
    // 화면 UI 진입점: 전적, 입력·타이머 갱신, 차례 표시, 화면 구성. 나머지는 partial 파일(Panels·Setup·Effects)
    public partial class MobileJanggiUIManager : MonoBehaviour
    {
        public static MobileJanggiUIManager Instance { get; private set; }

        private Text timerText;
        private Text recordText;

        private float turnTimer = 60f;
        private bool isTimerRunning = true;
        private bool isMatchEnded = false;
        private int thinkingDots;

        private const string KEY_WIN = "Janggi_Wins";
        private const string KEY_LOSS = "Janggi_Losses";
        private const string KEY_DRAW = "Janggi_Draws";

        public int Wins => PlayerPrefs.GetInt(KEY_WIN, 0);
        public int Losses => PlayerPrefs.GetInt(KEY_LOSS, 0);
        public int Draws => PlayerPrefs.GetInt(KEY_DRAW, 0);
        public int TotalGames => Wins + Losses + Draws;

        public void RecordWin()
        {
            if (isMatchEnded) return;
            isMatchEnded = true;
            PlayerPrefs.SetInt(KEY_WIN, Wins + 1);
            PlayerPrefs.Save();
            UpdateRecordDisplay();
        }

        public void RecordLoss()
        {
            if (isMatchEnded) return;
            isMatchEnded = true;
            PlayerPrefs.SetInt(KEY_LOSS, Losses + 1);
            PlayerPrefs.Save();
            UpdateRecordDisplay();
        }

        public void RecordDraw()
        {
            if (isMatchEnded) return;
            isMatchEnded = true;
            PlayerPrefs.SetInt(KEY_DRAW, Draws + 1);
            PlayerPrefs.Save();
            UpdateRecordDisplay();
        }

        private void UpdateRecordDisplay()
        {
            if (recordText != null)
            {
                int winRate = TotalGames > 0 ? Mathf.RoundToInt((float)Wins / TotalGames * 100f) : 0;
                string drawStr = Draws > 0 ? $" {Draws}무" : "";
                recordText.text = $"{TotalGames}전 {Wins}승 {Losses}패{drawStr}\n승률 {winRate}%";
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private const string KEY_CHO_PLACEMENT = "Janggi_Cho_Placement";
        private const string KEY_HAN_PLACEMENT = "Janggi_Han_Placement";

        private PlacementType choSelectedPlacement = PlacementType.MaSangMaSang;
        private PlacementType hanSelectedPlacement = PlacementType.MaSangMaSang;
        private int aiSelectedLevel = 5;
        private int timeLimitSelected;
        private GameManager.GameMode modeSelected;
        private static readonly string[] ModeLabels = { "AI와 · 나는 초", "AI와 · 나는 한", "둘이서" };
        private GameManager.RuleSet rulesSelected;
        private static readonly string[] RuleLabels = { "일반 (빅장 없음)", "빅장 = 무승부", "대회 (점수 판정)" };

        private void Start()
        {
            choSelectedPlacement = (PlacementType)PlayerPrefs.GetInt(KEY_CHO_PLACEMENT, (int)PlacementType.MaSangMaSang);
            hanSelectedPlacement = (PlacementType)PlayerPrefs.GetInt(KEY_HAN_PLACEMENT, (int)PlacementType.MaSangMaSang);
            if (GameManager.Instance != null)
            {
                aiSelectedLevel = GameManager.Instance.aiLevel;
                timeLimitSelected = GameManager.Instance.moveTimeLimitSec;
                modeSelected = GameManager.Instance.Mode;
                rulesSelected = GameManager.Instance.ruleSet;
            }

            BuildMobileUI();
        }

#if ENABLE_INPUT_SYSTEM
        // 뒤로 가기(안드로이드)·ESC는 입력 이벤트로 받음. 매 프레임 키 상태를 보면 앱을 켠 뒤 첫 누름을 놓침
        private bool backRequested;

        private void OnEnable() => UnityEngine.InputSystem.InputSystem.onEvent += OnInputEvent;
        private void OnDisable() => UnityEngine.InputSystem.InputSystem.onEvent -= OnInputEvent;

        private void OnInputEvent(UnityEngine.InputSystem.LowLevel.InputEventPtr eventPtr, UnityEngine.InputSystem.InputDevice device)
        {
            if (!(device is UnityEngine.InputSystem.Keyboard keyboard)) return;
            if (!eventPtr.IsA<UnityEngine.InputSystem.LowLevel.StateEvent>() && !eventPtr.IsA<UnityEngine.InputSystem.LowLevel.DeltaStateEvent>()) return;
            // 이벤트가 적용되기 전이므로 isPressed는 이전 상태: 눌리지 않았다가 눌린 순간만
            if (UnityEngine.InputSystem.InputControlExtensions.ReadValueFromEvent(keyboard.escapeKey, eventPtr, out float value) && value > 0.5f && !keyboard.escapeKey.isPressed)
                backRequested = true;
        }
#endif

        private void Update()
        {
            // 뒤로 가기(안드로이드)·ESC: 열린 창이 있으면 닫고, 없으면 메뉴를 엶
#if ENABLE_INPUT_SYSTEM
            bool back = backRequested;
            backRequested = false;
#else
            bool back = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (back)
            {
                if (activeOverlay != null) CloseOverlay();
                else if (setupDialog != null) Destroy(setupDialog);
                else ShowMenuSheet();
            }

            GameManager gm = GameManager.Instance;
            bool humanTurn = gm == null || (gm.IsHumanTurn && !gm.IsGameOver);

            // AI가 생각하는 동안은 점이 움직이는 "생각 중" 표시 (타이머는 사람 차례에만 흐름)
            if (gm != null && !gm.IsHumanTurn && !gm.IsGameOver)
            {
                int dotCount = 1 + (int)(Time.time * 2.5f) % 3;
                if (dotCount != thinkingDots) // 점 개수가 바뀔 때만 문자열을 새로 만듦
                {
                    thinkingDots = dotCount;
                    PlayerPanel ai = gm.aiTeam == Team.Cho ? choPanel : hanPanel;
                    if (ai != null) ai.Status.text = "생각 중" + new string('.', dotCount);
                    if (timerText != null) { timerText.text = gm.moveTimeLimitSec > 0 ? "--" : ""; timerText.color = TextSubColor; }
                }
            }
            else thinkingDots = 0;

            // 메뉴·기보·설정 창을 보는 동안은 제한 시간이 흐르지 않음
            if (isTimerRunning && humanTurn && timerText != null && activeOverlay == null && setupDialog == null)
            {
                if (gm == null || gm.moveTimeLimitSec <= 0)
                {
                    if (timerText.text.Length > 0) timerText.text = ""; // 제한 없음
                }
                else if (turnTimer > 0f)
                {
                    turnTimer -= Time.deltaTime;
                    int sec = Mathf.CeilToInt(Mathf.Max(0f, turnTimer));
                    timerText.text = $"{sec}초";
                    timerText.color = sec <= 10 ? HanColor : AccentGold;
                    if (turnTimer <= 0f) gm.TimeOut();
                }
            }

            // 모바일 멀티터치 / 제스처 핀치 줌 & 모바일 입력 지원
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 2)
            {
                var touch0 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                var touch1 = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[1];

                float prevDist = (touch0.history[0].screenPosition - touch1.history[0].screenPosition).magnitude;
                float currDist = (touch0.screenPosition - touch1.screenPosition).magnitude;

                float delta = currDist - prevDist;
                if (Camera.main != null)
                {
                    Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView - delta * 0.05f, 12f, 60f);
                }
            }
#endif
        }

        public void UpdateTurnDisplay(Team turn)
        {
            turnTimer = GameManager.Instance != null ? GameManager.Instance.moveTimeLimitSec : 0f; // 턴 전환 시 타이머 리셋
            thinkingDots = 0;
            GameManager gm = GameManager.Instance;
            foreach (var panel in new[] { hanPanel, choPanel })
            {
                if (panel == null) continue;
                bool isAi = gm != null && gm.isPlayerVsAI && panel.Team == gm.aiTeam;
                bool active = panel.Team == turn && (gm == null || !gm.IsGameOver);
                string side = panel.Team == Team.Cho ? "초(楚)" : "한(漢)";
                panel.Name.text = isAi ? $"{side} · AI {gm.aiLevel}단" : gm != null && !gm.isPlayerVsAI ? side : $"{side} · 나";
                panel.Status.text = gm != null && gm.IsGameOver ? "대국 종료" : active ? (isAi ? "생각 중" : "내 차례") : "대기 중";
                panel.Status.color = active ? AccentGold : TextSubColor;
                panel.Outline.enabled = active;
            }
            bool humanCanAct = gm == null || (gm.IsHumanTurn && !gm.IsGameOver);
            if (passButton != null) passButton.interactable = humanCanAct;
            if (hintButton != null) hintButton.interactable = humanCanAct;
            if (undoButton != null) undoButton.interactable = gm == null || gm.CanUndo;
        }

        private static Sprite cachedPillSprite;

        private Sprite GetRoundedSprite(int cornerRadius)
        {
            if (cachedPillSprite != null) return cachedPillSprite;

            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            float r = size * 0.45f;
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);
                    
                    // Rounded Pill/Circle Corner Mask
                    float cx = Mathf.Max(0, dx - (center - r));
                    float cy = Mathf.Max(0, dy - (center - r));
                    float dist = Mathf.Sqrt(cx * cx + cy * cy);

                    if (dist <= r)
                    {
                        float alpha = Mathf.Clamp01(r - dist + 1f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Vector4 border = new Vector4(r, r, r, r);
            cachedPillSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
            return cachedPillSprite;
        }

        private void BuildMobileUI()
        {
            // EventSystem 존재 여부 검사 및 생성
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }

            // Canvas (CanvasScaler - Mobile Responsive)
            GameObject canvasObj = new GameObject("MobileJanggiCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // 모바일 표준 9:16 레퍼런스
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            Font hangulFont = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 28);
            if (hangulFont == null) hangulFont = Font.CreateDynamicFontFromOSFont("NanumGothic", 28);
            if (hangulFont == null) hangulFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Sprite pillSprite = GetRoundedSprite(48);

            uiFont = hangulFont;
            uiCanvas = canvasObj.transform;

            // 장기판 방향과 같게: 평소엔 위 한(漢)·아래 초(楚), 내가 한으로 두면 뒤집음
            bool flipped = GameManager.Instance != null && GameManager.Instance.ViewFlipped;
            hanPanel = CreatePlayerPanel(canvasObj.transform, Team.Han, top: !flipped);
            choPanel = CreatePlayerPanel(canvasObj.transform, Team.Cho, top: flipped);
            PlayerPanel topPanel = flipped ? choPanel : hanPanel, bottomPanel = flipped ? hanPanel : choPanel;

            // 전적은 상대 패널 오른쪽, 내 남은 시간은 내 패널 오른쪽
            recordText = CreatePanelText(topPanel.Root, "Record", new Vector2(-28f, 0f), new Vector2(300f, 84f), 23, TextAnchor.MiddleRight, TextSubColor);
            recordText.rectTransform.anchorMin = recordText.rectTransform.anchorMax = recordText.rectTransform.pivot = new Vector2(1f, 0.5f);
            timerText = CreatePanelText(bottomPanel.Root, "Timer", new Vector2(-28f, 0f), new Vector2(200f, 84f), 40, TextAnchor.MiddleRight, AccentGold);
            timerText.rectTransform.anchorMin = timerText.rectTransform.anchorMax = timerText.rectTransform.pivot = new Vector2(1f, 0.5f);
            timerText.fontStyle = FontStyle.Bold;
            UpdateRecordDisplay();

            // 하단 도구 막대: 아이콘 + 글자, 화면 폭에 맞춰 4등분
            GameObject toolbar = new GameObject("Toolbar");
            toolbar.transform.SetParent(canvasObj.transform, false);
            RectTransform tbRect = toolbar.AddComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0f, 0f);
            tbRect.anchorMax = new Vector2(1f, 0f);
            tbRect.pivot = new Vector2(0.5f, 0f);
            tbRect.anchoredPosition = Vector2.zero;
            tbRect.sizeDelta = new Vector2(0f, 190f);
            toolbar.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 0.94f);

            HorizontalLayoutGroup tbLayout = toolbar.AddComponent<HorizontalLayoutGroup>();
            tbLayout.padding = new RectOffset(16, 16, 14, 40); // 아래쪽은 제스처 내비게이션 영역 여유
            tbLayout.spacing = 8f;
            tbLayout.childControlWidth = true;
            tbLayout.childControlHeight = true;
            tbLayout.childForceExpandWidth = true;
            tbLayout.childForceExpandHeight = true;

            undoButton = CreateToolbarButton(toolbar.transform, "undo", "무르기", OnUndoClicked);
            passButton = CreateToolbarButton(toolbar.transform, "pass", "한수 쉼", OnPassClicked);
            hintButton = CreateToolbarButton(toolbar.transform, "hint", "힌트", OnHintClicked);
            CreateToolbarButton(toolbar.transform, "view", "시점", OnResetViewClicked);
            CreateToolbarButton(toolbar.transform, "menu", "메뉴", ShowMenuSheet);

            if (GameManager.Instance != null) UpdateTurnDisplay(GameManager.Instance.CurrentTurn);
        }
    }
}
