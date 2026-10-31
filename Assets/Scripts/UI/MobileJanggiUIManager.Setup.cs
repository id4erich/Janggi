using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Janggi
{
    // 새 대국 설정 창: 대국 방식·차림·AI 수준·제한 시간 선택
    public partial class MobileJanggiUIManager
    {
        public void ShowPlacementSetupDialog()
        {
            GameObject canvasObj = GameObject.Find("MobileJanggiCanvas");
            if (canvasObj == null) return;

            // 이미 열려 있는 팝업이 있다면 중복 생성 방지
            if (GameObject.Find("PlacementSetupDialog") != null) return;

            Font font = uiFont; // 화면 구성 때 한 번 불러 둔 글꼴 재사용

            Sprite pillSprite = GetRoundedSprite(48);

            GameObject dialogOverlay = new GameObject("PlacementSetupDialog");
            setupDialog = dialogOverlay;
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
            cardRect.sizeDelta = new Vector2(860, 1820);

            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = pillSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            // Header Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(card.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0, 840);
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
            subTitleRect.anchoredPosition = new Vector2(0, 780);
            subTitleRect.sizeDelta = new Vector2(800, 50);

            Text subTitleTxt = subTitleObj.AddComponent<Text>();
            subTitleTxt.font = font;
            subTitleTxt.text = "대국 방식, 규칙, 마상 차림, AI 수준, 제한 시간을 선택하세요";
            subTitleTxt.fontSize = 26;
            subTitleTxt.alignment = TextAnchor.MiddleCenter;
            subTitleTxt.color = new Color(0.8f, 0.9f, 1.0f);

            // Section 0: Game Mode
            CreateChoiceSection(card.transform, 670, "대국 방식", ModeLabels, (int)modeSelected, i => modeSelected = (GameManager.GameMode)i, font, pillSprite);

            // Section 0-1: Rules (Fairy-Stockfish 변형과 1:1)
            CreateChoiceSection(card.transform, 440, "규칙 (빅장·한수 쉼 두 번이면 끝)", RuleLabels, (int)rulesSelected, i => rulesSelected = (GameManager.RuleSet)i, font, pillSprite);

            // Section 1: Cho Placement Selection
            CreateTeamPlacementSection(card.transform, "🔵 초(楚) 차림", 210, Team.Cho, font, pillSprite);

            // Section 2: Han (AI) Placement Selection
            CreateTeamPlacementSection(card.transform, "🔴 한(漢) 차림", -40, Team.Han, font, pillSprite);

            // Section 3: AI Level Selection
            CreateAiLevelSection(card.transform, -290, font, pillSprite);

            // Section 4: Move Time Limit
            CreateTimeLimitSection(card.transform, -520, font, pillSprite);

            // Start / Apply Button
            GameObject startBtnObj = new GameObject("Btn_StartGame");
            startBtnObj.transform.SetParent(card.transform, false);
            RectTransform startRect = startBtnObj.AddComponent<RectTransform>();
            startRect.anchoredPosition = new Vector2(0, -730);
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
                PlayerPrefs.SetInt(GameManager.KeyRuleSet, (int)rulesSelected);
                PlayerPrefs.Save();

                Destroy(dialogOverlay);
                isMatchEnded = false; // 새 대국의 승패도 전적에 기록되게
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.aiLevel = aiSelectedLevel;
                    GameManager.Instance.moveTimeLimitSec = timeLimitSelected;
                    GameManager.Instance.ruleSet = rulesSelected;
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
            noticeRect.anchoredPosition = new Vector2(0, -845);
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
    }
}
