using System.Collections.Generic;
using UnityEngine;

// Helper statis, dipakai bareng-bareng oleh MapGenerator (buat naruh pohon) dan
// DeerManager (buat naruh rusa), supaya logic "cari posisi kosong dalam panel"
// tidak duplikat dan hasilnya konsisten di semua tempat.
public static class AreaLayoutUtility
{
    // Menghitung setengah tinggi visual sebuah prefab (dalam world unit, sudah memperhitungkan scale-nya),
    // dipakai supaya badan objek (bukan cuma titik tengahnya) dijamin tidak kepotong tepi kamera.
    public static float GetSpriteHalfHeight(GameObject prefab, Vector3 scale)
    {
        if (prefab == null) return 0f;
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 0f;
        return sr.sprite.bounds.size.y * 0.5f * scale.y;
    }

    // panelSize          : ukuran panel area (lebar x tinggi)
    // occupied           : posisi-posisi yang sudah dipakai objek lain (biar tidak numpuk)
    // horizontalPadding  : jarak dari tepi kiri/kanan yang tidak boleh dipakai
    // minDistance        : jarak minimum antar objek dalam 1 area
    // groundOffsetMin/Max: jarak (world unit) dari TEPI BAWAH panel -> menentukan "pita tanah" tempat
    //                       TITIK ACUAN objek boleh berada. X tetap random bebas kiri-kanan.
    // objectHalfHeight   : setengah tinggi visual objek (dari GetSpriteHalfHeight) -> dipakai buat mepetin
    //                       batas atas/bawah pita supaya BADAN objek (bukan cuma titik tengahnya) tidak
    //                       pernah kepotong tepi atas/bawah kamera.
    public static Vector2 GetRandomFreePosition(
        Vector2 panelSize,
        List<Vector2> occupied,
        float horizontalPadding,
        float minDistance,
        float groundOffsetMin,
        float groundOffsetMax,
        float objectHalfHeight = 0f)
    {
        float halfW = Mathf.Max(panelSize.x * 0.5f - horizontalPadding, 0.05f);
        float halfH = panelSize.y * 0.5f;
        float bottomY = -halfH;

        float yMin = bottomY + Mathf.Min(groundOffsetMin, groundOffsetMax);
        float yMax = bottomY + Mathf.Max(groundOffsetMin, groundOffsetMax);

        // geser batas biar BADAN objek (bukan cuma titik tengahnya) tidak nembus tepi atas/bawah kamera
        yMin = Mathf.Max(yMin, bottomY + objectHalfHeight);
        yMax = Mathf.Min(yMax, halfH - objectHalfHeight);

        if (yMin > yMax)
        {
            // kalau "pita tanah" kamu di-set terlalu sempit buat muat objek setinggi ini,
            // jatuhkan ke satu nilai aman di tengah supaya tidak error, daripada crash Random.Range
            float mid = (yMin + yMax) * 0.5f;
            yMin = mid;
            yMax = mid;
        }

        Vector2 pos = Vector2.zero;
        bool ok = false;
        int attempts = 0;

        while (!ok && attempts < 30)
        {
            pos = new Vector2(Random.Range(-halfW, halfW), Random.Range(yMin, yMax));
            ok = true;
            foreach (var other in occupied)
            {
                if (Vector2.Distance(pos, other) < minDistance)
                {
                    ok = false;
                    break;
                }
            }
            attempts++;
        }
        return pos;
    }
}