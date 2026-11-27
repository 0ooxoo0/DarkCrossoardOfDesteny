using UnityEngine;

/// <summary>
/// Точка прицела из центра экрана. Общий код для AbilitySystem и демо-кастера.
/// Камеру берём через PlayerController.ResolveViewCamera: Camera.main в сцене выключен.
/// </summary>
public static class AimResolver
{
    private static readonly RaycastHit[] Buffer = new RaycastHit[16];

    /// <summary>hintFallback — направление, если камеры нет (тесты, сцены без камеры).</summary>
    public static Vector3 Resolve(Transform casterRoot, float range, LayerMask mask,
        float aimHeight, Vector3 hintFallback)
    {
        Vector3 origin = casterRoot != null ? casterRoot.position + Vector3.up * aimHeight : Vector3.zero;
        if (casterRoot == null) return origin;

        Camera cam = PlayerController.ResolveViewCamera();
        if (cam == null) return origin + hintFallback.normalized * Mathf.Max(1f, range);

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float maxDistance = Mathf.Max(1f, range);

        int count = Physics.RaycastNonAlloc(ray, Buffer, maxDistance, mask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        Vector3 point = ray.GetPoint(maxDistance);
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider collider = Buffer[i].collider;
            if (collider == null) continue;

            // Свой коллайдер (модель персонажа) препятствием не считается:
            // слой игрока = 0, поэтому маска его не исключает.
            if (casterRoot != null && collider.transform.root == casterRoot) continue;
            if (Buffer[i].distance >= best) continue;

            best = Buffer[i].distance;
            point = Buffer[i].point;
            found = true;
        }

        // Ничего не задели — точка на максимальной дальности вдоль луча.
        return found ? point : ray.GetPoint(maxDistance);
    }
}
