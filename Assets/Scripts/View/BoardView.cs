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

            if (FindFirstObjectByType<MobileJanggiUIManager>() == null)
            {
                GameObject uiObj = new GameObject("MobileJanggiUIManager");
                uiObj.AddComponent<MobileJanggiUIManager>();
            }

            if (JanggiSoundManager.Instance == null && FindFirstObjectByType<JanggiSoundManager>() == null)
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

        // 판을 다시 그릴 때마다 증가. 진행 중이던 이동 애니메이션이 옛 판에 표시를 남기지 않게 함
        private int boardGeneration;

        public void RedrawBoard()
        {
            boardGeneration++;
            SetupBoardSurface();
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
                // 평면 MeshCollider는 움직이는 Rigidbody와 함께 못 쓰므로 판 두께만 한 상자 콜라이더로 교체
                MeshCollider planeCol = boardGrid.GetComponent<MeshCollider>();
                if (planeCol != null)
                {
                    Destroy(planeCol);
                    BoxCollider box = boardGrid.AddComponent<BoxCollider>();
                    box.size = new Vector3(10f, 0.4f, 10f);
                    box.center = new Vector3(0f, -0.2f, 0f);
                }

                Rigidbody boardRb = boardGrid.GetComponent<Rigidbody>();
                if (boardRb == null) boardRb = boardGrid.AddComponent<Rigidbody>();
                boardRb.mass = 5f;
                boardRb.AddForce(new Vector3(0f, 8f, -6f), ForceMode.Impulse);
                boardRb.AddTorque(new Vector3(-400f, 150f, 200f), ForceMode.Impulse);
            }

            yield return new WaitForSeconds(0.1f);
        }

        public void ExplodeAiKing()
        {
            Team aiTeam = GameManager.Instance != null ? GameManager.Instance.aiTeam : Team.Han;
            // AI 쪽 궁을 찾아 분쇄 폭발 연출 (내가 한으로 두면 초의 궁)
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    PieceData p = GameManager.Instance.Board[x, y];
                    if (p != null && p.Team == aiTeam && p.Type == PieceType.King)
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

            // 판 뒤엎기 연출 뒤 새 대국: 물리를 걷어내고 원위치·평면 콜라이더(클릭 판정용)로 복구
            Rigidbody flipRb = boardGrid.GetComponent<Rigidbody>();
            if (flipRb != null)
            {
                flipRb.isKinematic = true;
                Destroy(flipRb);
                Destroy(boardGrid.GetComponent<BoxCollider>());
                boardGrid.AddComponent<MeshCollider>();
            }
            boardGrid.transform.rotation = Quaternion.identity;

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
                // 윗면의 자식으로 붙여 판 뒤엎기 연출 때 함께 움직임 (윗면 스케일 0.952 x 1.071 보정)
                body.transform.SetParent(boardGrid.transform, false);
                body.transform.localPosition = new Vector3(0f, -0.2f - 0.002f, 0f);
                body.transform.localScale = new Vector3(10f, 0.4f, 10f);
                body.GetComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("BoardArt/BoardSide");
            }

            // 판을 올려 둔 어두운 탁자 (비스듬한 시점에서 판 너머로 보이는 바닥)
            if (GameObject.Find("Table") == null)
            {
                GameObject table = GameObject.CreatePrimitive(PrimitiveType.Plane);
                table.name = "Table";
                table.layer = 2; // Ignore Raycast: 떨어지는 말은 받치되 판 바깥 터치가 가장자리 칸 선택으로 이어지지 않게
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
            RefreshCapturedTray();
            ClearHint();
            UpdateCheckMarker();
        }

        // 회양목 장기말 재질: 윗면은 음각 붓글씨(Tools/PieceArt/bake_piece_textures.py로 생성), 옆면은 나뭇결.
        // Resources의 템플릿 재질을 복제해 _NORMALMAP 셰이더 변형이 빌드에서 빠지지 않게 함
        private static readonly Dictionary<string, Material[]> pieceMaterialCache = new Dictionary<string, Material[]>();
        private static Material pieceSideMaterial;

        // 메시·표시용 단색 재질은 종류마다 한 번만 만들어 재사용 (매번 new 하면 오브젝트를 지워도 해제되지 않고 쌓임)
        private static readonly Dictionary<PieceType, Mesh> pieceMeshCache = new Dictionary<PieceType, Mesh>();
        private static readonly Dictionary<Color, Material> flatMaterialCache = new Dictionary<Color, Material>();

        private static Material FlatMaterial(Color color)
        {
            if (flatMaterialCache.TryGetValue(color, out Material mat)) return mat;
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            mat = new Material(litShader) { color = color };
            flatMaterialCache[color] = mat;
            return mat;
        }

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
            GameObject obj = BuildPieceVisual(data.Team, data.Type, $"{data.Team}_{data.Type}_({data.Position.x},{data.Position.y})", withCollider: true);
            obj.transform.position = BoardToWorldPos(data.Position);
            pieceRenderers[data.Position] = obj;
        }

        private static float PieceHeight(PieceType type)
        {
            switch (type)
            {
                case PieceType.King: return 0.32f;
                case PieceType.Car:
                case PieceType.Cannon: return 0.28f;
                case PieceType.Guard:
                case PieceType.Pawn: return 0.22f;
                default: return 0.25f;
            }
        }

        private GameObject BuildPieceVisual(Team team, PieceType type, string name, bool withCollider)
        {
            GameObject obj = new GameObject(name);
            // 한(漢)으로 둘 때는 카메라가 180도 돌아가므로 기물도 돌려 글자가 바로 보이게
            if (GameManager.Instance != null && GameManager.Instance.ViewFlipped) obj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // 1. 입체감 충만한 3D 두께(Height) 부여
            float radius = 0.42f;
            float height = 0.25f; // 입체 3D 조각판 두께감 강화
            switch (type)
            {
                case PieceType.King: radius = 0.50f; height = 0.32f; break; // 궁: 우뚝 솟은 3D 8각
                case PieceType.Car:
                case PieceType.Cannon: radius = 0.47f; height = 0.28f; break;
                case PieceType.Horse:
                case PieceType.Elephant: radius = 0.44f; height = 0.25f; break;
                case PieceType.Guard:
                case PieceType.Pawn: radius = 0.385f; height = 0.22f; break;
            }

            // 2. 8각 3D 조각판 부착
            MeshFilter mf = obj.AddComponent<MeshFilter>();
            if (!pieceMeshCache.TryGetValue(type, out Mesh mesh))
                pieceMeshCache[type] = mesh = JanggiMeshGenerator.CreateOctagonalPieceMesh(radius, height);
            mf.sharedMesh = mesh;

            MeshRenderer ren = obj.AddComponent<MeshRenderer>();
            ren.sharedMaterials = GetPieceMaterials(team, type);

            // 그림자 투사(Shadow Casting) 및 수신 설정
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            ren.receiveShadows = true;

            // 콜라이더 생성 (판 위의 말만; 잡힌 말 진열대는 클릭 판정에서 제외)
            if (withCollider)
            {
                BoxCollider boxCol = obj.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(radius * 2f, height, radius * 2f);
            }
            return obj;
        }

        // 잡은 말 진열: 초가 잡은 한의 말은 판 아래쪽(초 쪽) 탁자, 한이 잡은 초의 말은 판 위쪽 탁자에 줄지어 놓음
        private readonly List<GameObject> capturedTray = new List<GameObject>();
        private static readonly (PieceType Type, int Count)[] InitialPieces =
        {
            (PieceType.Car, 2), (PieceType.Cannon, 2), (PieceType.Horse, 2), (PieceType.Elephant, 2),
            (PieceType.Guard, 2), (PieceType.Pawn, 5), (PieceType.King, 1)
        };

        public void RefreshCapturedTray()
        {
            foreach (var t in capturedTray) if (t != null) Destroy(t);
            capturedTray.Clear();

            const float scale = 0.75f, spacing = 0.6f, tableTop = -0.402f;
            foreach (Team owner in new[] { Team.Cho, Team.Han })
            {
                var onBoard = new Dictionary<PieceType, int>();
                foreach (var p in GameManager.Instance.Board)
                    if (p != null && p.Team == owner) onBoard[p.Type] = onBoard.TryGetValue(p.Type, out int c) ? c + 1 : 1;

                // 잡힌 말은 상대 쪽에 놓임: 한의 말 → 아래(z < 0), 초의 말 → 위(z > 0)
                float z = owner == Team.Han ? -5.95f : 5.95f;
                int slot = 0;
                foreach (var (type, count) in InitialPieces)
                {
                    int missing = count - (onBoard.TryGetValue(type, out int n) ? n : 0);
                    for (int i = 0; i < missing; i++, slot++)
                    {
                        GameObject piece = BuildPieceVisual(owner, type, $"Captured_{owner}_{type}", withCollider: false);
                        float x = -4.2f + (slot % 15) * spacing;
                        float row = (slot / 15) * spacing * Mathf.Sign(z);
                        piece.transform.localScale = Vector3.one * scale;
                        piece.transform.position = new Vector3(x, tableTop + PieceHeight(type) * scale * 0.5f, z + row);
                        capturedTray.Add(piece);
                    }
                }
            }
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

        private float lastBlockedToastTime = -10f;

        private void OnCellClicked(BoardPos pos)
        {
            GameManager gm = GameManager.Instance;
            if (gm.IsGameOver || !gm.IsHumanTurn)
            {
                // 판을 눌러도 반응이 없는 이유를 알려 줌 (연타 시 1.5초에 한 번만)
                if (Time.time - lastBlockedToastTime > 1.5f && MobileJanggiUIManager.Instance != null)
                {
                    lastBlockedToastTime = Time.time;
                    MobileJanggiUIManager.Instance.ShowToast(gm.IsGameOver ? "대국이 끝났습니다 · 메뉴에서 새 대국" : "AI가 생각 중입니다");
                }
                return;
            }

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

            selectionHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            selectionHighlight.name = "SelectionHighlight";
            selectionHighlight.transform.position = BoardToWorldPos(pos) + new Vector3(0f, -0.04f, 0f);
            selectionHighlight.transform.localScale = new Vector3(0.90f, 0.025f, 0.90f);
            Destroy(selectionHighlight.GetComponent<Collider>());

            Renderer ren = selectionHighlight.GetComponent<Renderer>();
            ren.sharedMaterial = FlatMaterial(new Color(0.2f, 1.0f, 0.3f)); // 초록색 선택 표시링

            // 고른 말을 살짝 들어 올려 집어 든 느낌을 줌
            if (pieceRenderers.TryGetValue(pos, out liftedPiece))
            {
                liftedPos = pos;
                liftRoutine = StartCoroutine(LiftPiece(liftedPiece, BoardToWorldPos(pos)));
            }
        }

        private GameObject liftedPiece;
        private BoardPos liftedPos;
        private Coroutine liftRoutine;

        private IEnumerator LiftPiece(GameObject piece, Vector3 rest)
        {
            const float duration = 0.08f;
            for (float e = 0f; e < duration && piece != null; e += Time.deltaTime)
            {
                piece.transform.position = rest + Vector3.up * (0.12f * Mathf.SmoothStep(0f, 1f, e / duration));
                yield return null;
            }
            if (piece != null) piece.transform.position = rest + Vector3.up * 0.12f;
            liftRoutine = null;
        }

        private void ClearSelectionHighlight()
        {
            if (selectionHighlight != null)
            {
                Destroy(selectionHighlight);
                selectionHighlight = null;
            }
            if (liftRoutine != null)
            {
                StopCoroutine(liftRoutine);
                liftRoutine = null;
            }
            if (liftedPiece != null)
            {
                liftedPiece.transform.position = BoardToWorldPos(liftedPos);
                liftedPiece = null;
            }
        }

        private void HighlightMoves(PieceData piece)
        {
            ClearHighlights();
            var moves = RuleValidator.GetLegalMoves(piece, GameManager.Instance.Board, GameManager.Instance.BikjangRule);
            
            // 판 위에 납작하게 놓인 표시: 빈 칸은 초록 원판, 잡을 수 있는 상대 말은 아래에 붉은 고리
            Material emptyMat = FlatMaterial(new Color(0.16f, 0.48f, 0.26f));
            Material captureMat = FlatMaterial(new Color(0.80f, 0.16f, 0.10f));

            foreach (var m in moves)
            {
                bool isCapture = GameManager.Instance.Board[m.x, m.y] != null;
                GameObject hl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hl.name = "Highlight";
                Vector3 p = BoardToWorldPos(m);
                hl.transform.position = new Vector3(p.x, 0.008f, p.z);
                hl.transform.localScale = isCapture ? new Vector3(1.02f, 0.004f, 1.02f) : new Vector3(0.30f, 0.004f, 0.30f);

                Renderer ren = hl.GetComponent<Renderer>();
                ren.sharedMaterial = isCapture ? captureMat : emptyMat;
                ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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
            ClearHint();
            ClearCheckMarker();

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
            int generation = boardGeneration;
            // 거리에 따라 시간·높이를 달리 함: 한 칸은 짧게 들어 "탁", 판 끝까지는 조금 더 길고 높게
            float span = Mathf.InverseLerp(1f, 9f, Vector3.Distance(startPos, endPos) / cellSpacing);
            float duration = Mathf.Lerp(0.18f, 0.34f, span);
            float arcHeight = Mathf.Lerp(0.22f, 0.45f, span);

            for (float e = 0f; e < duration && movingObj != null; e += Time.deltaTime)
            {
                float t = e / duration;
                Vector3 p = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                p.y += 4f * arcHeight * t * (1f - t); // 포물선: 내려올수록 빨라져 내려놓는 힘이 느껴짐
                movingObj.transform.position = p;
                yield return null;
            }
            if (movingObj != null) movingObj.transform.position = endPos;

            // 닿는 순간 소리·진동을 함께 냄
            if (JanggiSoundManager.Instance != null)
            {
                if (capturedObj != null) JanggiSoundManager.Instance.PlayCaptureSound();
                else JanggiSoundManager.Instance.PlayMoveSound();
            }

            // 잡힌 말은 부딪힌 방향으로 밀려나며 작아져 사라짐
            if (capturedObj != null)
            {
                Vector3 dir = endPos - startPos;
                dir.y = 0f;
                StartCoroutine(KnockAwayCaptured(capturedObj, dir.sqrMagnitude > 0f ? dir.normalized : Vector3.forward));
            }

            // 내려놓을 때 살짝 눌렸다 펴짐 (튀어 오르지 않음)
            if (movingObj != null)
            {
                Vector3 baseScale = movingObj.transform.localScale;
                Vector3 squashed = new Vector3(baseScale.x * 1.05f, baseScale.y * 0.86f, baseScale.z * 1.05f);
                const float squash = 0.05f, recover = 0.09f;
                for (float e = 0f; e < squash + recover && movingObj != null; e += Time.deltaTime)
                {
                    float k = e < squash ? e / squash : 1f - (e - squash) / recover;
                    movingObj.transform.localScale = Vector3.Lerp(baseScale, squashed, k);
                    yield return null;
                }
                if (movingObj != null) movingObj.transform.localScale = baseScale;
            }

            // 애니메이션 도중 무르기·새 대국으로 판이 다시 그려졌으면 옛 좌표에 표시를 남기지 않음
            if (generation != boardGeneration) yield break;

            // 최근 착수 이동 지점 표시링 생성 (시작 위치: 파란원, 끝 위치: 노란원)
            CreateLastMoveMarkers(from, to);
            UpdateCheckMarker();
        }

        private IEnumerator KnockAwayCaptured(GameObject obj, Vector3 dir)
        {
            Vector3 start = obj.transform.position, scale = obj.transform.localScale;
            const float duration = 0.22f;
            for (float e = 0f; e < duration && obj != null; e += Time.deltaTime)
            {
                float t = e / duration;
                Vector3 p = start + dir * (0.55f * cellSpacing * (1f - (1f - t) * (1f - t)));
                p.y += 0.12f * Mathf.Sin(t * Mathf.PI);
                obj.transform.position = p;
                obj.transform.localScale = scale * (1f - 0.7f * t);
                yield return null;
            }
            if (obj != null) Destroy(obj);
            RefreshCapturedTray();
        }

        private void CreateLastMoveMarkers(BoardPos from, BoardPos to)
        {
            // 기존 누적된 최근 이동 마커 완전 삭제
            ClearLastMoveHighlight();

            // 출발 위치 마커: 판 위의 작은 짙은 점 (말이 떠난 자리)
            lastMoveFromHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lastMoveFromHighlight.name = "LastMoveFrom";
            Vector3 fromPos = BoardToWorldPos(from);
            lastMoveFromHighlight.transform.position = new Vector3(fromPos.x, 0.007f, fromPos.z);
            lastMoveFromHighlight.transform.localScale = new Vector3(0.34f, 0.004f, 0.34f);
            Destroy(lastMoveFromHighlight.GetComponent<Collider>());

            Renderer fromRen = lastMoveFromHighlight.GetComponent<Renderer>();
            fromRen.sharedMaterial = FlatMaterial(new Color(0.36f, 0.22f, 0.12f));
            fromRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 도착 위치 마커: 말 아래로 비치는 금색 고리
            lastMoveToHighlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lastMoveToHighlight.name = "LastMoveTo";
            Vector3 toPos = BoardToWorldPos(to);
            lastMoveToHighlight.transform.position = new Vector3(toPos.x, 0.007f, toPos.z);
            lastMoveToHighlight.transform.localScale = new Vector3(1.0f, 0.004f, 1.0f);
            Destroy(lastMoveToHighlight.GetComponent<Collider>());

            Renderer toRen = lastMoveToHighlight.GetComponent<Renderer>();
            toRen.sharedMaterial = FlatMaterial(new Color(0.95f, 0.70f, 0.15f));
            toRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ── 장군 표시·힌트 ────────────────────────────────

        private GameObject checkMarker;
        private readonly List<GameObject> hintMarkers = new List<GameObject>();
        private static readonly Color HintColor = new Color(0.15f, 0.75f, 0.95f);

        private GameObject CreateFlatMarker(string name, Vector3 center, Vector3 scale, Color color, PrimitiveType shape = PrimitiveType.Cylinder, float y = 0.009f)
        {
            GameObject marker = GameObject.CreatePrimitive(shape);
            marker.name = name;
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = new Vector3(center.x, y, center.z);
            marker.transform.localScale = scale;
            Renderer ren = marker.GetComponent<Renderer>();
            ren.sharedMaterial = FlatMaterial(color);
            ren.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return marker;
        }

        // 둘 차례인 쪽이 장군을 받고 있으면 그 궁 아래에 붉은 고리 (외통으로 끝난 판도 표시)
        private void UpdateCheckMarker()
        {
            ClearCheckMarker();
            GameManager gm = GameManager.Instance;
            if (gm == null || !RuleValidator.IsInCheck(gm.Board, gm.CurrentTurn)) return;
            BoardPos? king = RuleValidator.FindKing(gm.Board, gm.CurrentTurn);
            if (king.HasValue)
                checkMarker = CreateFlatMarker("CheckMarker", BoardToWorldPos(king.Value), new Vector3(1.22f, 0.004f, 1.22f), new Color(0.95f, 0.12f, 0.10f));
        }

        private void ClearCheckMarker()
        {
            if (checkMarker != null) Destroy(checkMarker);
            checkMarker = null;
        }

        // 추천 수: 출발 칸 고리 + 도착 칸 점 + 둘을 잇는 화살 막대
        public void ShowHint(Move move)
        {
            ClearHint();
            if (move.From == move.To)
            {
                if (MobileJanggiUIManager.Instance != null) MobileJanggiUIManager.Instance.ShowToast("힌트: 한수 쉼");
                return;
            }

            Vector3 from = BoardToWorldPos(move.From), to = BoardToWorldPos(move.To);
            // 장군 표시(붉은 고리)와 겹쳐도 보이도록 조금 위에 그림
            hintMarkers.Add(CreateFlatMarker("HintFrom", from, new Vector3(1.12f, 0.004f, 1.12f), HintColor, y: 0.013f));
            hintMarkers.Add(CreateFlatMarker("HintTo", to, new Vector3(0.46f, 0.004f, 0.46f), HintColor, y: 0.013f));

            Vector3 dir = to - from;
            dir.y = 0f;
            float gap = 0.5f; // 말에 가려지지 않게 양 끝을 비움
            if (dir.magnitude > gap * 2f)
            {
                GameObject shaft = CreateFlatMarker("HintArrow", (from + to) * 0.5f, new Vector3(0.12f, 0.004f, dir.magnitude - gap * 2f), HintColor, PrimitiveType.Cube, 0.013f);
                shaft.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                hintMarkers.Add(shaft);
            }
        }

        private void ClearHint()
        {
            foreach (var m in hintMarkers) if (m != null) Destroy(m);
            hintMarkers.Clear();
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
        }
    }
}
