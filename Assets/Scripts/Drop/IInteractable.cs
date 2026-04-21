using UnityEngine;
public interface IInteractable
{
    void TakeDamage(float damage, Vector3 hitPoint);
    ResourceType GetResourceType();
    int GetLevel();
}