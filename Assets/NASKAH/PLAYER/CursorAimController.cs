using UnityEngine;
using UnityEngine.InputSystem;

// Taruh LANGSUNG di object "kursorz" (child dari "Player").
// FULL WORLD-SPACE sekarang -> "kursorz" wajib punya SpriteRenderer (visual crosshair) dan
// Collider2D (mis. Circle Collider 2D) yang jadi hitbox tembakan. TIDAK PAKAI RectTransform/Canvas lagi.
[RequireComponent(typeof(Collider2D))]
public class CursorAimController : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("Kamera yang dipakai buat konversi posisi mouse ke world. Kosongkan biar otomatis pakai Camera.main")]
    public Camera worldCamera;
    [Tooltip("Sembunyikan kursor OS bawaan, diganti tampilan 'kursorz'")]
    public bool hideSystemCursor = true;

    [Header("Efek Tangan Goyang (dihitung dalam pixel layar dulu, biar konsisten walau Orthographic Size beda-beda)")]
    public float swayAmplitude = 25f;
    public float swaySpeed = 1.5f;

    // kolider ini yang jadi hitbox tembakan sesungguhnya (biasanya lebih kecil dari visual kursornya)
    private Collider2D aimCollider;

    // seed acak per-sumbu biar goyangan X dan Y tidak sinkron (kelihatan lebih natural, bukan robotic)
    private float noiseSeedX;
    private float noiseSeedY;

    private void Awake()
    {
        aimCollider = GetComponent<Collider2D>();
        if (worldCamera == null) worldCamera = Camera.main;

        noiseSeedX = Random.Range(0f, 1000f);
        noiseSeedY = Random.Range(0f, 1000f);

        if (hideSystemCursor) Cursor.visible = false;
    }

    private void Update()
    {
        if (worldCamera == null)
        {
            Debug.LogWarning("CursorAimController: 'World Camera' kosong dan Camera.main juga tidak ketemu -> kursor tidak akan bergerak. Cek Main Camera masih ber-tag 'MainCamera', atau drag manual ke field 'World Camera'.");
            return;
        }
        if (Mouse.current == null)
        {
            Debug.LogWarning("CursorAimController: Mouse.current null -> tidak ada mouse yang terdeteksi Input System.");
            return;
        }

        UpdateCursorPosition();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryShoot();
        }
    }

    private void UpdateCursorPosition()
    {
        if (worldCamera == null)
        {
            Debug.LogError("CursorAimController: field 'World Camera' kosong! Kursor tidak akan bergerak sampai ini di-assign (drag Main Camera ke field itu).");
            return;
        }

        // Pakai API Input lama KHUSUS buat baca posisi mouse -> lebih reliable di tab Game
        // yang di-dock/nggak fullscreen di Editor (Input System baru punya known-issue di kasus ini).
        // Deteksi klik tetap pakai Input System baru (Mouse.current), itu tidak kena masalah yang sama.
        Vector2 mouseScreenPos = UnityEngine.Input.mousePosition;

        // Perlin noise -> goyangan halus & mengalir, dihitung di ruang PIXEL layar dulu
        float offsetX = (Mathf.PerlinNoise(noiseSeedX + Time.time * swaySpeed, 0f) - 0.5f) * 2f * swayAmplitude;
        float offsetY = (Mathf.PerlinNoise(0f, noiseSeedY + Time.time * swaySpeed) - 0.5f) * 2f * swayAmplitude;

        Vector2 swayedScreenPos = mouseScreenPos + new Vector2(offsetX, offsetY);

        // kunci posisi supaya kursor tidak pernah keluar batas layar, walau mouse diseret jauh keluar jendela
        swayedScreenPos.x = Mathf.Clamp(swayedScreenPos.x, 0f, Screen.width);
        swayedScreenPos.y = Mathf.Clamp(swayedScreenPos.y, 0f, Screen.height);

        // konversi ke world position, di bidang Z yang sama dengan background/pohon/rusa (Z = 0)
        float distanceToPlane = Mathf.Abs(worldCamera.transform.position.z);
        Vector3 worldPos = worldCamera.ScreenToWorldPoint(new Vector3(swayedScreenPos.x, swayedScreenPos.y, distanceToPlane));
        worldPos.z = 0f;

        transform.position = worldPos;
    }

    private void TryShoot()
    {
        if (DeerManager.Instance == null || aimCollider == null) return;

        foreach (DeerInstance deer in DeerManager.Instance.ActiveDeer)
        {
            if (!deer.isAlive || deer.visualObject == null) continue;
            if (!deer.visualObject.activeInHierarchy) continue; // rusa di area yang lagi tidak kelihatan, skip

            SpriteRenderer deerSr = deer.visualObject.GetComponentInChildren<SpriteRenderer>();
            if (deerSr == null) continue;

            // Bandingkan AREA kolider kursor vs AREA gambar (bounds sprite) rusa -> kena di bagian
            // manapun dari fotonya (termasuk pinggir), tanpa perlu nambah Collider2D ke prefab rusa.
            if (aimCollider.bounds.Intersects(deerSr.bounds))
            {
                Debug.Log("aww, pacarku nokotan");
                return; // satu tembakan maksimal kena satu rusa
            }
        }
    }
}