using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Невидимая область перед камерой: собирает контейнеры лута, попавшие в кадр.
/// Нужна клавиатурному подбору, когда мышью никто не пользуется.
/// </summary>
[DisallowMultipleComponent]
public class LootViewArea : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Camera viewCamera;

    [Header("Область")]
    [Tooltip("Дальность обзора вдоль взгляда камеры.")]
    [SerializeField, Min(0.5f)] private float viewDistance = 12f;
    [Tooltip("Полуширина области на дальней границе, метры.")]
    [SerializeField, Min(0.5f)] private float viewHalfWidth = 6f;
    [Tooltip("Полувысота области на дальней границе, метры.")]
    [SerializeField, Min(0.5f)] private float viewHalfHeight = 4f;
    [Tooltip("Насколько близко к камере начинается область.")]
    [SerializeField, Min(0f)] private float nearOffset = 0.5f;

    [Header("Слои")]
    [SerializeField] private LayerMask containerMask = ~0;

    private static readonly Collider[] Buffer = new Collider[64];
    private readonly List<WorldLootContainer> visible = new List<WorldLootContainer>();

    /// <summary>Контейнеры в области, ближайшие первыми.</summary>
    public IReadOnlyList<WorldLootContainer> Visible => visible;

    /// <summary>Камера, по которой строится область.</summary>
    public Camera ViewCamera => viewCamera;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = PlayerController.ResolveViewCamera();
    }

    /// <summary>Пересобирает список. Вызывается владельцем, а не каждым Update.</summary>
    public void Refresh()
    {
        visible.Clear();

        if (viewCamera == null)
        {
            viewCamera = PlayerController.ResolveViewCamera();
            if (viewCamera == null) return;
        }

        // Призмы физики нет: проверяем попадание точки в усечённую пирамиду обзора.
        // Контейнер — небольшой объект, проверки центра достаточно и это дешевле коллайдеров.
        WorldLootContainer[] all = FindObjectsByType<WorldLootContainer>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Transform camTransform = viewCamera.transform;
        Vector3 origin = camTransform.position;
        Vector3 forward = camTransform.forward;
        Vector3 right = camTransform.right;
        Vector3 up = camTransform.up;

        for (int i = 0; i < all.Length; i++)
        {
            WorldLootContainer container = all[i];
            if (container == null || container.IsEmpty) continue;

            Vector3 delta = container.transform.position - origin;
            float depth = Vector3.Dot(delta, forward);
            if (depth < nearOffset || depth > viewDistance) continue;

            // Границы расширяются линейно — область выглядит как конус обзора.
            float t = Mathf.InverseLerp(nearOffset, viewDistance, depth);
            float halfWidth = Mathf.Lerp(0.5f, viewHalfWidth, t);
            float halfHeight = Mathf.Lerp(0.5f, viewHalfHeight, t);

            if (Mathf.Abs(Vector3.Dot(delta, right)) > halfWidth) continue;
            if (Mathf.Abs(Vector3.Dot(delta, up)) > halfHeight) continue;

            visible.Add(container);
        }

        // Ближние первыми: клавиатура выбирает то, что под ногами, а не за горизонтом.
        visible.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - origin)
                .CompareTo(Vector3.SqrMagnitude(b.transform.position - origin)));
    }

    private void OnDrawGizmosSelected()
    {
        Camera cam = viewCamera != null ? viewCamera : PlayerController.ResolveViewCamera();
        if (cam == null) return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(cam.transform.position, cam.transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(
            new Vector3(0f, 0f, (nearOffset + viewDistance) * 0.5f),
            new Vector3(viewHalfWidth * 2f, viewHalfHeight * 2f, viewDistance - nearOffset));
    }
}
