using UnityEngine;

/// <summary>
/// Editable attack timing on an Animator state.
/// Add this StateMachineBehaviour to an attack state, then tune hitbox/combo windows here.
/// The receiver (PlayerController / EnemyCombatAI) opens and closes the real CombatHitbox.
/// </summary>
public class CombatAnimationWindow : StateMachineBehaviour
{
    [Header("Hitbox window (normalized time)")]
    [Range(0f, 1f)] public float hitboxStart = 0.25f;
    [Range(0f, 1f)] public float hitboxEnd = 0.55f;

    [Header("Combo input window (normalized time)")]
    [Range(0f, 1f)] public float comboWindowStart = 0.35f;
    [Range(0f, 1f)] public float comboWindowEnd = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool logWindows;

    private ICombatAnimationReceiver receiver;
    private bool hitboxOpen;
    private bool comboOpen;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        receiver = animator.GetComponentInParent<ICombatAnimationReceiver>();
        hitboxOpen = false;
        comboOpen = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (receiver == null) return;

        float t = Mathf.Repeat(stateInfo.normalizedTime, 1f);

        if (!hitboxOpen && t >= hitboxStart && t <= hitboxEnd)
        {
            hitboxOpen = true;
            receiver.OnAttackWindowOpen();
            if (logWindows) Debug.Log("[Combat] Hitbox OPEN at " + t.ToString("F2") + " on " + animator.name);
        }
        else if (hitboxOpen && t > hitboxEnd)
        {
            hitboxOpen = false;
            receiver.OnAttackWindowClose();
            if (logWindows) Debug.Log("[Combat] Hitbox CLOSE at " + t.ToString("F2") + " on " + animator.name);
        }

        if (!comboOpen && t >= comboWindowStart && t <= comboWindowEnd)
        {
            comboOpen = true;
            receiver.OnComboWindowOpen();
        }
        else if (comboOpen && t > comboWindowEnd)
        {
            comboOpen = false;
            receiver.OnComboWindowClose();
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (receiver == null) return;

        if (hitboxOpen)
        {
            hitboxOpen = false;
            receiver.OnAttackWindowClose();
        }

        if (comboOpen)
        {
            comboOpen = false;
            receiver.OnComboWindowClose();
        }
    }
}
