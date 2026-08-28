using System;
using System.Collections.Generic;
using UnityEngine;

// Taruh script ini di GameObject "Map Generation".
// FULL WORLD-SPACE (bukan UI/Canvas) -> pakai SpriteRenderer + Sorting Layer.
// Player (kursor bidik) tetap di Canvas (Screen Space - Overlay) terpisah.
public class MapGenerator : MonoBehaviour
{
    [Header("Ukuran Map (grid) - random di antara nilai ini")]
    public int minRows = 3;
    public int maxRows = 5;
    public int minCols = 3;
    public int maxCols = 5;

    [Header("Area Kosong (Hole)")]
    [Tooltip("Maksimal jumlah area yang 'bolong' / tidak bisa diakses dalam 1 map (bisa 0, 1, atau ini)")]
    public int maxHoles = 2;

    [Header("Referensi Visual")]
    [Tooltip("Parent tempat semua area dibuat. Kosongkan biar otomatis pakai transform object ini sendiri.")]
    public Transform areaParent;
    [Tooltip("Kamera acuan buat hitung batas layar & auto-fit background. Kosongkan biar otomatis pakai Camera.main")]
    public Camera targetCamera;
    [Tooltip("Isi dengan sprite hutanz1 dan hutanz2")]
    public Sprite[] backgroundSprites;
    [Tooltip("Margin ekstra biar background dijamin nutup 1 layar penuh tanpa ada celah tipis di tepi (1 = pas, 1.02 = sedikit lebih)")]
    public float backgroundOverscan = 1.02f;
    [Tooltip("Prefab pohonz (SpriteRenderer biasa)")]
    public GameObject treePrefab;
    [Tooltip("Prefab mobilz -> muncul SEKALI, hanya di area tempat player spawn, nempel pojok kiri-bawah")]
    public GameObject carPrefab;

    [Header("Sorting (urutan render)")]
    [Tooltip("Samakan nama Sorting Layer ini dengan yang dipakai DeerManager")]
    public string sortingLayerName = "Default";
    public int backgroundSortingOrder = 0;
    public int treeSortingOrder = 1;
    public int carSortingOrder = 1;

    [Header("Pengaturan Pohon")]
    [Tooltip("Skala prefab pohon saat di-spawn (kompensasi kalau prefab-nya kegedean/kekecilan)")]
    public Vector3 treeScale = Vector3.one;
    [Tooltip("Maksimal jumlah pohon per area (0 - nilai ini)")]
    public int maxTreesPerArea = 2;
    [Tooltip("Jarak dari TEPI KAMERA kiri/kanan yang tidak boleh dipakai naruh pohon (world unit)")]
    public float horizontalPadding = 1f;
    [Tooltip("Jarak minimum antar pohon dalam 1 area (world unit), biar tidak numpuk")]
    public float minDistanceBetweenObjects = 1.5f;
    [Tooltip("Batas bawah 'pita tanah' (world unit dari TEPI BAWAH KAMERA) tempat pohon boleh muncul")]
    public float groundOffsetMin = 0.3f;
    [Tooltip("Batas atas 'pita tanah' (world unit dari TEPI BAWAH KAMERA) tempat pohon boleh muncul")]
    public float groundOffsetMax = 2f;

    [Header("Posisi Mobil (mobilz) - nempel pojok kiri-bawah kamera")]
    [Tooltip("Skala prefab mobil saat di-spawn")]
    public Vector3 carScale = Vector3.one;
    [Tooltip("Jarak mobil dari TEPI KIRI kamera (world unit)")]
    public float carLeftMargin = 1f;
    [Tooltip("Jarak mobil dari TEPI BAWAH kamera (world unit)")]
    public float carBottomMargin = 0.5f;

    // ---- Data internal, TIDAK perlu (dan tidak boleh) diubah manual dari Inspector ----
    private AreaData[,] grid;
    private int gridRows;
    private int gridCols;
    private List<Vector2Int> existingAreasList = new List<Vector2Int>();

    public int CurrentRow { get; private set; }
    public int CurrentCol { get; private set; }
    public Vector2Int SpawnCell { get; private set; }
    public AreaData CurrentArea => grid[CurrentRow, CurrentCol];
    public IReadOnlyList<Vector2Int> ExistingAreas => existingAreasList;

    public event Action OnMapGenerated;
    public event Action<Vector2Int> OnPlayerEnteredArea;

