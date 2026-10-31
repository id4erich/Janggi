using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Janggi
{
    public class MobileJanggiUIManager : MonoBehaviour
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

        private void Start()
        {
            choSelectedPlacement = (PlacementType)PlayerPrefs.GetInt(KEY_CHO_PLACEMENT, (int)PlacementType.MaSangMaSang);
            hanSelectedPlacement = (PlacementType)PlayerPrefs.GetInt(KEY_HAN_PLACEMENT, (int)PlacementType.MaSangMaSang);
            if (GameManager.Instance != null)
            {
                aiSelectedLevel = GameManager.Instance.aiLevel;
                timeLimitSelected = GameManager.Instance.moveTimeLimitSec;
                modeSelected = GameManager.Instance.Mode;
            }

            BuildMobileUI();
        }

        private void Update()
        {
            // 뒤로 가기(안드로이드)·ESC: 열린 창이 있으면 닫고, 없으면 메뉴를 엶
#if ENABLE_INPUT_SYSTEM
            bool back = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            bool back = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (back)
            {
                if (activeOverlay != null) CloseOverlay();
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

            if (isTimerRunning && humanTurn && timerText != null)
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
                    Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView - delta * 0.05f, 30f, 75f);
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

            CreateToolbarButton(toolbar.transform, "undo", "무르기", OnUndoClicked);
            passButton = CreateToolbarButton(toolbar.transform, "pass", "한수 쉼", OnPassClicked);
            hintButton = CreateToolbarButton(toolbar.transform, "hint", "힌트", OnHintClicked);
            CreateToolbarButton(toolbar.transform, "view", "시점", OnResetViewClicked);
            CreateToolbarButton(toolbar.transform, "menu", "메뉴", ShowMenuSheet);

            if (GameManager.Instance != null) UpdateTurnDisplay(GameManager.Instance.CurrentTurn);
        }

        // ── 대국자 패널·도구 막대·메뉴 ─────────────────────────────

        private static readonly Color SurfaceColor = new Color(0.11f, 0.12f, 0.15f, 1f); // 불투명: 뒤의 금색 테두리가 비치지 않게
        private static readonly Color AccentGold = new Color(0.96f, 0.77f, 0.32f);
        private static readonly Color TextSubColor = new Color(0.72f, 0.76f, 0.84f);
        private static readonly Color HanColor = new Color(0.90f, 0.32f, 0.28f);
        private static readonly Color ChoColor = new Color(0.40f, 0.62f, 1.00f);

        private class PlayerPanel
        {
            public RectTransform Root;
            public Image Outline;
            public Text Name;
            public Text Status;
            public Team Team;
        }

        private PlayerPanel hanPanel, choPanel;
        private Button passButton, hintButton;
        private Font uiFont;
        private Transform uiCanvas;
        private GameObject activeOverlay; // 메뉴 시트·확인 창 (뒤로 가기 키로 닫음)

        private PlayerPanel CreatePlayerPanel(Transform parent, Team team, bool top)
        {
            var panel = new PlayerPanel { Team = team };
            GameObject root = new GameObject(top ? "OpponentPanel" : "MyPanel");
            root.transform.SetParent(parent, false);
            panel.Root = root.AddComponent<RectTransform>();
            panel.Root.anchorMin = new Vector2(0f, top ? 1f : 0f);
            panel.Root.anchorMax = new Vector2(1f, top ? 1f : 0f);
            panel.Root.pivot = new Vector2(0.5f, top ? 1f : 0f);
            panel.Root.anchoredPosition = new Vector2(0f, top ? -70f : 214f);
            panel.Root.sizeDelta = new Vector2(-60f, 124f);

            Sprite rounded = GetRoundedSprite(48);
            // 차례 표시용 금색 테두리 (배경보다 살짝 크게, 배경 뒤에)
            GameObject outline = new GameObject("TurnOutline");
            outline.transform.SetParent(root.transform, false);
            panel.Outline = outline.AddComponent<Image>();
            panel.Outline.sprite = rounded;
            panel.Outline.type = Image.Type.Sliced;
            panel.Outline.color = AccentGold;
            panel.Outline.raycastTarget = false;
            Stretch(panel.Outline.rectTransform, 5f);

            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.sprite = rounded;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = SurfaceColor;
            bgImg.raycastTarget = false;
            Stretch(bgImg.rectTransform, 0f);

            // 궁 기물 얼굴을 둥글게 잘라 프로필로 사용
            GameObject avatarMask = new GameObject("Avatar");
            avatarMask.transform.SetParent(root.transform, false);
            RectTransform maskRect = avatarMask.AddComponent<RectTransform>();
            maskRect.anchorMin = maskRect.anchorMax = maskRect.pivot = new Vector2(0f, 0.5f);
            maskRect.anchoredPosition = new Vector2(20f, 0f);
            maskRect.sizeDelta = new Vector2(92f, 92f);
            Image maskImg = avatarMask.AddComponent<Image>();
            maskImg.sprite = rounded;
            maskImg.type = Image.Type.Sliced;
            maskImg.raycastTarget = false;
            avatarMask.AddComponent<Mask>().showMaskGraphic = false;
            GameObject face = new GameObject("Face");
            face.transform.SetParent(avatarMask.transform, false);
            RawImage faceImg = face.AddComponent<RawImage>();
            faceImg.texture = Resources.Load<Texture2D>($"PieceArt/{team}_King");
            faceImg.raycastTarget = false;
            Stretch(faceImg.rectTransform, 0f);

            panel.Name = CreatePanelText(root.transform, "Name", new Vector2(132f, 20f), new Vector2(460f, 46f), 34, TextAnchor.MiddleLeft, team == Team.Han ? HanColor : ChoColor);
            panel.Name.fontStyle = FontStyle.Bold;
            panel.Status = CreatePanelText(root.transform, "Status", new Vector2(132f, -24f), new Vector2(460f, 38f), 25, TextAnchor.MiddleLeft, TextSubColor);
            foreach (var t in new[] { panel.Name, panel.Status })
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0f, 0.5f);
            return panel;
        }

        private Text CreatePanelText(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Text txt = obj.AddComponent<Text>();
            txt.rectTransform.anchoredPosition = pos;
            txt.rectTransform.sizeDelta = size;
            txt.font = uiFont;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private static void Stretch(RectTransform rect, float grow)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = new Vector2(grow * 2f, grow * 2f);
            rect.anchoredPosition = Vector2.zero;
        }

        private Button CreateToolbarButton(Transform parent, string icon, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject($"Btn_{icon}");
            btnObj.transform.SetParent(parent, false);
            Image bg = btnObj.AddComponent<Image>();
            bg.sprite = GetRoundedSprite(48);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.06f);
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock colors = btn.colors;
            colors.pressedColor = new Color(1f, 1f, 1f, 3f); // 누르면 배경이 밝아짐 (tint 배율)
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            btn.colors = colors;
            btn.onClick.AddListener(action);

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);
            RawImage iconImg = iconObj.AddComponent<RawImage>();
            iconImg.texture = Resources.Load<Texture2D>($"UIIcons/{icon}");
            iconImg.raycastTarget = false;
            RectTransform ir = iconImg.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f);
            ir.pivot = new Vector2(0.5f, 1f);
            ir.anchoredPosition = new Vector2(0f, -14f);
            ir.sizeDelta = new Vector2(64f, 64f);

            Text txt = CreatePanelText(btnObj.transform, "Label", Vector2.zero, new Vector2(0f, 40f), 26, TextAnchor.MiddleCenter, Color.white);
            RectTransform tr = txt.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.anchoredPosition = new Vector2(0f, 12f);
            txt.text = label;
            return btn;
        }

        private GameObject CreateOverlay(string name)
        {
            CloseOverlay();
            GameObject overlay = new GameObject(name);
            overlay.transform.SetParent(uiCanvas, false);
            Image dim = overlay.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            Stretch(dim.rectTransform, 0f);
            overlay.AddComponent<Button>().onClick.AddListener(CloseOverlay); // 바깥을 누르면 닫힘
            activeOverlay = overlay;
            return overlay;
        }

        private void CloseOverlay()
        {
            if (activeOverlay != null) Destroy(activeOverlay);
            activeOverlay = null;
        }

        private RectTransform CreateCard(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            GameObject card = new GameObject("Card");
            card.transform.SetParent(parent, false);
            Image img = card.AddComponent<Image>();
            img.sprite = GetRoundedSprite(48);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.14f, 0.15f, 0.19f, 0.99f);
            card.AddComponent<Button>().transition = Selectable.Transition.None; // 카드 안을 눌러도 닫히지 않게 클릭을 막음
            RectTransform rect = img.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private void CreateSheetRow(Transform parent, float y, string icon, string label, Color color, UnityEngine.Events.UnityAction action)
        {
            GameObject row = new GameObject($"Row_{icon}");
            row.transform.SetParent(parent, false);
            Image bg = row.AddComponent<Image>();
            bg.sprite = GetRoundedSprite(48);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            RectTransform rect = bg.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-48f, 112f);
            Button btn = row.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(action);

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(row.transform, false);
            RawImage iconImg = iconObj.AddComponent<RawImage>();
            iconImg.texture = Resources.Load<Texture2D>($"UIIcons/{icon}");
            iconImg.color = color;
            iconImg.raycastTarget = false;
            RectTransform ir = iconImg.rectTransform;
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = new Vector2(32f, 0f);
            ir.sizeDelta = new Vector2(56f, 56f);

            Text txt = CreatePanelText(row.transform, "Label", new Vector2(116f, 0f), new Vector2(600f, 60f), 32, TextAnchor.MiddleLeft, color);
            txt.rectTransform.anchorMin = txt.rectTransform.anchorMax = txt.rectTransform.pivot = new Vector2(0f, 0.5f);
            txt.text = label;
        }

        private void ShowMenuSheet()
        {
            GameObject overlay = CreateOverlay("MenuSheet");
            RectTransform sheet = CreateCard(overlay.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 768f));
            // 화면 폭에 맞춰 늘어나게 (캔버스 폭은 기기 비율에 따라 1080보다 좁을 수 있음)
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.sizeDelta = new Vector2(-32f, 768f);

            Text title = CreatePanelText(sheet, "Title", new Vector2(0f, -24f), new Vector2(900f, 60f), 34, TextAnchor.MiddleCenter, Color.white);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.fontStyle = FontStyle.Bold;
            title.text = "메뉴";

            CreateSheetRow(sheet, -100f, "new_game", "새 대국 · 차림과 AI 수준", Color.white, () => { CloseOverlay(); ShowPlacementSetupDialog(); });
            CreateSheetRow(sheet, -224f, "record", "기보 · 둔 수 목록", Color.white, ShowMoveRecordSheet);
            CreateSheetRow(sheet, -348f, "settings", "설정 · 효과음과 음성", Color.white, ShowSettingsSheet);
            CreateSheetRow(sheet, -472f, "flag", "기권", HanColor, () => ShowConfirm("기권할까요?", "이번 대국은 패배로 기록됩니다.", "기권", OnResignClicked));
            CreateSheetRow(sheet, -596f, "exit", "나가기", TextSubColor, () => ShowConfirm("앱을 종료할까요?", "진행 중인 대국은 저장되지 않습니다.", "나가기", OnExitClicked));
        }

        private void ShowMoveRecordSheet()
        {
            List<string> lines = GameManager.Instance != null ? GameManager.Instance.GetMoveRecordLines() : new List<string>();
            GameObject overlay = CreateOverlay("MoveRecordSheet");
            RectTransform sheet = CreateCard(overlay.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 1100f));
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.sizeDelta = new Vector2(-32f, 1100f);

            Text title = CreatePanelText(sheet, "Title", new Vector2(0f, -24f), new Vector2(900f, 60f), 34, TextAnchor.MiddleCenter, Color.white);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.fontStyle = FontStyle.Bold;
            title.text = $"기보 ({lines.Count}수)";

            Text hint = CreatePanelText(sheet, "Legend", new Vector2(0f, -78f), new Vector2(900f, 36f), 22, TextAnchor.MiddleCenter, TextSubColor);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(0.5f, 1f);
            hint.text = "좌표: 초(楚) 쪽 왼쪽 아래가 1,1 (가로,세로) · ×는 잡은 말";

            // 스크롤 목록: 마스크 영역 안에서 줄을 위에서부터 쌓고, 마지막 수가 보이게 맨 아래로 스크롤
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(sheet, false);
            RectTransform vp = viewport.AddComponent<RectTransform>();
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = new Vector2(40f, 30f);
            vp.offsetMax = new Vector2(-40f, -130f);
            viewport.AddComponent<RectMask2D>();
            Image vpHit = viewport.AddComponent<Image>();
            vpHit.color = new Color(0f, 0f, 0f, 0.001f); // 드래그를 받기 위한 투명 배경

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform ct = content.AddComponent<RectTransform>();
            ct.anchorMin = new Vector2(0f, 1f);
            ct.anchorMax = new Vector2(1f, 1f);
            ct.pivot = new Vector2(0.5f, 1f);
            ct.sizeDelta = Vector2.zero; // 폭은 목록 영역과 같게 (기본값 100이면 좌우로 50씩 삐져나가 잘림)
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (lines.Count == 0) lines.Add("아직 둔 수가 없습니다.");
            foreach (string line in lines)
            {
                Text row = CreatePanelText(content.transform, "Line", Vector2.zero, new Vector2(0f, 52f), 30, TextAnchor.MiddleLeft,
                    line.Contains(". 초 ") ? ChoColor : line.Contains(". 한 ") ? HanColor : TextSubColor);
                row.text = line;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            }

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = ct;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }

        private void ShowSettingsSheet()
        {
            JanggiSoundManager sound = JanggiSoundManager.Instance;
            GameObject overlay = CreateOverlay("SettingsSheet");
            RectTransform sheet = CreateCard(overlay.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 400f));
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.sizeDelta = new Vector2(-32f, 400f);

            Text title = CreatePanelText(sheet, "Title", new Vector2(0f, -24f), new Vector2(900f, 60f), 34, TextAnchor.MiddleCenter, Color.white);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.fontStyle = FontStyle.Bold;
            title.text = "설정";
            if (sound == null) return;

            // 누를 때마다 켜짐/꺼짐이 바뀌고 시트를 다시 그려 상태를 보여 줌
            CreateSheetRow(sheet, -100f, "settings", $"효과음: {(sound.SfxEnabled ? "켜짐" : "꺼짐")}", sound.SfxEnabled ? Color.white : TextSubColor,
                () => { sound.SfxEnabled = !sound.SfxEnabled; ShowSettingsSheet(); });
            CreateSheetRow(sheet, -224f, "settings", $"음성 대사: {(sound.VoiceEnabled ? "켜짐" : "꺼짐")}", sound.VoiceEnabled ? Color.white : TextSubColor,
                () => { sound.VoiceEnabled = !sound.VoiceEnabled; ShowSettingsSheet(); });
        }

        private void ShowConfirm(string title, string message, string confirmLabel, UnityEngine.Events.UnityAction onConfirm)
        {
            GameObject overlay = CreateOverlay("ConfirmDialog");
            RectTransform card = CreateCard(overlay.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 420f));

            Text t = CreatePanelText(card, "Title", new Vector2(0f, 110f), new Vector2(740f, 60f), 38, TextAnchor.MiddleCenter, Color.white);
            t.fontStyle = FontStyle.Bold;
            t.text = title;
            Text m = CreatePanelText(card, "Message", new Vector2(0f, 30f), new Vector2(740f, 60f), 27, TextAnchor.MiddleCenter, TextSubColor);
            m.text = message;

            CreateDialogButton(card, new Vector2(-190f, -120f), "취소", new Color(0.26f, 0.28f, 0.34f), CloseOverlay);
            CreateDialogButton(card, new Vector2(190f, -120f), confirmLabel, new Color(0.72f, 0.22f, 0.20f), () => { CloseOverlay(); onConfirm(); });
        }

        private void CreateDialogButton(Transform parent, Vector2 pos, string label, Color color, UnityEngine.Events.UnityAction action)
        {
            GameObject obj = new GameObject($"Btn_{label}");
            obj.transform.SetParent(parent, false);
            Image img = obj.AddComponent<Image>();
            img.sprite = GetRoundedSprite(48);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = new Vector2(340f, 104f);
            obj.AddComponent<Button>().onClick.AddListener(action);
            Text txt = CreatePanelText(obj.transform, "Label", Vector2.zero, new Vector2(340f, 104f), 32, TextAnchor.MiddleCenter, Color.white);
            txt.fontStyle = FontStyle.Bold;
            txt.text = label;
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
            cardRect.sizeDelta = new Vector2(860, 1590);

            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = pillSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            // Header Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(card.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 725);
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
            subTitleRect.anchoredPosition = new Vector2(0, 665);
            subTitleRect.sizeDelta = new Vector2(800, 50);

            Text subTitleTxt = subTitleObj.AddComponent<Text>();
            subTitleTxt.font = font;
            subTitleTxt.text = "대국 방식, 마상 차림, AI 수준, 제한 시간을 선택하세요";
            subTitleTxt.fontSize = 26;
            subTitleTxt.alignment = TextAnchor.MiddleCenter;
            subTitleTxt.color = new Color(0.8f, 0.9f, 1.0f);

            // Section 0: Game Mode
            CreateChoiceSection(card.transform, 555, "대국 방식", ModeLabels, (int)modeSelected, i => modeSelected = (GameManager.GameMode)i, font, pillSprite);

            // Section 1: Cho Placement Selection
            CreateTeamPlacementSection(card.transform, "🔵 초(楚) 차림", 325, Team.Cho, font, pillSprite);

            // Section 2: Han (AI) Placement Selection
            CreateTeamPlacementSection(card.transform, "🔴 한(漢) 차림", 75, Team.Han, font, pillSprite);

            // Section 3: AI Level Selection
            CreateAiLevelSection(card.transform, -175, font, pillSprite);

            // Section 4: Move Time Limit
            CreateTimeLimitSection(card.transform, -405, font, pillSprite);

            // Start / Apply Button
            GameObject startBtnObj = new GameObject("Btn_StartGame");
            startBtnObj.transform.SetParent(card.transform, false);
            RectTransform startRect = startBtnObj.AddComponent<RectTransform>();
            startRect.anchoredPosition = new Vector2(0, -615);
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
                PlayerPrefs.SetInt(GameManager.KeyMoveTimeLimit, timeLimitSelected);
                PlayerPrefs.SetInt(GameManager.KeyGameMode, (int)modeSelected);
                PlayerPrefs.Save();

                Destroy(dialogOverlay);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.aiLevel = aiSelectedLevel;
                    GameManager.Instance.moveTimeLimitSec = timeLimitSelected;
                    bool modeChanged = GameManager.Instance.Mode != modeSelected;
                    GameManager.Instance.ApplyMode(modeSelected);
                    if (modeChanged)
                    {
                        // 판 방향·패널 배치가 바뀌므로 새 대국을 저장한 뒤 씬을 다시 불러 처음부터 구성
                        GameManager.Instance.StartGame(choSelectedPlacement, hanSelectedPlacement);
                        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                        return;
                    }
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
            noticeRect.anchoredPosition = new Vector2(0, -730);
            noticeRect.sizeDelta = new Vector2(800, 80);

            Text noticeTxt = noticeObj.AddComponent<Text>();
            noticeTxt.font = font;
            noticeTxt.text = "성원장기 (GNU GPL v3) · 소스: github.com/id4erich/Janggi\nAI 엔진: Fairy-Stockfish (GNU GPL v3) · github.com/fairy-stockfish/Fairy-Stockfish\n기물 글씨: Yuji Boku·Yuji Syuku (SIL Open Font License 1.1)";
            noticeTxt.fontSize = 16;
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

        private static readonly int[] TimeLimitOptions = { 0, 30, 60 };

        private void CreateTimeLimitSection(Transform parent, float startY, Font font, Sprite pillSprite)
        {
            string[] labels = new string[TimeLimitOptions.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = TimeLimitOptions[i] == 0 ? "제한 없음" : $"{TimeLimitOptions[i]}초";
            CreateChoiceSection(parent, startY, "⏱ 한 수 제한 시간 (넘기면 시간패)", labels,
                System.Array.IndexOf(TimeLimitOptions, timeLimitSelected), i => timeLimitSelected = TimeLimitOptions[i], font, pillSprite);
        }

        // 제목 + 가로로 늘어선 선택 버튼 (선택한 것만 파란색)
        private void CreateChoiceSection(Transform parent, float startY, string title, string[] labels, int selected,
            System.Action<int> onSelect, Font font, Sprite pillSprite)
        {
            GameObject titleObj = new GameObject($"SectionTitle_{title}");
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
            titleTxt.color = new Color(1.0f, 0.85f, 0.35f);

            Color on = new Color(0.25f, 0.55f, 0.95f), off = new Color(0.20f, 0.24f, 0.32f);
            List<Image> btnImages = new List<Image>();
            float width = 760f / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                GameObject btnObj = new GameObject($"Choice_{labels[i]}");
                btnObj.transform.SetParent(parent, false);
                RectTransform rect = btnObj.AddComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-380f + width * (i + 0.5f), startY - 85f);
                rect.sizeDelta = new Vector2(width - 16f, 90f);

                Image img = btnObj.AddComponent<Image>();
                img.sprite = pillSprite;
                img.type = Image.Type.Sliced;
                img.color = i == selected ? on : off;
                btnImages.Add(img);

                btnObj.AddComponent<Button>().onClick.AddListener(() =>
                {
                    onSelect(index);
                    for (int k = 0; k < btnImages.Count; k++) btnImages[k].color = k == index ? on : off;
                });

                GameObject txtObj = new GameObject("Label");
                txtObj.transform.SetParent(btnObj.transform, false);
                RectTransform txtRect = txtObj.AddComponent<RectTransform>();
                txtRect.anchorMin = Vector2.zero;
                txtRect.anchorMax = Vector2.one;
                txtRect.sizeDelta = Vector2.zero;

                Text txt = txtObj.AddComponent<Text>();
                txt.font = font;
                txt.text = labels[i];
                txt.fontSize = 26;
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
                    Debug.Log("무르기 완료");
                }
            }
        }

        private void OnHintClicked()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (!gm.RequestHint(move => { if (BoardView.Instance != null) BoardView.Instance.ShowHint(move); }))
                ShowToast("지금은 힌트를 볼 수 없습니다");
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
            if (uiCanvas == null) return;
            if (GameManager.Instance != null) UpdateTurnDisplay(GameManager.Instance.CurrentTurn); // 패널 상태를 "대국 종료"로

            // 바깥을 누르면 창이 닫혀 끝난 판을 볼 수 있음 (새 대국은 메뉴에서도 가능)
            GameObject overlay = CreateOverlay("GameOverDialog");
            RectTransform card = CreateCard(overlay.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 600f));

            Text title = CreatePanelText(card, "Title", new Vector2(0f, 200f), new Vector2(780f, 100f), 72, TextAnchor.MiddleCenter, isPlayerWin ? AccentGold : HanColor);
            title.fontStyle = FontStyle.Bold;
            title.text = isPlayerWin ? "승리!" : "패배";

            Text result = CreatePanelText(card, "Result", new Vector2(0f, 120f), new Vector2(780f, 50f), 28, TextAnchor.MiddleCenter, TextSubColor);
            result.text = resultMsg;

            // 승패 연출이 이 문구를 바꿔 가며 놀리거나 분해함
            Text txt = CreatePanelText(card, "ResultText", new Vector2(0f, 10f), new Vector2(780f, 150f), 34, TextAnchor.MiddleCenter, Color.white);
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.fontStyle = FontStyle.Bold;

            CreateDialogButton(card, new Vector2(-190f, -200f), "판 보기", new Color(0.26f, 0.28f, 0.34f), CloseOverlay);
            CreateDialogButton(card, new Vector2(190f, -200f), "새 대국", new Color(0.25f, 0.50f, 0.90f), () =>
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));

            if (isPlayerWin)
            {
                RecordWin();
                txt.text = "축하합니다!";
                if (JanggiSoundManager.Instance != null) JanggiSoundManager.Instance.PlayPlayerWinVoice();
                StartCoroutine(PlayerWinSequence(txt));
            }
            else
            {
                RecordLoss();
                txt.text = "장기두는 사람 어디갔나~?";
                if (JanggiSoundManager.Instance != null) JanggiSoundManager.Instance.PlayPlayerLoseTauntVoice();
                StartCoroutine(PlayerLoseTauntSequence(txt, card.gameObject));
            }
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
                    // 연출 B: AI 궁 기물 멘탈 분쇄 폭발 + 억울한 변명 팝업
                    if (BoardView.Instance != null) BoardView.Instance.ExplodeAiKing();
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
                        BoardView.Instance.ExplodeAiKing();
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
                "😜 AI의 승리! 😜\n장기두는 사람 어디갔나~?",
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
            ShowBanner(msg, new Color(0.85f, 0.15f, 0.15f, 0.95f), Color.yellow);
        }

        // 음성 없이 잠깐 띄우는 안내 (힌트 결과 등)
        public void ShowToast(string msg) => ShowBanner(msg, new Color(0.12f, 0.14f, 0.18f, 0.95f), Color.white);

        private void ShowBanner(string msg, Color bgColor, Color textColor)
        {
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
            bg.color = bgColor;

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
            txt.color = textColor;

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
