using UnityEngine;
using UnityEngine.UI;

namespace Janggi
{
    public class CameraUIController : MonoBehaviour
    {
        public Camera mainCamera;
        
        [Header("Camera Settings")]
        public float moveSpeed = 5.0f;
        public float zoomSpeed = 2.0f;
        public float rotateSpeed = 45.0f;

        [Tooltip("기본 시점: 바로 위(90도)에서 좁은 시야각으로 내려다봐 판이 반듯한 직사각형으로 화면 폭을 채움")]
        public float defaultPitch = 90.0f;
        public float defaultFieldOfView = 24.0f;
        [Tooltip("'시점' 버튼으로 전환하는 비스듬한 입체 시점")]
        public float tiltedPitch = 60.0f;
        public float tiltedFieldOfView = 38.0f;
        // 위 대국자 패널(약 10%)보다 아래 패널+도구 막대(약 17.6%)가 높아서, 판을 그 차이의 절반만큼 위로 올려 가운데에 둠
        private const float UiCenterOffset = 0.038f;
        public static readonly Color BackgroundColor = new Color(0.13f, 0.10f, 0.08f); // 탁자 너머 어두운 방

        private float currentPitch = 90.0f;
        private float currentYaw = 0.0f;
        private float baseYaw = 0.0f; // 한(漢)으로 두면 180
        private Vector3 targetLookAt = new Vector3(0f, 0f, 0f);
        private float distance = 11.2f;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = BackgroundColor;
            mainCamera.fieldOfView = defaultFieldOfView;
            currentPitch = defaultPitch;
            if (GameManager.Instance != null && GameManager.Instance.ViewFlipped) baseYaw = 180f;
            currentYaw = baseYaw;
            UpdateCameraTransform();
        }

        private void Update()
        {
            // 우클릭 마우스 드래그로 장기판 자유 회전/이동
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                if (UnityEngine.InputSystem.Mouse.current.rightButton.isPressed)
                {
                    Vector2 delta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
                    currentYaw += delta.x * 0.2f;
                    currentPitch = Mathf.Clamp(currentPitch - delta.y * 0.2f, 30f, 90f);
                    UpdateCameraTransform();
                }

                float scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    distance = Mathf.Clamp(distance - scroll * 0.005f, 6f, 25f);
                    UpdateCameraTransform();
                }
            }
#else
            if (Input.GetMouseButton(1))
            {
                currentYaw += Input.GetAxis("Mouse X") * 2f;
                currentPitch = Mathf.Clamp(currentPitch - Input.GetAxis("Mouse Y") * 2f, 30f, 90f);
                UpdateCameraTransform();
            }
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                distance = Mathf.Clamp(distance - scroll * 5f, 6f, 25f);
                UpdateCameraTransform();
            }
