using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class RemoteLoader : MonoBehaviour
{
    [Tooltip("Впишите сюда точный адрес ассета из окна Addressables Groups")]
    public string assetAddress = "Assets/Assets/Stylized Medieval Kingdom...";

    [Tooltip("Куда заспавнить объект")]
    public Transform spawnPoint;

    void Start()
    {
        // Запускаем асинхронную загрузку
        Addressables.LoadAssetAsync<GameObject>(assetAddress).Completed += OnAssetLoaded;
    }

    void OnAssetLoaded(AsyncOperationHandle<GameObject> handle)
    {
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