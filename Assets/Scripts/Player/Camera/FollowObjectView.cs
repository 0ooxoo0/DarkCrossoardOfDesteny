using UnityEngine;

public class FollowObjectView : MonoBehaviour
{
    [SerializeField]
    private Transform fpsTransformView;
    [SerializeField]
    private Transform thirdPersonTransformView;
    [SerializeField]
    private bool isFpsView = true;

    private void FixedUpdate()
    {
        //if(Input.GetKeyDown(KeyCode.Tab))
        //{
        //    isFpsView = !isFpsView;
        //}
        // Обращение к пустой ссылке бросает исключение каждый FixedUpdate и
        // забивает консоль: берём назначенную цель и молчим, если нет ни одной.
        Transform target = isFpsView ? fpsTransformView : thirdPersonTransformView;
        if (target == null) target = thirdPersonTransformView != null ? thirdPersonTransformView : fpsTransformView;
        if (target == null) return;

        transform.position = target.position;
    }
}