    private void Awake()
    {
        if (areaParent == null) areaParent = transform;
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Start()
    {
        // Sengaja di Start (bukan Awake): Unity menjamin SEMUA Awake() di seluruh scene selesai
        // dulu sebelum Start() manapun berjalan. Jadi DeerManager (yang subscribe ke OnMapGenerated
        // di Awake-nya) dijamin sudah ter-subscribe duluan, apapun urutan GameObject di Hierarchy.
        GenerateMap();
    }

    // ========================= UKURAN PANDANGAN KAMERA (dipakai bareng DeerManager juga) =========================

    // Menghitung berapa besar area (dalam world unit) yang benar-benar kelihatan oleh kamera.
    // Ini jadi patokan TUNGGAL buat: seberapa besar background harus di-scale, dan di mana batas
    // tepi layar yang tidak boleh dilewati pohon/rusa/mobil.
    public Vector2 GetCameraViewSize()
    {
        if (targetCamera == null) return new Vector2(16f, 9f); // fallback kalau kamera belum ke-assign

        float worldHeight = targetCamera.orthographicSize * 2f;
        float worldWidth = worldHeight * targetCamera.aspect;
        return new Vector2(worldWidth, worldHeight);
    }

    // ========================= GENERATE MAP =========================

    private void GenerateMap()
    {
        gridRows = UnityEngine.Random.Range(minRows, maxRows + 1);
        gridCols = UnityEngine.Random.Range(minCols, maxCols + 1);
        grid = new AreaData[gridRows, gridCols];

        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
                grid[r, c] = new AreaData { row = r, col = c, exists = true };

        int holesCount = UnityEngine.Random.Range(0, maxHoles + 1);

        Vector2Int spawn = Vector2Int.zero;
        bool validLayout = false;
        const int maxAttempts = 50;
        int bottomRow = gridRows - 1;

        for (int attempt = 0; attempt < maxAttempts && !validLayout; attempt++)
        {
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    grid[r, c].exists = true;

            List<Vector2Int> allCells = new List<Vector2Int>();
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    allCells.Add(new Vector2Int(r, c));

            Shuffle(allCells);
            for (int i = 0; i < holesCount && i < allCells.Count; i++)
                grid[allCells[i].x, allCells[i].y].exists = false;

            // spawn HANYA boleh di baris paling bawah, dan area itu harus "exists" (bukan hole)
            List<Vector2Int> spawnCandidates = new List<Vector2Int>();
            for (int c = 0; c < gridCols; c++)
                if (grid[bottomRow, c].exists) spawnCandidates.Add(new Vector2Int(bottomRow, c));

            if (spawnCandidates.Count == 0) continue; // baris bawah full hole (jarang banget), coba layout lain

            spawn = spawnCandidates[UnityEngine.Random.Range(0, spawnCandidates.Count)];

            if (IsFullyConnected(spawn))
                validLayout = true;
        }

        if (!validLayout)
        {
            for (int r = 0; r < gridRows; r++)
                for (int c = 0; c < gridCols; c++)
                    grid[r, c].exists = true;
            spawn = new Vector2Int(bottomRow, 0);
        }

        CurrentRow = spawn.x;
        CurrentCol = spawn.y;
        SpawnCell = spawn;

        existingAreasList.Clear();
        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
                if (grid[r, c].exists) existingAreasList.Add(new Vector2Int(r, c));

        foreach (var cell in existingAreasList)
            BuildAreaContent(grid[cell.x, cell.y]);

        foreach (var cell in existingAreasList)
        {
            AreaData data = grid[cell.x, cell.y];
            data.areaObject.SetActive(cell.x == CurrentRow && cell.y == CurrentCol);
        }

        OnMapGenerated?.Invoke();
    }

    private bool IsFullyConnected(Vector2Int start)
    {
        int totalExisting = 0;
        for (int r = 0; r < gridRows; r++)
            for (int c = 0; c < gridCols; c++)
                if (grid[r, c].exists) totalExisting++;

        bool[,] visited = new bool[gridRows, gridCols];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited[start.x, start.y] = true;
        int reached = 1;

        Vector2Int[] dirs = { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            foreach (var d in dirs)
            {
                int nr = cur.x + d.x;
                int nc = cur.y + d.y;
                if (nr < 0 || nr >= gridRows || nc < 0 || nc >= gridCols) continue;
                if (visited[nr, nc] || !grid[nr, nc].exists) continue;

                visited[nr, nc] = true;
                reached++;
                queue.Enqueue(new Vector2Int(nr, nc));
            }
        }

        return reached == totalExisting;
    }

    private void BuildAreaContent(AreaData data)
    {
        GameObject areaObj = new GameObject($"Area_{data.row}_{data.col}");
        areaObj.transform.SetParent(areaParent, false);
        areaObj.transform.localPosition = Vector3.zero; // semua area numpuk di posisi yang sama, tinggal di-SetActive gantian
        areaObj.transform.localScale = Vector3.one;       // container ini TIDAK pernah di-scale, biar tidak ikut nge-scale anak-anaknya

        Vector2 panelSize = GetCameraViewSize(); // patokan batas layar buat semua penempatan di bawah ini

        // --- Background: object TERPISAH dari container, biar scaling background gak ikut ngubah skala pohon/rusa/mobil ---
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(areaObj.transform, false);
        bgObj.transform.localPosition = Vector3.zero;

        data.backgroundIndex = UnityEngine.Random.Range(0, backgroundSprites.Length);
        SpriteRenderer bg = bgObj.AddComponent<SpriteRenderer>();
        bg.sprite = backgroundSprites[data.backgroundIndex];
        bg.sortingLayerName = sortingLayerName;
        bg.sortingOrder = backgroundSortingOrder;

        FitBackgroundToCamera(bg, panelSize);

        List<Vector2> occupiedPositions = new List<Vector2>();

        // tinggi visual pohon (world unit, sudah kena treeScale) -> biar badan pohon gak kepotong tepi kamera
        float treeHalfHeight = AreaLayoutUtility.GetSpriteHalfHeight(treePrefab, treeScale);

        // pohon (0 - maxTreesPerArea)
        int treeCount = UnityEngine.Random.Range(0, maxTreesPerArea + 1);
        for (int i = 0; i < treeCount; i++)
        {
            Vector2 pos = AreaLayoutUtility.GetRandomFreePosition(
                panelSize, occupiedPositions, horizontalPadding, minDistanceBetweenObjects, groundOffsetMin, groundOffsetMax, treeHalfHeight);
            occupiedPositions.Add(pos);
            data.treePositions.Add(pos);

            GameObject tree = Instantiate(treePrefab, areaObj.transform);
            tree.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            tree.transform.localScale = treeScale;
            ApplySorting(tree, treeSortingOrder);
        }

        // mobilz -> SEKALI SAJA, hanya di area spawn, NEMPEL pojok kiri-bawah kamera
        if (carPrefab != null && data.row == SpawnCell.x && data.col == SpawnCell.y)
        {
            float halfW = panelSize.x * 0.5f;
            float halfH = panelSize.y * 0.5f;
            Vector3 carPos = new Vector3(-halfW + carLeftMargin, -halfH + carBottomMargin, 0f);

            GameObject car = Instantiate(carPrefab, areaObj.transform);
            car.transform.localPosition = carPos;
            car.transform.localScale = carScale;
            ApplySorting(car, carSortingOrder);
        }

        data.isGenerated = true;
        data.areaObject = areaObj;
        areaObj.SetActive(false);
    }

    // Nge-scale SpriteRenderer background supaya persis (atau sedikit lebih besar dari) ukuran pandangan kamera,
    // apapun ukuran asli sprite-nya / berapapun Pixels Per Unit yang dipakai pas import.
    private void FitBackgroundToCamera(SpriteRenderer bg, Vector2 cameraViewSize)
    {
        if (bg.sprite == null) return;

        Vector2 nativeSize = bg.sprite.bounds.size; // ukuran sprite ini di scale (1,1,1)
        if (nativeSize.x <= 0f || nativeSize.y <= 0f) return;

        float scaleX = cameraViewSize.x / nativeSize.x;
        float scaleY = cameraViewSize.y / nativeSize.y;
        float uniformScale = Mathf.Max(scaleX, scaleY) * backgroundOverscan; // "cover": nutup penuh, boleh dikit ke-crop, tapi TIDAK gepeng/distorsi

        bg.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    private void ApplySorting(GameObject obj, int order)
    {
        SpriteRenderer sr = obj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = order;
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ========================= AKSES DATA (dipakai script lain, mis. DeerManager) =========================

    public AreaData GetAreaData(int row, int col)
    {
        if (row < 0 || row >= gridRows || col < 0 || col >= gridCols) return null;
        return grid[row, col];
    }

    // ========================= MOVEMENT SUPPORT =========================

    public bool CanMove(Vector2Int direction)
    {
        int nr = CurrentRow + direction.x;
        int nc = CurrentCol + direction.y;
        if (nr < 0 || nr >= gridRows || nc < 0 || nc >= gridCols) return false;
        return grid[nr, nc].exists;
    }

    public void MoveTo(Vector2Int direction)
    {
        if (!CanMove(direction)) return;

        AreaData oldArea = grid[CurrentRow, CurrentCol];
        oldArea.areaObject.SetActive(false);

        CurrentRow += direction.x;
        CurrentCol += direction.y;

        AreaData newArea = grid[CurrentRow, CurrentCol];
        newArea.areaObject.SetActive(true);

        OnPlayerEnteredArea?.Invoke(new Vector2Int(CurrentRow, CurrentCol));
    }
}