using UnityEngine;
using UnityEngine.UI;

// Taruh script ini di GameObject BARU, misal "DirectionArrows", sebagai child dari Canvas
// (sejajar dengan "Player" dan "ScreenFade"). Ini murni UI biasa, TIDAK perlu world-space,
// jadi RectTransform di sini aman-aman saja dipakai sesuai fungsinya.
//
// Setup 4 child Image di bawah "DirectionArrows":
//   - "ArrowUp"    -> anchor top-center,    sprite arrow_up
//   - "ArrowDown"  -> anchor bottom-center, sprite arrow_down
//   - "ArrowLeft"  -> anchor middle-left,   sprite arrow_left
//   - "ArrowRight" -> anchor middle-right,  sprite arrow_right
// Drag Anchor Preset-nya (Alt+Shift+klik preset yang sesuai) biar Anchor & Pos ke-set otomatis.
public class DirectionArrowController : MonoBehaviour
{
    [Header("Referensi")]
    public MapGenerator mapGenerator;

    [Header("Panah Arah (drag Image/GameObject masing-masing)")]
    public GameObject arrowUp;
    public GameObject arrowDown;
    public GameObject arrowLeft;
    public GameObject arrowRight;

    private void Awake()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("DirectionArrowController: field 'Map Generator' belum di-assign!");
            return;
        }

        // Subscribe di Awake -> aman terhadap urutan Awake/Start antar script (lihat catatan
        // yang sama di MapGenerator.OnMapGenerated soal kenapa event ini baru nembak di Start()).
        mapGenerator.OnMapGenerated += UpdateArrows;
        mapGenerator.OnPlayerEnteredArea += OnPlayerMoved;
    }

    private void OnDestroy()
    {
        if (mapGenerator == null) return;
        mapGenerator.OnMapGenerated -= UpdateArrows;
        mapGenerator.OnPlayerEnteredArea -= OnPlayerMoved;
    }

    private void OnPlayerMoved(Vector2Int newCell) => UpdateArrows();

    private void UpdateArrows()
    {
        SetArrow(arrowUp, new Vector2Int(-1, 0));   // atas  -> row berkurang
        SetArrow(arrowDown, new Vector2Int(1, 0));  // bawah -> row bertambah
        SetArrow(arrowLeft, new Vector2Int(0, -1)); // kiri  -> col berkurang
        SetArrow(arrowRight, new Vector2Int(0, 1)); // kanan -> col bertambah
    }

    private void SetArrow(GameObject arrow, Vector2Int direction)
    {
        if (arrow == null) return;
        arrow.SetActive(mapGenerator.CanMove(direction));
    }
}