#endif
        }

        public void UpdateCameraTransform()
        {
            if (mainCamera == null) return;

            // 장기판 좌우 전체 폭(9.6 단위)이 화면 좌우 양 끝에 여백 없이 100% 딱 들어맞는 정밀 거리 자동 연산
            float aspect = mainCamera.aspect;
            float targetDistance = distance;

            // 정사영/직교 거리 연산 (FOV 60도 기준 좌우 폭 꽉 채움)
            if (aspect > 0.01f)
            {
                // 장기판 좌우 끝을 꽉 채우는 거리 (판 폭 9.52, 바로 위 시점은 여백을 최소로)
                bool topDown = currentPitch >= 89f;
                float tanHalf = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                targetDistance = (topDown ? 4.9f : 5.2f) / (tanHalf * aspect);
                // 태블릿처럼 화면이 넓으면 판 폭만 맞출 때 위아래 잡은 말 줄(z ±6.3)이 선수 패널에 가려짐:
                // 패널 사이 화면 높이의 약 70% 안에 잡은 말 줄까지 들어오도록 필요하면 더 물러남 (휴대폰은 그대로)
                if (topDown) targetDistance = Mathf.Max(targetDistance, 6.3f / 0.70f / tanHalf);
                // 비스듬히 볼 때는 가까운 쪽 판 가장자리가 더 넓게 보이므로 그만큼 물러남
                float fovRatio = Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(30f * Mathf.Deg2Rad);
                targetDistance *= 1f + Mathf.Cos(currentPitch * Mathf.Deg2Rad) * 0.25f * fovRatio;
            }

            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 dir = rot * new Vector3(0, 0, -targetDistance);
            Vector3 lookAt = targetLookAt;
            if (currentPitch >= 89f) // 화면 위쪽 방향으로 판을 올림 (시점이 돌아가도 같게)
                lookAt -= rot * Vector3.up * (UiCenterOffset * 2f * targetDistance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            mainCamera.transform.position = lookAt + dir;
            mainCamera.transform.rotation = rot; // 바로 위에서도 화면 위쪽이 한(漢) 진영이 되도록 LookAt 대신 회전 직접 지정
        }

        public void MoveUp() { targetLookAt += mainCamera.transform.up * 1.5f; UpdateCameraTransform(); }
        public void MoveDown() { targetLookAt -= mainCamera.transform.up * 1.5f; UpdateCameraTransform(); }
        public void MoveLeft() { targetLookAt -= mainCamera.transform.right * 1.5f; UpdateCameraTransform(); }
        public void MoveRight() { targetLookAt += mainCamera.transform.right * 1.5f; UpdateCameraTransform(); }

        public void ZoomIn() { distance = Mathf.Clamp(distance - 2f, 6f, 25f); UpdateCameraTransform(); }
        public void ZoomOut() { distance = Mathf.Clamp(distance + 2f, 6f, 25f); UpdateCameraTransform(); }

        // 시점 리셋: 이미 기본 시점이면 바로 위 시점으로, 아니면 기본(비스듬한) 시점으로 전환
        public void ResetView()
        {
            bool atDefault = targetLookAt == Vector3.zero && Mathf.Approximately(currentYaw, baseYaw) && Mathf.Approximately(currentPitch, defaultPitch);
            targetLookAt = new Vector3(0f, 0f, 0f);
            currentPitch = atDefault ? tiltedPitch : defaultPitch;
            mainCamera.fieldOfView = atDefault ? tiltedFieldOfView : defaultFieldOfView;
            currentYaw = baseYaw;
            distance = 18.5f;
            UpdateCameraTransform();
        }

        public void ShakeCamera(float duration = 0.6f, float magnitude = 0.8f)
        {
            StartCoroutine(ShakeCoroutine(duration, magnitude));
        }

        private System.Collections.IEnumerator ShakeCoroutine(float duration, float magnitude)
        {
            Vector3 originalLookAt = targetLookAt;
            float elapsed = 0.0f;

            while (elapsed < duration)
            {
                float offsetX = Random.Range(-1f, 1f) * magnitude;
                float offsetY = Random.Range(-1f, 1f) * magnitude;

                targetLookAt = originalLookAt + new Vector3(offsetX, 0f, offsetY);
                UpdateCameraTransform();

                elapsed += Time.deltaTime;
                yield return null;
            }

            targetLookAt = originalLookAt;
            UpdateCameraTransform();
        }

        private void CreateCameraControllerUI()
        {
            // Canvas 생성
            GameObject canvasObj = new GameObject("CameraControlCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // UI Panel
            GameObject panel = new GameObject("ControlPanel");
            panel.transform.SetParent(canvasObj.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0); // 우측 하단
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(1, 0);
            panelRect.anchoredPosition = new Vector2(-20, 20);
            panelRect.sizeDelta = new Vector2(160, 220);

            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.65f);

            // 방향 이동 버튼 생성 (상, 하, 좌, 우, 리셋, 줌)
            CreateButton(panel.transform, "▲", new Vector2(0, 60), new Vector2(40, 40), MoveUp);
            CreateButton(panel.transform, "▼", new Vector2(0, -20), new Vector2(40, 40), MoveDown);
            CreateButton(panel.transform, "◄", new Vector2(-45, 20), new Vector2(40, 40), MoveLeft);
            CreateButton(panel.transform, "►", new Vector2(45, 20), new Vector2(40, 40), MoveRight);

            CreateButton(panel.transform, "⟲ Reset", new Vector2(0, 20), new Vector2(45, 35), ResetView);
            CreateButton(panel.transform, "+ Zoom", new Vector2(-40, -70), new Vector2(65, 35), ZoomIn);
            CreateButton(panel.transform, "- Zoom", new Vector2(40, -70), new Vector2(65, 35), ZoomOut);
        }

        private void CreateButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            Text txt = textObj.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 14;
        }
    }
}
