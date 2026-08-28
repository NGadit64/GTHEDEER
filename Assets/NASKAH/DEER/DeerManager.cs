using System.Collections.Generic;
using UnityEngine;

// Taruh script ini di GameObject TERPISAH, misal "Deer Manager".
// SEKARANG FULL WORLD-SPACE, konsisten dengan MapGenerator -> pakai SpriteRenderer + Sorting Layer.
public class DeerManager : MonoBehaviour
{
    public static DeerManager Instance { get; private set; }

    [Header("Referensi")]
    public MapGenerator mapGenerator;
    [Tooltip("Prefab noko (SpriteRenderer biasa)")]
    public GameObject deerPrefab;

    [Header("Sorting (urutan render)")]
    [Tooltip("Samakan nama Sorting Layer ini dengan yang dipakai MapGenerator")]
    public string sortingLayerName = "Default";
    [Tooltip("Harus LEBIH BESAR dari 'Tree Sorting Order' di MapGenerator, supaya rusa selalu di depan pohon")]
    public int deerSortingOrder = 2;

    [Header("Pengaturan Rusa")]
    [Tooltip("Skala prefab rusa saat di-spawn (buat kompensasi kalau prefab-nya kegedean/kekecilan)")]
    public Vector3 deerScale = Vector3.one;
    [Tooltip("Jumlah maksimal rusa yang aktif hidup di map dalam satu waktu (se-map, bukan per area)")]
    public int maxDeerTotal = 3;
    [Tooltip("Kalau true, area spawn player tidak akan kebagian rusa")]
    public bool excludeSpawnAreaFromDeer = true;
    [Tooltip("Jarak dari tepi kiri/kanan area (world unit) yang tidak boleh dipakai naruh rusa")]
    public float horizontalPadding = 1f;
    [Tooltip("Jarak minimum antar objek (rusa/pohon) dalam 1 area (world unit), biar tidak numpuk")]
    public float minDistanceBetweenObjects = 1.5f;
    [Tooltip("Batas bawah 'pita tanah' (world unit dari tepi bawah area) tempat rusa boleh muncul")]
    public float groundOffsetMin = 0.3f;
    [Tooltip("Batas atas 'pita tanah' (world unit dari tepi bawah area) tempat rusa boleh muncul")]
    public float groundOffsetMax = 2f;

    // ---- Data internal, TIDAK perlu (dan tidak boleh) diubah manual dari Inspector ----
    private readonly List<DeerInstance> activeDeer = new List<DeerInstance>();
    private readonly Dictionary<Vector2Int, DeerInstance> deerByArea = new Dictionary<Vector2Int, DeerInstance>();
    private int nextDeerId = 0;

    public IReadOnlyList<DeerInstance> ActiveDeer => activeDeer;

    private void Awake()
    {
        Instance = this;
        mapGenerator.OnMapGenerated += SpawnInitialDeer;
    }

    private void OnDestroy()
    {
        if (mapGenerator != null) mapGenerator.OnMapGenerated -= SpawnInitialDeer;
    }

    // ========================= SPAWN AWAL =========================

    private void SpawnInitialDeer()
    {
        List<Vector2Int> pool = new List<Vector2Int>(mapGenerator.ExistingAreas);
        if (excludeSpawnAreaFromDeer) pool.Remove(mapGenerator.SpawnCell);

        Shuffle(pool);
        int count = Mathf.Min(maxDeerTotal, pool.Count);
        for (int i = 0; i < count; i++)
            SpawnDeerInArea(pool[i]);
    }

    private void SpawnDeerInArea(Vector2Int cell)
    {
        if (deerByArea.ContainsKey(cell)) return; // sudah ada rusa di area ini, skip (maks 1 rusa per area)

        AreaData area = mapGenerator.GetAreaData(cell.x, cell.y);
        if (area == null || !area.exists || area.areaObject == null) return;

        // Batas penempatan rusa sekarang ikut patokan yang SAMA dengan MapGenerator: ukuran pandangan
        // kamera -> jadi rusa juga dijamin nggak akan muncul di luar tepi layar.
        Vector2 panelSize = mapGenerator.GetCameraViewSize();

        // pohon yang sudah ada di area ini dihitung sebagai "occupied" biar rusa nggak numpuk sama pohon
        List<Vector2> occupied = new List<Vector2>(area.treePositions);

        // tinggi visual rusa (world unit, sudah kena deerScale) -> biar badan rusa gak kepotong tepi kamera
        float deerHalfHeight = AreaLayoutUtility.GetSpriteHalfHeight(deerPrefab, deerScale);

        Vector2 pos = AreaLayoutUtility.GetRandomFreePosition(
            panelSize, occupied, horizontalPadding, minDistanceBetweenObjects, groundOffsetMin, groundOffsetMax, deerHalfHeight);

        GameObject deerObj = Instantiate(deerPrefab, area.areaObject.transform);
        deerObj.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        deerObj.transform.localScale = deerScale;

        SpriteRenderer deerSr = deerObj.GetComponentInChildren<SpriteRenderer>();
        if (deerSr != null)
        {
            deerSr.sortingLayerName = sortingLayerName;
            deerSr.sortingOrder = deerSortingOrder;
        }

        DeerAgent agent = deerObj.GetComponent<DeerAgent>();
        if (agent == null) agent = deerObj.AddComponent<DeerAgent>();

        DeerInstance instance = new DeerInstance
        {
            id = nextDeerId++,
            areaCell = cell,
            localPosition = pos,
            visualObject = deerObj,
            isAlive = true
        };
        agent.DeerId = instance.id;

        activeDeer.Add(instance);
        deerByArea[cell] = instance;
    }

    // ========================= QUERY (dipakai fitur lain nanti) =========================

    public bool TryGetDeerInArea(Vector2Int cell, out DeerInstance deer) => deerByArea.TryGetValue(cell, out deer);

    public DeerInstance GetDeerById(int id) => activeDeer.Find(d => d.id == id);

    // ========================= UNTUK FITUR NANTI - BELUM DIPANGGIL DARI MANA PUN =========================
    public void RemoveDeer(DeerInstance deer, bool respawnElsewhere = true)
    {
        if (deer == null || !deer.isAlive) return;

        deer.isAlive = false;
        deerByArea.Remove(deer.areaCell);
        activeDeer.Remove(deer);

        if (deer.visualObject != null) Destroy(deer.visualObject);

        if (respawnElsewhere)
            RespawnDeerInNewArea(deer.areaCell);
    }

    private void RespawnDeerInNewArea(Vector2Int excludeCell)
    {
        List<Vector2Int> pool = new List<Vector2Int>(mapGenerator.ExistingAreas);
        pool.Remove(excludeCell);
        pool.RemoveAll(cell => deerByArea.ContainsKey(cell));
        if (excludeSpawnAreaFromDeer) pool.Remove(mapGenerator.SpawnCell);

        if (pool.Count == 0) return;

        Vector2Int target = pool[Random.Range(0, pool.Count)];
        SpawnDeerInArea(target);
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}