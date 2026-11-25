using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class RemoteLoader : MonoBehaviour
{
    [Tooltip("Впишите сюда точный адрес ассета из окна Addressables Groups")]
    public string assetAddress;

    [Tooltip("Куда заспавнить объект")]
    public Transform spawnPoint;

    void Start()
    {
        if (string.IsNullOrWhiteSpace(assetAddress))
        {
            Debug.LogError(name + ": assetAddress is null or empty.", this);
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError(name + ": spawnPoint is not assigned.", this);
            return;
        }

        Addressables.LoadAssetAsync<GameObject>(assetAddress).Completed += OnAssetLoaded;
    }

    void OnAssetLoaded(AsyncOperationHandle<GameObject> handle)
    {
        if (spawnPoint == null)
        {
            Debug.LogError(name + ": spawnPoint is not assigned.", this);
            return;
        }

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log("Ассет успешно загружен с сервера!");
            // Создаем объект в сцене
            Instantiate(handle.Result, spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            Debug.LogError("Ошибка загрузки ассета: " + handle.OperationException);
        }
    }
}