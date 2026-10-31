using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Janggi
{
    public class BoardView : MonoBehaviour
    {
        public static BoardView Instance { get; private set; }

        [Header("Board Settings")]
        public Vector3 originPosition = new Vector3(-4f, 0f, -4.5f);
        public float cellSpacing = 1.0f;

        private Dictionary<BoardPos, GameObject> pieceRenderers = new Dictionary<BoardPos, GameObject>();
        private List<GameObject> activeHighlights = new List<GameObject>();

        private BoardPos? selectedPos = null;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // CameraUIController 및 MobileJanggiUIManager 자동 부착
            if (Camera.main != null && Camera.main.GetComponent<CameraUIController>() == null)
            {
                Camera.main.gameObject.AddComponent<CameraUIController>();
            }

            if (FindObjectOfType<MobileJanggiUIManager>() == null)
            {
                GameObject uiObj = new GameObject("MobileJanggiUIManager");
                uiObj.AddComponent<MobileJanggiUIManager>();
            }

            if (JanggiSoundManager.Instance == null && FindObjectOfType<JanggiSoundManager>() == null)
            {
                GameObject soundObj = new GameObject("JanggiSoundManager");
                soundObj.AddComponent<JanggiSoundManager>();
            }

            SetupBoardSurface();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPieceMoved += HandlePieceMoved;
                GameManager.Instance.OnBoardReset += RedrawBoard;
                GenerateVisualBoard();
            }

            // 대국 시작 인사 음성 연출
            if (JanggiSoundManager.Instance != null)
            {
                JanggiSoundManager.Instance.PlayGameStartVoice();
            }
        }

        private GameObject selectionHighlight;

        public void RedrawBoard()
        {
            DeselectPiece();
            ClearHighlights();
            ClearLastMoveHighlight();

            foreach (var kvp in pieceRenderers)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            pieceRenderers.Clear();

            GenerateVisualBoard();
        }

        public void FlipBoard()
        {
            StartCoroutine(FlipBoardCoroutine());
        }

        private IEnumerator FlipBoardCoroutine()
        {
            GameObject boardGrid = GameObject.Find("BoardGrid");
            List<Rigidbody> pieceRbs = new List<Rigidbody>();

            // 모든 장기말에 Rigidbody 추가하여 3D 물리 폭발/뒤집기 연출 준비
            foreach (var kvp in pieceRenderers)
            {
                if (kvp.Value != null)
                {
                    Rigidbody rb = kvp.Value.GetComponent<Rigidbody>();
                    if (rb == null) rb = kvp.Value.AddComponent<Rigidbody>();
                    rb.mass = 0.5f;
                    rb.AddExplosionForce(350f, new Vector3(UnityEngine.Random.Range(-2f, 2f), -1f, UnityEngine.Random.Range(-2f, 2f)), 10f, 2.5f, ForceMode.Impulse);
                    rb.AddTorque(new Vector3(UnityEngine.Random.Range(-500f, 500f), UnityEngine.Random.Range(-500f, 500f), UnityEngine.Random.Range(-500f, 500f)));
                    pieceRbs.Add(rb);
                }
            }

            // 장기판(BoardGrid) 수직 및 회전 엎기 애니메이션
            if (boardGrid != null)
            {
                Rigidbody boardRb = boardGrid.GetComponent<Rigidbody>();
                if (boardRb == null) boardRb = boardGrid.AddComponent<Rigidbody>();
                boardRb.mass = 5f;
                boardRb.AddForce(new Vector3(0f, 8f, -6f), ForceMode.Impulse);
                boardRb.AddTorque(new Vector3(-400f, 150f, 200f), ForceMode.Impulse);
            }

            yield return new WaitForSeconds(0.1f);
        }

        public void ExplodeHanKing()
        {
            // 한(漢) 궁 찾아서 분쇄 폭발 연출
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = GameManager.Instance.Board[x, y];
                    if (p != null && p.Team == Team.Han && p.Type == PieceType.King)
                    {
                        if (pieceRenderers.TryGetValue(p.Position, out GameObject kingObj))
                        {
                            StartCoroutine(ExplodeObjectCoroutine(kingObj));
                        }
                    }
                }
            }
        }

        private IEnumerator ExplodeObjectCoroutine(GameObject obj)
        {
            if (obj == null) yield break;

            Vector3 pos = obj.transform.position;
            Destroy(obj);

            // 15개의 작은 조각 파편 폭발 생성
            for (int i = 0; i < 15; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.transform.position = pos + Random.insideUnitSphere * 0.2f;
                shard.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

                Renderer ren = shard.GetComponent<Renderer>();
                ren.material.color = new Color(0.85f, 0.1f, 0.1f); // 붉은 핏빛 한나라 파편

                Rigidbody rb = shard.AddComponent<Rigidbody>();
                rb.AddExplosionForce(400f, pos, 3f, 1f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 600f);

                Destroy(shard, 3.5f);
            }
        }

        private void SetupBoardSurface()
        {
            GameObject boardGrid = GameObject.Find("BoardGrid");
            if (boardGrid == null)
            {
                boardGrid = GameObject.CreatePrimitive(PrimitiveType.Plane);
                boardGrid.name = "BoardGrid";
            }

            // 기물 월드 좌표계 Vector3(-4 ~ +4, -4.5 ~ +4.5) 교차점과 100% 정밀 밀착 정렬
            boardGrid.transform.position = new Vector3(0f, 0f, 0f);
            boardGrid.transform.localScale = new Vector3(0.952f, 1f, 1.071f);

            // 나뭇결·먹선 텍스처는 Tools/PieceArt/bake_board_texture.py로 미리 구워 둔 것
            MeshRenderer ren = boardGrid.GetComponent<MeshRenderer>();
            ren.sharedMaterial = Resources.Load<Material>("BoardArt/Board");
            ren.receiveShadows = true;

            // 판 두께: 윗면(Plane) 바로 아래 나무 몸체. 클릭 판정은 윗면 Plane 콜라이더만 사용
            if (GameObject.Find("BoardBody") == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "BoardBody";
                Destroy(body.GetComponent<Collider>());
                body.transform.position = new Vector3(0f, -0.2f - 0.002f, 0f);
                body.transform.localScale = new Vector3(9.52f, 0.4f, 10.71f);
                body.GetComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("BoardArt/BoardSide");
            }

            // 판을 올려 둔 어두운 탁자 (비스듬한 시점에서 판 너머로 보이는 바닥)
            if (GameObject.Find("Table") == null)
            {
                GameObject table = GameObject.CreatePrimitive(PrimitiveType.Plane);
                table.name = "Table";
                Destroy(table.GetComponent<Collider>());
                table.transform.position = new Vector3(0f, -0.402f, 0f);
                table.transform.localScale = new Vector3(8f, 1f, 8f);
                Material tableMat = new Material(Resources.Load<Material>("BoardArt/BoardSide"));
                tableMat.color = new Color(0.42f, 0.30f, 0.24f);
                table.GetComponent<MeshRenderer>().sharedMaterial = tableMat;
            }
        }

        public Vector3 BoardToWorldPos(BoardPos pos)
        {
            return originPosition + new Vector3(pos.x * cellSpacing, 0.1f, pos.y * cellSpacing);
        }

        public BoardPos WorldToBoardPos(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt((worldPos.x - originPosition.x) / cellSpacing);
            int y = Mathf.RoundToInt((worldPos.z - originPosition.z) / cellSpacing);
            return new BoardPos(Mathf.Clamp(x, 0, 8), Mathf.Clamp(y, 0, 9));
        }

        public void GenerateVisualBoard()
        {
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData data = GameManager.Instance.Board[x, y];
                    if (data != null)
                    {
                        CreatePieceObject(data);
                    }
                }
            }
        }

        // 회양목 장기말 재질: 윗면은 음각 붓글씨(Tools/PieceArt/bake_piece_textures.py로 생성), 옆면은 나뭇결.
        // Resources의 템플릿 재질을 복제해 _NORMALMAP 셰이더 변형이 빌드에서 빠지지 않게 함
        private static readonly Dictionary<string, Material[]> pieceMaterialCache = new Dictionary<string, Material[]>();
        private static Material pieceSideMaterial;

        private static Material[] GetPieceMaterials(Team team, PieceType type)
        {
            string key = $"{team}_{type}";
            if (pieceMaterialCache.TryGetValue(key, out Material[] mats)) return mats;

            if (pieceSideMaterial == null)
            {
                pieceSideMaterial = new Material(Resources.Load<Material>("PieceArt/PieceSide"));
                pieceSideMaterial.SetTexture("_BaseMap", Resources.Load<Texture2D>("PieceArt/wood_side"));
            }

            Material topMat = new Material(Resources.Load<Material>("PieceArt/PieceTop"));
            topMat.SetTexture("_BaseMap", Resources.Load<Texture2D>($"PieceArt/{key}"));
            topMat.SetTexture("_BumpMap", Resources.Load<Texture2D>($"PieceArt/{key}_n"));

            mats = new[] { pieceSideMaterial, topMat };
            pieceMaterialCache[key] = mats;
            return mats;
        }

        private void CreatePieceObject(PieceData data)
        {
            GameObject obj = new GameObject($"{data.Team}_{data.Type}_({data.Position.x},{data.Position.y})");
            obj.transform.position = BoardToWorldPos(data.Position);

            // 1. 입체감 충만한 3D 두께(Height) 부여
            float radius = 0.42f;
            float height = 0.25f; // 입체 3D 조각판 두께감 강화
            switch (data.Type)
            {
                case PieceType.King: radius = 0.48f; height = 0.32f; break; // 궁: 우뚝 솟은 3D 8각
                case PieceType.Car:
                case PieceType.Cannon: radius = 0.43f; height = 0.28f; break;
                case PieceType.Horse:
                case PieceType.Elephant: radius = 0.40f; height = 0.25f; break;
                case PieceType.Guard:
                case PieceType.Pawn: radius = 0.35f; height = 0.22f; break;
            }

            // 2. 8각 3D 조각판 부착
            MeshFilter mf = obj.AddComponent<MeshFilter>();
            mf.mesh = JanggiMeshGenerator.CreateOctagonalPieceMesh(radius, height);

            MeshRenderer ren = obj.AddComponent<MeshRenderer>();
            ren.sharedMaterials = GetPieceMaterials(data.Team, data.Type);

            // 그림자 투사(Shadow Casting) 및 수신 설정
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            ren.receiveShadows = true;

            // 콜라이더 생성
            BoxCollider boxCol = obj.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(radius * 2f, height, radius * 2f);

            pieceRenderers[data.Position] = obj;
        }

        private void Update()
        {
            Vector2? inputPos = null;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                inputPos = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                inputPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                inputPos = Input.GetTouch(0).position;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                inputPos = Input.mousePosition;
            }
