using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Janggi
{
    public class MobileJanggiUIManager : MonoBehaviour
    {
        public static MobileJanggiUIManager Instance { get; private set; }

        private Text turnStatusText;
        private Text timerText;
        private Text scoreText;
        private Text recordText;
        private Image choTurnIndicator;
        private Image hanTurnIndicator;

        private float turnTimer = 60f;
        private bool isTimerRunning = true;
        private bool isMatchEnded = false;

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
                recordText.text = $"📊 {TotalGames}전 {Wins}승 {Losses}패{drawStr} ( 승률 {winRate:D2}% )";
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

        private void Start()
        {
            choSelectedPlacement = (PlacementType)PlayerPrefs.GetInt(KEY_CHO_PLACEMENT, (int)PlacementType.MaSangMaSang);
            hanSelectedPlacement = (PlacementType)PlayerPrefs.GetInt(KEY_HAN_PLACEMENT, (int)PlacementType.MaSangMaSang);
            if (GameManager.Instance != null) aiSelectedLevel = GameManager.Instance.aiLevel;

            BuildMobileUI();
        }

        private void Update()
        {
            // 백 키 (안드로이드 이전 키 / ESC) 입력 시 앱 종료 또는 종료 확인
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnExitClicked();
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnExitClicked();
            }
#endif

            GameManager gm = GameManager.Instance;
            bool humanTurn = gm == null || (gm.IsHumanTurn && !gm.IsGameOver);

            // AI가 생각하는 동안은 점이 움직이는 "생각 중" 표시 (타이머는 사람 차례에만 흐름)
            if (gm != null && !gm.IsHumanTurn && !gm.IsGameOver && turnStatusText != null)
            {
                string dots = new string('.', 1 + (int)(Time.time * 2.5f) % 3);
                turnStatusText.text = $"🔴 한(漢) 생각 중{dots} · AI {gm.aiLevel}단";
                if (timerText != null) { timerText.text = "⏱️ --"; timerText.color = Color.yellow; }
            }

            if (isTimerRunning && humanTurn && turnTimer > 0f)
            {
                turnTimer -= Time.deltaTime;
                if (timerText != null)
                {
                    int sec = Mathf.CeilToInt(turnTimer);
                    timerText.text = $"⏱️ {sec:D2}초";
                    if (sec <= 10) timerText.color = Color.red;
                    else timerText.color = Color.yellow;
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
                    Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView - delta * 0.05f, 30f, 75f);
                }
            }
#endif
        }

        public void UpdateTurnDisplay(Team turn)
        {
            turnTimer = 60f; // 턴 전환 시 타이머 리셋
            if (turnStatusText != null)
            {
                string level = (GameManager.Instance != null) ? $" · AI {GameManager.Instance.aiLevel}단" : "";
                turnStatusText.text = (turn == Team.Cho) ? $"🔵 초(楚) 턴{level}" : $"🔴 한(漢) 턴{level}";
                turnStatusText.color = (turn == Team.Cho) ? new Color(0.1f, 0.6f, 1f) : new Color(1f, 0.3f, 0.3f);
            }

            if (choTurnIndicator != null) choTurnIndicator.gameObject.SetActive(turn == Team.Cho);
            if (hanTurnIndicator != null) hanTurnIndicator.gameObject.SetActive(turn == Team.Han);
        }

        private static Sprite cachedPillSprite;
        private static Sprite cachedCardSprite;

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

            // Material Design 3 Colors (Dark Elevation & Primary Accent)
            Color m3ContainerColor = new Color(0.13f, 0.15f, 0.20f, 0.92f); // MD3 Surface Container
            Color m3Primary = new Color(0.40f, 0.70f, 1.0f);                 // MD3 Primary Blue
            Color m3Secondary = new Color(0.95f, 0.40f, 0.40f);               // MD3 Secondary Red

            // 1. Top Floating Material Card (중앙 플로팅 대국 상태 카드)
            GameObject topCard = new GameObject("TopMaterialCard");
            topCard.transform.SetParent(canvasObj.transform, false);
            RectTransform topRect = topCard.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.5f, 1f);
            topRect.anchorMax = new Vector2(0.5f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = new Vector2(0, -90); // 상단 노치/상태바 영역 침범 방지 Safe Area 적용
            topRect.sizeDelta = new Vector2(880, 160); // 전적 및 차림 버튼 배치를 위한 너비 확장

            Image topBg = topCard.AddComponent<Image>();
            topBg.sprite = pillSprite;
            topBg.type = Image.Type.Sliced;
            topBg.color = m3ContainerColor;

            // 턴 현황
            GameObject statusObj = new GameObject("TurnStatusText");
            statusObj.transform.SetParent(topCard.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchoredPosition = new Vector2(-60, 36);
            statusRect.sizeDelta = new Vector2(560, 44); // "· AI n단" 표시 공간

            turnStatusText = statusObj.AddComponent<Text>();
            turnStatusText.font = hangulFont;
            turnStatusText.text = (GameManager.Instance != null) ? $"🔵 초(楚) 턴 · AI {GameManager.Instance.aiLevel}단" : "🔵 초(楚) 턴";
            turnStatusText.fontSize = 32;
            turnStatusText.fontStyle = FontStyle.Bold;
            turnStatusText.alignment = TextAnchor.MiddleCenter;
            turnStatusText.color = m3Primary;

            // 타이머 Chip
            GameObject timerObj = new GameObject("TimerChip");
            timerObj.transform.SetParent(topCard.transform, false);
            RectTransform timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchoredPosition = new Vector2(-220, -18);
            timerRect.sizeDelta = new Vector2(220, 36);

            timerText = timerObj.AddComponent<Text>();
            timerText.font = hangulFont;
            timerText.text = "⏱️ 60s";
            timerText.fontSize = 24;
            timerText.fontStyle = FontStyle.Bold;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = new Color(1.0f, 0.85f, 0.30f);

            // 전적 및 승률 표시 Chip (몇전 몇승 몇패 ( 승률 00% ))
            GameObject recordObj = new GameObject("RecordChip");
            recordObj.transform.SetParent(topCard.transform, false);
            RectTransform recordRect = recordObj.AddComponent<RectTransform>();
            recordRect.anchoredPosition = new Vector2(80, -18);
            recordRect.sizeDelta = new Vector2(380, 36);

            recordText = recordObj.AddComponent<Text>();
            recordText.font = hangulFont;
            recordText.fontSize = 21;
            recordText.fontStyle = FontStyle.Bold;
            recordText.alignment = TextAnchor.MiddleCenter;
            recordText.color = new Color(0.85f, 0.95f, 1.0f);
            UpdateRecordDisplay();

            // 차림 설정 버튼 (상단 카드 우측 상단 고정 ⚙️ 차림 설정)
            GameObject setupBtnObj = new GameObject("Btn_TopSetup");
            setupBtnObj.transform.SetParent(topCard.transform, false);
            RectTransform setupBtnRect = setupBtnObj.AddComponent<RectTransform>();
            setupBtnRect.anchorMin = new Vector2(1f, 1f);
            setupBtnRect.anchorMax = new Vector2(1f, 1f);
            setupBtnRect.pivot = new Vector2(1f, 1f);
            setupBtnRect.anchoredPosition = new Vector2(-20, -20);
            setupBtnRect.sizeDelta = new Vector2(140, 54);

            Image setupBtnImg = setupBtnObj.AddComponent<Image>();
            setupBtnImg.sprite = pillSprite;
            setupBtnImg.type = Image.Type.Sliced;
            setupBtnImg.color = new Color(0.25f, 0.35f, 0.50f, 0.95f);

            Button setupBtn = setupBtnObj.AddComponent<Button>();
            setupBtn.onClick.AddListener(ShowPlacementSetupDialog);

            GameObject setupTxtObj = new GameObject("Label");
            setupTxtObj.transform.SetParent(setupBtnObj.transform, false);
            RectTransform setupTxtRect = setupTxtObj.AddComponent<RectTransform>();
            setupTxtRect.anchorMin = Vector2.zero;
            setupTxtRect.anchorMax = Vector2.one;
            setupTxtRect.sizeDelta = Vector2.zero;

            Text setupTxt = setupTxtObj.AddComponent<Text>();
            setupTxt.font = hangulFont;
            setupTxt.text = "⚙️ 차림";
            setupTxt.fontSize = 22;
            setupTxt.fontStyle = FontStyle.Bold;
            setupTxt.alignment = TextAnchor.MiddleCenter;
            setupTxt.color = Color.white;

            // 2. Bottom Floating Action Pill Bar (하단 6종 알약 버튼)
            GameObject bottomBar = new GameObject("BottomNav");
            bottomBar.transform.SetParent(canvasObj.transform, false);
            RectTransform botRect = bottomBar.AddComponent<RectTransform>();
            botRect.anchorMin = new Vector2(0.5f, 0f);
            botRect.anchorMax = new Vector2(0.5f, 0f);
            botRect.pivot = new Vector2(0.5f, 0f);
            botRect.anchoredPosition = new Vector2(0, 60);
            botRect.sizeDelta = new Vector2(1040, 100);

            HorizontalLayoutGroup layoutGroup = bottomBar.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.spacing = 10f;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            float btnWidth = 158f;
            float btnHeight = 84f;

            CreateMaterialPillButton(bottomBar.transform, "⚙️ 차림", Vector2.zero, new Vector2(btnWidth, btnHeight), hangulFont, pillSprite, new Color(0.25f, 0.35f, 0.50f, 0.95f), ShowPlacementSetupDialog);
            CreateMaterialPillButton(bottomBar.transform, "⏸️ 한수 쉼", Vector2.zero, new Vector2(btnWidth, btnHeight), hangulFont, pillSprite, new Color(0.20f, 0.25f, 0.35f, 0.95f), OnPassClicked);
            CreateMaterialPillButton(bottomBar.transform, "↩️ 물르기", Vector2.zero, new Vector2(btnWidth, btnHeight), hangulFont, pillSprite, new Color(0.20f, 0.25f, 0.35f, 0.95f), OnUndoClicked);
            CreateMaterialPillButton(bottomBar.transform, "⟲ 시점 리셋", Vector2.zero, new Vector2(btnWidth, btnHeight), hangulFont, pillSprite, new Color(0.20f, 0.25f, 0.35f, 0.95f), OnResetViewClicked);
            CreateMaterialPillButton(bottomBar.transform, "🏳️ 기권", Vector2.zero, new Vector2(btnWidth, btnHeight), hangulFont, pillSprite, new Color(0.42f, 0.15f, 0.18f, 0.95f), OnResignClicked);
            CreateMaterialPillButton(bottomBar.transform, "🚪 종료", Vector2.zero, new Vector2(btnWidth, btnHeight), hangulFont, pillSprite, new Color(0.30f, 0.30f, 0.35f, 0.95f), OnExitClicked);
        }

        public void ShowPlacementSetupDialog()
        {
            GameObject canvasObj = GameObject.Find("MobileJanggiCanvas");
            if (canvasObj == null) return;

            // 이미 열려 있는 팝업이 있다면 중복 생성 방지
            if (GameObject.Find("PlacementSetupDialog") != null) return;

            Font font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 28) 
                     ?? Font.CreateDynamicFontFromOSFont("NanumGothic", 28) 
                     ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Sprite pillSprite = GetRoundedSprite(48);

            GameObject dialogOverlay = new GameObject("PlacementSetupDialog");
            dialogOverlay.transform.SetParent(canvasObj.transform, false);

            RectTransform overlayRect = dialogOverlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            Image darkBg = dialogOverlay.AddComponent<Image>();
            darkBg.color = new Color(0f, 0f, 0f, 0.75f);

            GameObject card = new GameObject("SetupCard");
            card.transform.SetParent(dialogOverlay.transform, false);

            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(860, 1140);

            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = pillSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            // Header Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(card.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 500);
            titleRect.sizeDelta = new Vector2(800, 70);

            Text titleTxt = titleObj.AddComponent<Text>();
            titleTxt.font = font;
            titleTxt.text = "⚔️ 장기 차림 설정 ⚔️";
            titleTxt.fontSize = 42;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(1.0f, 0.85f, 0.35f);

            // Subtitle
            GameObject subTitleObj = new GameObject("SubTitleText");
            subTitleObj.transform.SetParent(card.transform, false);
            RectTransform subTitleRect = subTitleObj.AddComponent<RectTransform>();
            subTitleRect.anchoredPosition = new Vector2(0, 440);
            subTitleRect.sizeDelta = new Vector2(800, 50);

            Text subTitleTxt = subTitleObj.AddComponent<Text>();
            subTitleTxt.font = font;
            subTitleTxt.text = "초(楚)·한(漢)의 마상 차림과 AI 수준을 선택하세요";
            subTitleTxt.fontSize = 26;
            subTitleTxt.alignment = TextAnchor.MiddleCenter;
            subTitleTxt.color = new Color(0.8f, 0.9f, 1.0f);

            // Section 1: Cho (Player) Placement Selection
            CreateTeamPlacementSection(card.transform, "🔵 초(楚) 차림 (Player)", 330, Team.Cho, font, pillSprite);

            // Section 2: Han (AI) Placement Selection
            CreateTeamPlacementSection(card.transform, "🔴 한(漢) 차림 (AI)", 80, Team.Han, font, pillSprite);

            // Section 3: AI Level Selection
            CreateAiLevelSection(card.transform, -170, font, pillSprite);

            // Start / Apply Button
            GameObject startBtnObj = new GameObject("Btn_StartGame");
            startBtnObj.transform.SetParent(card.transform, false);
            RectTransform startRect = startBtnObj.AddComponent<RectTransform>();
            startRect.anchoredPosition = new Vector2(0, -450);
            startRect.sizeDelta = new Vector2(460, 96);

            Image startImg = startBtnObj.AddComponent<Image>();
            startImg.sprite = pillSprite;
            startImg.type = Image.Type.Sliced;
            startImg.color = new Color(0.20f, 0.65f, 0.35f, 1f);

            Button startBtn = startBtnObj.AddComponent<Button>();
            startBtn.onClick.AddListener(() =>
            {
                // PlayerPrefs에 차림 설정 저장 (다음에 앱을 켜도 유지)
                PlayerPrefs.SetInt(KEY_CHO_PLACEMENT, (int)choSelectedPlacement);
                PlayerPrefs.SetInt(KEY_HAN_PLACEMENT, (int)hanSelectedPlacement);
                PlayerPrefs.SetInt(GameManager.KeyAiLevel, aiSelectedLevel);
                PlayerPrefs.Save();

                Destroy(dialogOverlay);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.aiLevel = aiSelectedLevel;
                    GameManager.Instance.StartGame(choSelectedPlacement, hanSelectedPlacement);
                    UpdateTurnDisplay(GameManager.Instance.CurrentTurn);
                }
                if (BoardView.Instance != null)
                {
                    BoardView.Instance.RedrawBoard();
                }
            });

            GameObject startTxtObj = new GameObject("Label");
            startTxtObj.transform.SetParent(startBtnObj.transform, false);
            RectTransform startTxtRect = startTxtObj.AddComponent<RectTransform>();
            startTxtRect.anchorMin = Vector2.zero;
            startTxtRect.anchorMax = Vector2.one;
            startTxtRect.sizeDelta = Vector2.zero;

            Text startTxt = startTxtObj.AddComponent<Text>();
            startTxt.font = font;
            startTxt.text = "🎮 대국 시작 🎮";
            startTxt.fontSize = 36;
            startTxt.fontStyle = FontStyle.Bold;
            startTxt.alignment = TextAnchor.MiddleCenter;
            startTxt.color = Color.white;

            // 오픈소스 고지 (APK에 GPL v3 엔진이 포함되므로 라이선스와 소스 위치를 앱 안에 표시)
            GameObject noticeObj = new GameObject("OpenSourceNotice");
            noticeObj.transform.SetParent(card.transform, false);
            RectTransform noticeRect = noticeObj.AddComponent<RectTransform>();
            noticeRect.anchoredPosition = new Vector2(0, -535);
            noticeRect.sizeDelta = new Vector2(800, 60);

            Text noticeTxt = noticeObj.AddComponent<Text>();
            noticeTxt.font = font;
            noticeTxt.text = "AI 엔진: Fairy-Stockfish (GNU GPL v3) · github.com/fairy-stockfish/Fairy-Stockfish\n기물 글씨: Yuji Boku·Yuji Syuku (SIL Open Font License 1.1)";
            noticeTxt.fontSize = 17;
            noticeTxt.alignment = TextAnchor.MiddleCenter;
            noticeTxt.color = new Color(0.65f, 0.70f, 0.78f);
        }

        private void CreateTeamPlacementSection(Transform parent, string title, float startY, Team team, Font font, Sprite pillSprite)
        {
            GameObject titleObj = new GameObject($"SectionTitle_{team}");
            titleObj.transform.SetParent(parent, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, startY);
            titleRect.sizeDelta = new Vector2(780, 50);

            Text titleTxt = titleObj.AddComponent<Text>();
            titleTxt.font = font;
            titleTxt.text = title;
            titleTxt.fontSize = 30;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.color = (team == Team.Cho) ? new Color(0.4f, 0.75f, 1.0f) : new Color(1.0f, 0.45f, 0.45f);

            PlacementType[] options = new PlacementType[]
            {
                PlacementType.MaSangMaSang,
                PlacementType.SangMaSangMa,
                PlacementType.MaSangSangMa,
                PlacementType.SangMaMaSang
            };

            string[] optionNames = new string[]
            {
                "마상마상\n(마 상 마 상)",
                "상마상마\n(상 마 상 마)",
                "외상마\n(마 상 상 마)",
                "양귀마\n(상 마 마 상)"
            };

            List<Image> btnImages = new List<Image>();

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                PlacementType placement = options[i];

                GameObject btnObj = new GameObject($"PlacementBtn_{team}_{placement}");
                btnObj.transform.SetParent(parent, false);

                RectTransform rect = btnObj.AddComponent<RectTransform>();
                float posX = -285f + (index % 4) * 190f;
                rect.anchoredPosition = new Vector2(posX, startY - 90f);
                rect.sizeDelta = new Vector2(175f, 100f);

                Image img = btnObj.AddComponent<Image>();
                img.sprite = pillSprite;
                img.type = Image.Type.Sliced;
                
                PlacementType defaultType = (team == Team.Cho) ? choSelectedPlacement : hanSelectedPlacement;
                img.color = (placement == defaultType) ? new Color(0.25f, 0.55f, 0.95f) : new Color(0.20f, 0.24f, 0.32f);
                btnImages.Add(img);

                Button btn = btnObj.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (team == Team.Cho) choSelectedPlacement = placement;
                    else hanSelectedPlacement = placement;

                    for (int k = 0; k < btnImages.Count; k++)
                    {
                        btnImages[k].color = (options[k] == placement) ? new Color(0.25f, 0.55f, 0.95f) : new Color(0.20f, 0.24f, 0.32f);
                    }
                });

                GameObject txtObj = new GameObject("Label");
                txtObj.transform.SetParent(btnObj.transform, false);
                RectTransform txtRect = txtObj.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;

                Text txt = txtObj.AddComponent<Text>();
                txt.font = font;
                txt.text = optionNames[i];
                txt.fontSize = 22;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
            }
        }

        private void CreateAiLevelSection(Transform parent, float startY, Font font, Sprite pillSprite)
        {
            GameObject titleObj = new GameObject("SectionTitle_AiLevel");
            titleObj.transform.SetParent(parent, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, startY);
            titleRect.sizeDelta = new Vector2(780, 50);

            Text titleTxt = titleObj.AddComponent<Text>();
            titleTxt.font = font;
            titleTxt.text = "🤖 AI 수준 (1단 ~ 9단)";
            titleTxt.fontSize = 30;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.color = new Color(1.0f, 0.85f, 0.35f);

            List<Image> btnImages = new List<Image>();

            for (int level = GameManager.MinAiLevel; level <= GameManager.MaxAiLevel; level++)
            {
                int lv = level;

                GameObject btnObj = new GameObject($"AiLevelBtn_{lv}");
                btnObj.transform.SetParent(parent, false);

                RectTransform rect = btnObj.AddComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-344f + (lv - 1) * 86f, startY - 85f);
                rect.sizeDelta = new Vector2(80f, 90f);

                Image img = btnObj.AddComponent<Image>();
                img.sprite = pillSprite;
                img.type = Image.Type.Sliced;
                img.color = (lv == aiSelectedLevel) ? new Color(0.25f, 0.55f, 0.95f) : new Color(0.20f, 0.24f, 0.32f);
                btnImages.Add(img);

                Button btn = btnObj.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    aiSelectedLevel = lv;
                    for (int k = 0; k < btnImages.Count; k++)
                    {
                        btnImages[k].color = (k + GameManager.MinAiLevel == lv) ? new Color(0.25f, 0.55f, 0.95f) : new Color(0.20f, 0.24f, 0.32f);
                    }
                });

                GameObject txtObj = new GameObject("Label");
                txtObj.transform.SetParent(btnObj.transform, false);
                RectTransform txtRect = txtObj.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;

                Text txt = txtObj.AddComponent<Text>();
                txt.font = font;
                txt.text = $"{lv}단";
                txt.fontSize = 26;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
            }
        }

        private void CreateMaterialPillButton(Transform parent, string label, Vector2 pos, Vector2 size, Font font, Sprite pillSprite, Color bgColor, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.sprite = pillSprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.text = label;
            txt.fontSize = 30;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 18;
            txt.resizeTextMaxSize = 32;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

        private void CreateMaterialProfileCard(Transform parent, string name, Vector2 pos, Vector2 anchor, Font font, Color badgeColor)
        {
            GameObject profile = new GameObject(name);
            profile.transform.SetParent(parent, false);

            RectTransform rect = profile.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(260, 80);

            Text txt = profile.AddComponent<Text>();
            txt.font = font;
            txt.text = name;
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white;
        }

        private void CreateMaterialButton(Transform parent, string label, Vector2 pos, Vector2 size, Font font, Color bgColor, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.text = label;
            txt.fontSize = 25;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

        private void OnPassClicked()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsHumanTurn)
            {
                GameManager.Instance.PassTurn();
                UpdateTurnDisplay(GameManager.Instance.CurrentTurn);
            }
        }

        private void OnUndoClicked()
        {
            if (GameManager.Instance != null)
            {
                bool success = GameManager.Instance.UndoMove();
                if (success)
                {
                    Debug.Log("물르기 완료");
                }
            }
        }

        private void OnResetViewClicked()
        {
            CameraUIController camCtrl = FindFirstObjectByType<CameraUIController>();
            if (camCtrl != null) camCtrl.ResetView();
        }

        private void OnResignClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Resign();
            }
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ShowGameOverDialog(string resultMsg, bool isPlayerWin)
        {
            GameObject canvasObj = GameObject.Find("MobileJanggiCanvas");
            if (canvasObj == null) return;

            GameObject dialog = new GameObject("GameOverDialog");
            dialog.transform.SetParent(canvasObj.transform, false);

            RectTransform rect = dialog.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(640, 380);

            // Material Design 3 Elevated Dialog Card
            Image img = dialog.AddComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);

            GameObject textObj = new GameObject("ResultText");
            textObj.transform.SetParent(dialog.transform, false);
            RectTransform txtRect = textObj.AddComponent<RectTransform>();
            txtRect.anchoredPosition = new Vector2(0, 45);
            txtRect.sizeDelta = new Vector2(580, 160);

            Text txt = textObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 34;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(1.0f, 0.85f, 0.35f);

            if (isPlayerWin)
            {
                RecordWin();
                txt.text = "🏆 승리를 축하합니다! 🏆";
                if (JanggiSoundManager.Instance != null) JanggiSoundManager.Instance.PlayPlayerWinVoice();
                StartCoroutine(PlayerWinSequence(txt));
            }
            else
            {
                RecordLoss();
                txt.text = $"😜 {resultMsg} 😜\n\n장기두는 사람 어디갔나~?";
                if (JanggiSoundManager.Instance != null) JanggiSoundManager.Instance.PlayPlayerLoseTauntVoice();
                StartCoroutine(PlayerLoseTauntSequence(txt, dialog));
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateMaterialButton(dialog.transform, "새 대국 시작", new Vector2(0, -95), new Vector2(280, 72), font, new Color(0.25f, 0.50f, 0.90f), () => {
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            });
        }

        private IEnumerator PlayerWinSequence(Text txt)
        {
            // 1. "승리를 축하합니다." 표시 후 5초 대기
            yield return new WaitForSeconds(5.0f);

            // 2. 5초 뒤 분노 문구 변경
            if (txt != null)
            {
                txt.text = "💢 에이 씨 기분더럽네 💢";
                txt.color = new Color(1.0f, 0.25f, 0.25f);
            }

            if (JanggiSoundManager.Instance != null) JanggiSoundManager.Instance.PlayFlipRageSound();

            // 3. 다양한 분노 연출 무작위 조합 실행!
            int randomRageType = UnityEngine.Random.Range(0, 4);

            // [공통 연출 1] 화면 핏빛 붉은 플래시 번쩍임
            StartCoroutine(FlashRedScreenOverlay());

            // [공통 연출 2] 쾅! 책상 치는 지진/카메라 진동
            CameraUIController camCtrl = FindFirstObjectByType<CameraUIController>();
            if (camCtrl != null)
            {
                camCtrl.ShakeCamera(0.8f, 1.2f);
            }

            switch (randomRageType)
            {
                case 0:
                    // 연출 A: 물리 장기판 수직 뒤엎기 + 3D 말 폭발
                    if (BoardView.Instance != null) BoardView.Instance.FlipBoard();
                    break;

                case 1:
                    // 연출 B: AI 한(漢) 궁 기물 멘탈 분쇄 폭발 + 억울한 변명 팝업
                    if (BoardView.Instance != null) BoardView.Instance.ExplodeHanKing();
                    yield return new WaitForSeconds(0.8f);
                    if (txt != null) txt.text = "🤬 내가 렉만 안 걸렸어도 이기는 건데!!";
                    break;

                case 2:
                    // 연출 C: 억울한 패배 변명 연속 분출 + 장기판 뒤엎기
                    if (txt != null) txt.text = "🗯️ 장기판 상판 밸런스가 안 맞아!!";
                    yield return new WaitForSeconds(1.2f);
                    if (txt != null) txt.text = "💢 에이 몰라! 다시 해 다시!! 💥";
                    if (BoardView.Instance != null) BoardView.Instance.FlipBoard();
                    break;

                case 3:
                default:
                    // 연출 D: 종합 선물 세트 (궁 파편 폭발 + 장기판 뒤엎기)
                    if (BoardView.Instance != null)
                    {
                        BoardView.Instance.ExplodeHanKing();
                        yield return new WaitForSeconds(0.4f);
                        BoardView.Instance.FlipBoard();
                    }
                    break;
            }
        }

        private IEnumerator FlashRedScreenOverlay()
        {
            GameObject canvasObj = GameObject.Find("MobileJanggiCanvas");
            if (canvasObj == null) yield break;

            GameObject overlay = new GameObject("RedRageOverlay");
            overlay.transform.SetParent(canvasObj.transform, false);

            RectTransform rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image img = overlay.AddComponent<Image>();
            img.color = new Color(1.0f, 0.0f, 0.0f, 0.45f);

            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                if (img != null)
                {
                    img.color = new Color(1.0f, 0.0f, 0.0f, Mathf.Lerp(0.45f, 0.0f, t / 0.6f));
                }
                yield return null;
            }

            if (overlay != null) Destroy(overlay);
        }

        private IEnumerator PlayerLoseTauntSequence(Text txt, GameObject dialog)
        {
            // AI 승리 빵빠레 & 불꽃놀이 축포 3D/2N 효과 즉시 발사!
            StartCoroutine(SpawnFireworksEffect());

            string[] tauntMessages = new string[]
            {
                "😜 AI 한(漢)의 승리! 😜\n\n장기두는 사람 어디갔나~?",
                "🤣 낄낄낄~ 수가 너무 훤히 보이는구먼!",
                "💡 훈수라도 좀 받아서 두고 오시게~ ㅋㅋㅋ",
                "⚡ 한 판 더 해서 덤벼보시게! 낄낄낄!",
                "🎺 빵빠레~ AI의 압도적 대승리!! 🎉",
                "🥱 장기판 앞에서 졸고 계셨나보오~ ㅋㅋㅋ",
                "🥇AI 전승 신화! 장기의 신은 나다!",
                "💥 터졌다 축포! 빵~ 빵~ 🎆"
            };

            RectTransform dialogRect = dialog != null ? dialog.GetComponent<RectTransform>() : null;
            Vector2 basePos = dialogRect != null ? dialogRect.anchoredPosition : Vector2.zero;

            int stepCount = 0;
            while (dialog != null && txt != null)
            {
                yield return new WaitForSeconds(2.0f);
                stepCount++;

                // 무작위 약올림 멘트 변경
                int randMsgIdx = UnityEngine.Random.Range(0, tauntMessages.Length);
                if (txt != null)
                {
                    txt.text = tauntMessages[randMsgIdx];
                }

                // 무작위 시각 연출 효과 선택 (0: 팝업 쉐이크, 1: 불꽃 축포 2차 발사, 2: 황금빛 빵빠레, 3: 카메라 지진)
                int randomEffect = UnityEngine.Random.Range(0, 4);

                switch (randomEffect)
                {
                    case 0:
                        if (dialogRect != null)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                dialogRect.anchoredPosition = basePos + new Vector2(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(-12f, 12f));
                                yield return new WaitForSeconds(0.035f);
                            }
                            dialogRect.anchoredPosition = basePos;
                        }
                        break;

                    case 1:
                        StartCoroutine(SpawnFireworksEffect());
                        break;

                    case 2:
                        StartCoroutine(FlashGoldScreenOverlay());
                        break;

                    case 3:
                    default:
                        CameraUIController camCtrl = FindFirstObjectByType<CameraUIController>();
                        if (camCtrl != null)
                        {
                            camCtrl.ShakeCamera(0.6f, 0.9f);
                        }
                        break;
                }
            }
        }

        public void ShowJangiyoBanner(string msg)
        {
            if (JanggiSoundManager.Instance != null) JanggiSoundManager.Instance.PlayJangiyoVoice();

            GameObject canvasObj = GameObject.Find("MobileJanggiCanvas");
            if (canvasObj == null) return;

            GameObject bannerObj = new GameObject("JangiyoBanner");
            bannerObj.transform.SetParent(canvasObj.transform, false);

            RectTransform rect = bannerObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.65f);
            rect.anchorMax = new Vector2(0.5f, 0.65f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600, 110);

            Image bg = bannerObj.AddComponent<Image>();
            bg.sprite = GetRoundedSprite(48);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.85f, 0.15f, 0.15f, 0.95f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(bannerObj.transform, false);
            RectTransform txtRect = textObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            Text txt = textObj.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 36) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = msg;
            txt.fontSize = 36;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.yellow;

            StartCoroutine(AnimateBanner(bannerObj));
        }

        private IEnumerator AnimateBanner(GameObject bannerObj)
        {
            Vector3 startScale = new Vector3(0.2f, 0.2f, 0.2f);
            Vector3 endScale = Vector3.one;
            float time = 0f;

            while (time < 0.25f)
            {
                time += Time.deltaTime;
                float t = time / 0.25f;
                if (bannerObj != null)
                {
                    bannerObj.transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Sin(t * Mathf.PI * 0.5f));
                }
                yield return null;
            }

            yield return new WaitForSeconds(1.2f);

            time = 0f;
            while (time < 0.25f)
            {
                time += Time.deltaTime;
                float t = time / 0.25f;
                if (bannerObj != null)
                {
                    bannerObj.transform.localScale = Vector3.Lerp(endScale, Vector3.zero, t);
                }
                yield return null;
            }

            if (bannerObj != null) Destroy(bannerObj);
        }

        private IEnumerator SpawnFireworksEffect()
        {
            GameObject canvasObj = GameObject.Find("MobileJanggiCanvas");
            if (canvasObj == null) yield break;

            Color[] colors = new Color[]
            {
                new Color(1f, 0.2f, 0.2f),
                new Color(1f, 0.85f, 0.2f),
                new Color(0.2f, 0.8f, 1f),
                new Color(0.3f, 1f, 0.4f),
                new Color(0.9f, 0.3f, 1f)
            };

            for (int f = 0; f < 3; f++)
            {
                Vector2 centerPos = new Vector2(UnityEngine.Random.Range(-350f, 350f), UnityEngine.Random.Range(100f, 600f));

                for (int i = 0; i < 18; i++)
                {
                    GameObject spark = new GameObject("FireworksSpark");
                    spark.transform.SetParent(canvasObj.transform, false);

                    RectTransform rect = spark.AddComponent<RectTransform>();
                    rect.anchoredPosition = centerPos;
                    rect.sizeDelta = new Vector2(UnityEngine.Random.Range(20f, 35f), UnityEngine.Random.Range(20f, 35f));

                    Image img = spark.AddComponent<Image>();
                    img.sprite = GetRoundedSprite(30);
                    img.color = colors[UnityEngine.Random.Range(0, colors.Length)];

                    float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float speed = UnityEngine.Random.Range(250f, 550f);
                    Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

                    StartCoroutine(AnimateSparkle(spark, rect, img, vel));
                }
                yield return new WaitForSeconds(0.15f);
            }
        }

        private IEnumerator AnimateSparkle(GameObject spark, RectTransform rect, Image img, Vector2 velocity)
        {
            float duration = UnityEngine.Random.Range(0.8f, 1.4f);
            float time = 0f;
            Vector2 pos = rect.anchoredPosition;

            while (time < duration)
            {
                time += Time.deltaTime;
                pos += velocity * Time.deltaTime;
                velocity.y -= 300f * Time.deltaTime;

                if (rect != null) rect.anchoredPosition = pos;
                if (img != null)
                {
                    Color c = img.color;
                    c.a = Mathf.Lerp(1f, 0f, time / duration);
                    img.color = c;
                }
                yield return null;
            }

            if (spark != null) Destroy(spark);
        }

        private IEnumerator FlashGoldScreenOverlay()
        {
            GameObject canvasObj = GameObject.Find("MobileJanggiCanvas");
            if (canvasObj == null) yield break;

            GameObject overlay = new GameObject("GoldVictoryOverlay");
            overlay.transform.SetParent(canvasObj.transform, false);

            RectTransform rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image img = overlay.AddComponent<Image>();
            img.color = new Color(1.0f, 0.85f, 0.2f, 0.40f);

            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                if (img != null)
                {
                    img.color = new Color(1.0f, 0.85f, 0.2f, Mathf.Lerp(0.40f, 0.0f, t / 0.5f));
                }
                yield return null;
            }

            if (overlay != null) Destroy(overlay);
        }
    }
}
