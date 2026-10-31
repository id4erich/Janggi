using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Janggi
{
    // 대국 종료 창, 승패 연출(축포·판 뒤엎기·놀리기), 알림 배너
    public partial class MobileJanggiUIManager
    {
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
            txt.font = uiFont; // 화면 구성 때 한 번 불러 둔 글꼴 재사용 (띄울 때마다 새로 만들면 쌓임)
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