#endif

            if (inputPos.HasValue)
            {
                // UI 위를 클릭/터치했을 경우 장기판 클릭 이벤트 제외 (EventSystem 레이캐스트 검사)
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Ray ray = Camera.main.ScreenPointToRay(inputPos.Value);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    BoardPos pos = WorldToBoardPos(hit.point);
                    OnCellClicked(pos);
                }
            }
        }

        private void OnCellClicked(BoardPos pos)
        {
            if (GameManager.Instance.CurrentTurn == GameManager.Instance.aiTeam && GameManager.Instance.isPlayerVsAI)
                return;

            PieceData clickedPiece = GameManager.Instance.Board[pos.x, pos.y];

            if (selectedPos == null)
            {
                // 말 선택
                if (clickedPiece != null && clickedPiece.Team == GameManager.Instance.CurrentTurn)
                {
                    SelectPiece(pos);
                }
            }
            else
            {
                BoardPos from = selectedPos.Value;

                // 이미 선택된 말을 다시 클릭하면 선택 해제
                if (from == pos)
                {
                    DeselectPiece();
                    return;
                }

                // 자신의 다른 말을 클릭하면 선택 대상 변경
                if (clickedPiece != null && clickedPiece.Team == GameManager.Instance.CurrentTurn)
                {
                    SelectPiece(pos);
                    return;
                }

                // 이동 또는 상대 말 잡기 시도
                DeselectPiece();
                GameManager.Instance.MakeMove(from, pos);
            }
        }

        private void SelectPiece(BoardPos pos)
        {
            selectedPos = pos;
            ShowSelectionHighlight(pos);
            PieceData p = GameManager.Instance.Board[pos.x, pos.y];
            if (p != null)
            {
                HighlightMoves(p);
            }
        }

        private void DeselectPiece()
        {
            selectedPos = null;
            ClearSelectionHighlight();
            ClearHighlights();
        }

        private void ShowSelectionHighlight(BoardPos pos)
        {
            ClearSelectionHighlight();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            selectionHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            selectionHighlight.name = "SelectionHighlight";
            selectionHighlight.transform.position = BoardToWorldPos(pos) + new Vector3(0f, -0.04f, 0f);
            selectionHighlight.transform.localScale = new Vector3(0.90f, 0.025f, 0.90f);
            Destroy(selectionHighlight.GetComponent<Collider>());

            Renderer ren = selectionHighlight.GetComponent<Renderer>();
            Material mat = new Material(litShader);
            mat.color = new Color(0.2f, 1.0f, 0.3f, 0.8f); // 초록색 선택 표시링
            ren.material = mat;
        }

        private void ClearSelectionHighlight()
        {
            if (selectionHighlight != null)
            {
                Destroy(selectionHighlight);
                selectionHighlight = null;
            }
        }

        private void HighlightMoves(PieceData piece)
        {
            ClearHighlights();
            var moves = RuleValidator.GetLegalMoves(piece, GameManager.Instance.Board);
            
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            foreach (var m in moves)
            {
                GameObject hl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hl.name = "Highlight";
                hl.transform.position = BoardToWorldPos(m) + new Vector3(0f, 0.18f, 0f);
                hl.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);

                Renderer ren = hl.GetComponent<Renderer>();
                Material mat = new Material(litShader);
                mat.color = new Color(1.0f, 0.9f, 0.1f, 0.85f); // 이동 가능 위치 노란색 작은 구체
                ren.material = mat;

                Destroy(hl.GetComponent<Collider>());
                activeHighlights.Add(hl);
            }
        }

        private void ClearHighlights()
        {
            foreach (var hl in activeHighlights)
            {
                if (hl != null) Destroy(hl);
            }
            activeHighlights.Clear();
        }

        private GameObject lastMoveFromHighlight;
        private GameObject lastMoveToHighlight;

        private void HandlePieceMoved(BoardPos from, BoardPos to)
        {
            DeselectPiece();
            ClearHighlights();
            ClearLastMoveHighlight();

            GameObject capturedObj = null;
            if (pieceRenderers.TryGetValue(to, out capturedObj))
            {
                pieceRenderers.Remove(to);
            }

            if (pieceRenderers.TryGetValue(from, out GameObject movingObj))
            {
                pieceRenderers.Remove(from);
                pieceRenderers[to] = movingObj;

                Vector3 startPos = BoardToWorldPos(from);
                Vector3 endPos = BoardToWorldPos(to);

                StartCoroutine(AnimatePieceMovement(movingObj, startPos, endPos, from, to, capturedObj));
            }
        }

        private IEnumerator AnimatePieceMovement(GameObject movingObj, Vector3 startPos, Vector3 endPos, BoardPos from, BoardPos to, GameObject capturedObj)
        {
            float duration = 0.45f; // 이동 애니메이션 소요 시간
            float elapsedTime = 0f;
            float arcHeight = 0.8f; // 공중으로 둥실 부드럽게 들려서 포물선 이동하는 3D 곡선 높이

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                // SmoothStep(이지인 아웃) 보정
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // XZ 선형 이동 + Y 수직 포물선 곡선 (Sin 곡선 파동)
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, smoothT);
                currentPos.y += Mathf.Sin(smoothT * Mathf.PI) * arcHeight;

                if (movingObj != null)
                {
                    movingObj.transform.position = currentPos;
                }

                yield return null;
            }

            if (movingObj != null)
            {
                movingObj.transform.position = endPos;
            }

            // 착수 "탁": 내려놓는 순간 살짝 튕겼다 자리 잡음
            const float settle = 0.12f;
            for (float e = 0f; e < settle && movingObj != null; e += Time.deltaTime)
            {
                movingObj.transform.position = endPos + Vector3.up * (Mathf.Sin(e / settle * Mathf.PI) * 0.06f);
                yield return null;
            }
            if (movingObj != null) movingObj.transform.position = endPos;

            // 착수음 / 잡기 sound effect 재생
            if (JanggiSoundManager.Instance != null)
            {
                if (capturedObj != null) JanggiSoundManager.Instance.PlayCaptureSound();
                else JanggiSoundManager.Instance.PlayMoveSound();
            }

            // 도착 시 상대 잡힌 말 파괴 처리 (착수 임팩트 효과)
            if (capturedObj != null)
            {
                Destroy(capturedObj);
            }

            // 최근 착수 이동 지점 표시링 생성 (시작 위치: 파란원, 끝 위치: 노란원)
            CreateLastMoveMarkers(from, to);
        }

        private void CreateLastMoveMarkers(BoardPos from, BoardPos to)
        {
            // 기존 누적된 최근 이동 마커 완전 삭제
            ClearLastMoveHighlight();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // 출발 위치 마커 (파란원)
            lastMoveFromHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lastMoveFromHighlight.name = "LastMoveFrom";
            lastMoveFromHighlight.transform.position = BoardToWorldPos(from) + new Vector3(0f, -0.05f, 0f);
            lastMoveFromHighlight.transform.localScale = new Vector3(0.85f, 0.02f, 0.85f);
            Destroy(lastMoveFromHighlight.GetComponent<Collider>());

            Renderer fromRen = lastMoveFromHighlight.GetComponent<Renderer>();
            Material fromMat = new Material(litShader);
            fromMat.color = new Color(0.1f, 0.5f, 1.0f, 0.7f); // 파란원 (Blue Circle)
            fromRen.material = fromMat;

            // 도착 위치 마커 (노란원)
            lastMoveToHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lastMoveToHighlight.name = "LastMoveTo";
            lastMoveToHighlight.transform.position = BoardToWorldPos(to) + new Vector3(0f, -0.05f, 0f);
            lastMoveToHighlight.transform.localScale = new Vector3(0.95f, 0.02f, 0.95f);
            Destroy(lastMoveToHighlight.GetComponent<Collider>());

            Renderer toRen = lastMoveToHighlight.GetComponent<Renderer>();
            Material toMat = new Material(litShader);
            toMat.color = new Color(1.0f, 0.85f, 0.0f, 0.8f); // 노란원 (Yellow Circle)
            toRen.material = toMat;
        }

        private void ClearLastMoveHighlight()
        {
            if (lastMoveFromHighlight != null)
            {
                Destroy(lastMoveFromHighlight);
                lastMoveFromHighlight = null;
            }
            if (lastMoveToHighlight != null)
            {
                Destroy(lastMoveToHighlight);
                lastMoveToHighlight = null;
            }

            // 씬에 누적 생성된 기존 "LastMoveFrom", "LastMoveTo" 마커 잔여물 전수 제거
            var allObjs = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjs)
            {
                if (obj != null && (obj.name == "LastMoveFrom" || obj.name == "LastMoveTo"))
                {
                    Destroy(obj);
                }
            }
        }
    }
}
