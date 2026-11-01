using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Janggi
{
    // 대국자 패널·도구 막대·메뉴와 각종 시트·확인 창, 도구 막대 버튼 동작
    public partial class MobileJanggiUIManager
    {
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
            public RectTransform Captured; // 이 대국자가 잡은 상대 말
        }

        private PlayerPanel hanPanel, choPanel;
        private Button passButton, hintButton, undoButton;
        private Font uiFont;
        private Transform uiCanvas;
        private GameObject activeOverlay; // 메뉴 시트·확인 창 (뒤로 가기 키로 닫음)
        private GameObject setupDialog;   // 차림 설정 창 (따로 관리)

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

            // 상태 글 오른쪽, 전적·남은 시간 글 왼쪽 사이에 잡은 말을 줄지어 놓음
            GameObject captured = new GameObject("Captured");
            captured.transform.SetParent(root.transform, false);
            panel.Captured = captured.AddComponent<RectTransform>();
            panel.Captured.anchorMin = panel.Captured.anchorMax = panel.Captured.pivot = new Vector2(0f, 0.5f);
            panel.Captured.anchoredPosition = new Vector2(262f, -24f);
            panel.Captured.sizeDelta = new Vector2(CapturedRowWidth, 36f);
            return panel;
        }

        private const float CapturedRowWidth = 420f, CapturedIcon = 34f;

        // owner의 말 중 잡힌 것들을 잡은 쪽(상대) 패널에 표시. 많으면 겹쳐 놓아 한 줄에 맞춤
        public void ShowCapturedPieces(Team owner, List<PieceType> types)
        {
            PlayerPanel panel = hanPanel != null && hanPanel.Team != owner ? hanPanel : choPanel;
            if (panel == null || panel.Team == owner) return;
            foreach (Transform child in panel.Captured) Destroy(child.gameObject);

            float step = types.Count > 1 ? Mathf.Min(CapturedIcon + 2f, (CapturedRowWidth - CapturedIcon) / (types.Count - 1)) : 0f;
            Sprite rounded = GetRoundedSprite(48);
            for (int i = 0; i < types.Count; i++)
            {
                GameObject icon = new GameObject($"Captured_{types[i]}");
                icon.transform.SetParent(panel.Captured, false);
                RectTransform rect = icon.AddComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(i * step, 0f);
                rect.sizeDelta = new Vector2(CapturedIcon, CapturedIcon);
                Image mask = icon.AddComponent<Image>();
                mask.sprite = rounded;
                mask.type = Image.Type.Sliced;
                mask.raycastTarget = false;
                icon.AddComponent<Mask>().showMaskGraphic = false;

                GameObject face = new GameObject("Face");
                face.transform.SetParent(icon.transform, false);
                RawImage img = face.AddComponent<RawImage>();
                img.texture = Resources.Load<Texture2D>($"PieceArt/{owner}_{types[i]}");
                img.raycastTarget = false;
                Stretch(img.rectTransform, 0f);
            }
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
            CreateSheetRow(sheet, -348f, "settings", "설정 · 소리와 진동", Color.white, ShowSettingsSheet);
            CreateSheetRow(sheet, -472f, "flag", "기권", HanColor, () => ShowConfirm("기권할까요?", "이번 대국은 패배로 기록됩니다.", "기권", OnResignClicked));
            CreateSheetRow(sheet, -596f, "exit", "나가기", TextSubColor, () => ShowConfirm("앱을 종료할까요?", "두던 대국은 저장되어 다음에 이어서 둘 수 있습니다.", "나가기", OnExitClicked));
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
            RectTransform sheet = CreateCard(overlay.transform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 524f));
            sheet.anchorMin = new Vector2(0f, 0f);
            sheet.anchorMax = new Vector2(1f, 0f);
            sheet.sizeDelta = new Vector2(-32f, 524f);

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
            CreateSheetRow(sheet, -348f, "settings", $"진동: {(sound.VibrationEnabled ? "켜짐" : "꺼짐")}", sound.VibrationEnabled ? Color.white : TextSubColor,
                () => { sound.VibrationEnabled = !sound.VibrationEnabled; ShowSettingsSheet(); });
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

        private void OnPassClicked()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsHumanTurn || gm.IsGameOver) return;
            if (RuleValidator.IsInCheck(gm.Board, gm.CurrentTurn))
            {
                ShowToast("장군 상태에서는 한수 쉼을 할 수 없습니다");
                return;
            }
            gm.PassTurn();
            UpdateTurnDisplay(gm.CurrentTurn);
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
    }
}
