using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Taruh di GameObject "Player".
// Pakai package Input System (baru) -- pastikan Active Input Handling di
// Project Settings > Player masih "Input System Package (New)" atau "Both".
public class PlayerController : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public ScreenFader screenFader;

    private bool isTransitioning = false;

    // Mapping tombol -> arah gerak (row, col). Pakai Dictionary supaya tidak ada if-else
    // berantai, dan gampang nambah tombol baru (mis. arrow key) tanpa ubah logic Update().
    private static readonly Dictionary<Key, Vector2Int> DirectionKeys = new Dictionary<Key, Vector2Int>
    {
        { Key.W, new Vector2Int(-1, 0) }, // atas  -> row berkurang
        { Key.S, new Vector2Int(1, 0) },  // bawah -> row bertambah
        { Key.A, new Vector2Int(0, -1) }, // kiri  -> col berkurang
        { Key.D, new Vector2Int(0, 1) },  // kanan -> col bertambah
    };

    private void Update()
    {
        if (isTransitioning) return;
        if (Keyboard.current == null) return; // jaga-jaga kalau belum ada keyboard terdeteksi

        foreach (var pair in DirectionKeys)
        {
            if (Keyboard.current[pair.Key].wasPressedThisFrame)
            {
                TryMove(pair.Value);
                break; // satu input per frame cukup, area cuma bisa pindah 1 langkah tiap kali tekan
            }
        }
    }

    private void TryMove(Vector2Int direction)
    {
        // Kalau area tujuan tidak ada / tidak bisa diakses: tidak terjadi apa-apa, tanpa transisi.
        if (mapGenerator.CanMove(direction))
        {
            StartCoroutine(MoveRoutine(direction));
        }
    }

    private IEnumerator MoveRoutine(Vector2Int direction)
    {
        isTransitioning = true;

        yield return StartCoroutine(screenFader.FadeOut());
        mapGenerator.MoveTo(direction);
        yield return StartCoroutine(screenFader.FadeIn());

        isTransitioning = false;
    }
}