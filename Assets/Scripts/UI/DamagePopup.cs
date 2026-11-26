using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Всплывающая цифра урона в мире. Объекты переиспользуются пулом,
/// создаются кодом: префаб и материал не нужны.
/// </summary>
public static class DamagePopup
{
    private const int FontSize = 6;
    private const float Lifetime = 0.85f;
    private const float RiseSpeed = 1.6f;
    private const float WorldScale = 0.22f;

    private static readonly Queue<DamagePopupInstance> Pool = new Queue<DamagePopupInstance>(16);
    private static Transform holder;

    public static void Spawn(Vector3 worldPoint, float damage, Color color)
    {
        Spawn(worldPoint, Mathf.Max(1f, Mathf.Round(damage)).ToString("0"), color);
    }

    public static void Spawn(Vector3 worldPoint, string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        DamagePopupInstance instance = Take();
        if (instance == null) return;

        instance.Play(worldPoint + Vector3.up * 1.4f, text, color, Lifetime, RiseSpeed);
    }

    private static DamagePopupInstance Take()
    {
        while (Pool.Count > 0)
        {
            DamagePopupInstance cached = Pool.Dequeue();
            if (cached != null) return cached;
        }

        if (holder == null)
        {
            holder = new GameObject("[DamagePopups]").transform;
            Object.DontDestroyOnLoad(holder.gameObject);
        }

        GameObject go = new GameObject("DamagePopup");
        go.transform.SetParent(holder, false);
        go.transform.localScale = Vector3.one * WorldScale;

        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.fontSize = FontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;

        return go.AddComponent<DamagePopupInstance>();
    }

    internal static void Release(DamagePopupInstance instance)
    {
        if (instance == null) return;
        Pool.Enqueue(instance);
    }
}

/// <summary>Одна цифра: поднимается вверх, гаснет и возвращается в пул.</summary>
public class DamagePopupInstance : MonoBehaviour
{
    private TextMeshPro text;
    private float lifetime;
    private float riseSpeed;
    private float elapsed;

    private void Awake()
    {
        text = GetComponent<TextMeshPro>();
    }

    internal void Play(Vector3 worldPoint, string value, Color color, float duration, float rise)
    {
        transform.position = worldPoint;
        lifetime = duration;
        riseSpeed = rise;
        elapsed = 0f;

        if (text == null) text = GetComponent<TextMeshPro>();
        if (text != null)
        {
            text.text = value;
            text.color = color;
        }

        gameObject.SetActive(true);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lifetime));
        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

        Camera camera = Camera.main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);

        if (text != null)
        {
            Color color = text.color;
            color.a = 1f - t * t;
            text.color = color;
        }

        if (t < 1f) return;

        gameObject.SetActive(false);
        DamagePopup.Release(this);
    }
}
