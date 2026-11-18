using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Печь алхимии в мире: держит рецепты и конфиг, показывает подсказку и по E
/// открывает окно крафта в режиме печи. Не открывает окно повторно.
/// </summary>
[DisallowMultipleComponent]
public class AlchemyStation : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private AlchemyConfig config;
    [SerializeField] private List<AlchemyRecipe> recipes = new List<AlchemyRecipe>();

    [Header("Взаимодействие")]
    [SerializeField, Min(0.5f)] private float interactRadius = 3f;
    [Tooltip("Подсказка у печи. Пусто — стандартная.")]
    [SerializeField] private string hintText = "E — алхимия";

    private Transform player;
    private bool playerInside;
    private bool hintShown;

    public IReadOnlyList<AlchemyRecipe> Recipes => recipes;
    public AlchemyConfig Config => config;
    public bool PlayerInside => playerInside;

    private void Start()
    {
        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) player = health.transform;
        else
        {
            var controller = FindFirstObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }
    }

    private void OnDisable()
    {
        HideHint();
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        bool inside = delta.sqrMagnitude <= interactRadius * interactRadius;

        if (inside != playerInside)
        {
            playerInside = inside;
            if (inside) ShowHint();
            else HideHint();
        }

        if (!playerInside) return;

        AlchemyPanelUI panel = ResolvePanel();
        if (panel == null) return;

        if (playerInside && !panel.IsOpen && !hintShown)
            ShowHint();

        if (!CultivationInput.Pressed(Key.E, KeyCode.E)) return;

        if (panel.IsOpen)
        {
            panel.Close();
            ShowHint();
            return;
        }

        CultivationUIRoot ui = CultivationUIRoot.Instance;
        if (ui != null && ui.IsInputBlocked) return;

        panel.OpenForStation(this);

        if (panel.IsOpen)
        {
            HideHint();
        }
    }

    private void ShowHint()
    {
        AlchemyPanelUI panel = ResolvePanel();
        if (panel == null) return;

        panel.SetHint(hintText);
        hintShown = true;
    }

    private void HideHint()
    {
        if (!hintShown) return;

        AlchemyPanelUI panel = ResolvePanel();
        if (panel != null) panel.SetHint(string.Empty);
        hintShown = false;
    }

    private static AlchemyPanelUI ResolvePanel()
    {
        CultivationUIRoot ui = CultivationUIRoot.Instance;
        return ui != null ? ui.Alchemy : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.55f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
