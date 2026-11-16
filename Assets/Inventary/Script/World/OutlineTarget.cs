using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Контур объекта для наведения мышью. Один раз создаёт вывернутые копии мешей
/// и дальше только включает их — без пост-обработки и без второго материала на исходнике.
/// </summary>
public class OutlineTarget : MonoBehaviour
{
    private const string CopyName = "Outline";

    [Header("Материал")]
    [SerializeField] private Material outlineMaterial;

    private readonly List<GameObject> copies = new List<GameObject>();
    private bool built;
    private bool warnedNoMaterial;

    private void Awake()
    {
        EnsureBuilt();
    }

    /// <summary>Показывает или прячет контур. Повторный вызов не пересоздаёт копии.</summary>
    public void Show(bool visible)
    {
        EnsureBuilt();

        for (int i = 0; i < copies.Count; i++)
        {
            if (copies[i] != null) copies[i].SetActive(visible);
        }
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;

        if (outlineMaterial == null)
        {
            WarnNoMaterial();
            return;
        }

        // Копии нужны и для выключенных мешей: контейнер может состоять из частей.
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null) continue;
            if (filter.name == CopyName) continue;

            MeshRenderer source = filter.GetComponent<MeshRenderer>();
            if (source == null) continue;

            // Партиклы не имеют меша для контура, но их дочерние объекты могут его иметь.
            if (filter.GetComponent<ParticleSystem>() != null) continue;

            copies.Add(CreateCopy(filter));
        }
    }

    private GameObject CreateCopy(MeshFilter source)
    {
        GameObject copy = new GameObject(CopyName);
        Transform copyTransform = copy.transform;
        copyTransform.SetParent(source.transform, false);
        copyTransform.localPosition = Vector3.zero;
        copyTransform.localRotation = Quaternion.identity;
        copyTransform.localScale = Vector3.one;

        MeshFilter meshFilter = copy.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = source.sharedMesh;

        MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = outlineMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        copy.SetActive(false);
        return copy;
    }

    private void WarnNoMaterial()
    {
        if (warnedNoMaterial) return;

        warnedNoMaterial = true;
        Debug.LogWarning("[OutlineTarget] Не назначен material — контур не будет показан.");
    }
}
