# Лут-контейнеры, подбор и визуальная экипировка — план реализации

> **Для агента-исполнителя:** обязательный скилл — `superpowers:executing-plans`
> (native) или `superpowers:subagent-driven-development`. Шаги отмечаются
> чекбоксами. План реализует утверждённый дизайн
> `docs/superpowers/specs/2026-11-15-loot-containers-equipment-visuals-design.md`.

**Goal:** Дать игроку полный цикл предметов: один `ItemEffect` с лутом на земле,
список при наведении, подбор с подходом, визуальная смена одежды на персонаже,
выброс в мир и удаление из инвентаря — не меняя `PlayerController` и систему боя.

**Architecture:** `Inventory` остаётся источником истины. `CharacterEquipmentVisuals`
только включает/выключает уже существующие объекты одежды `BasicHero_M` по событиям
`Inventory`; `PlayerEquipmentBridge` — подписчик. `WorldLootContainer` хранит записи
`{ItemData, quantity}`, `WorldLootService` создаёт контейнеры и сливает выброс.
`LootPointerController` делает луч мышью, `LootPanelUI` рисует список,
`LootPickupController` забирает вблизи или ведёт персонажа. Сцена собирается
идемпотентным Editor-скриптом `InventorySetup`.

**Tech Stack:** Unity 6000.2.10f1, Assembly-CSharp без asmdef, URP 17.6, uGUI,
TextMeshPro, Input System, `NavMeshAgent`, `JsonUtility`.

**Spec:** `docs/superpowers/specs/2026-11-15-loot-containers-equipment-visuals-design.md`

## Global Constraints

- **Не менять:** `Assets/Scripts/Player/PlayerController.cs`,
  `Assets/Scripts/DestructibleObject.cs`, `Assets/Scripts/Cultivation/**`,
  `Assets/Scripts/Combat/WeaponManager.cs`, `WeaponData.cs`, `AbilitySystem.cs`.
- **Не добавлять** теги и слои в `ProjectSettings/TagManager.asset`.
- **Не менять** модель персонажа, аватар и `Animator`; второго скелета не создавать.
- **Инвентарь не знает о сцене:** визуалы — подписчик на события `Inventory`.
- **Курсор** в геймплее всегда свободный и видимый; после Esc/Alt наведение работает.
- **Один класс — один файл**, PascalCase, `[SerializeField] private`, без магических
  чисел и строк; XML-комментарии на русском.
- **Проверка:** проект без рабочего Test Framework. Цикл =
  `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo` (0 ошибок) + ручная
  проверка в Play mode. Юнит-тестов не добавляем.
- **Сцену руками не править:** только `Tools → Inventory → Setup Test Data`.
- **Идемпотентность:** повторный запуск сетапа не плодит объекты и не теряет ссылки.

## Review Focus

1. **Скиннинг после переноса одежды.** Перенос объектов в папки слотов не должен
   ломать `SkinnedMeshRenderer`: тело и одежда двигаются со скелетом, нет T-позы,
   дублей и висящих в воздухе деталей.
2. **Клик по луту не атакует.** Клик по сфере и по кнопкам панели не запускает
   атаку, а атака по врагам и деревьям продолжает работать.
3. **Курсор после меню.** После Esc и Alt мышь остаётся свободной и видимой,
   наведение на лут продолжает работать.
4. **Подход.** Персонаж перемещается к контейнеру, а не стоит на месте; WASD
   отменяет; по прибытии, таймауту и отмене скорость и `Speed` сбрасываются,
   персонаж не «уезжает» и не застревает.
5. **Полный инвентарь и выброс.** При нехватке места берётся часть, остаток остаётся;
   контейнер исчезает только при нуле; выброс удаляет предмет из инвентаря только
   после успешного создания контейнера (нет потерь и дублей).

---

### Task 1: Слоты и данные предмета

**Files:**
- Modify: `Assets/Inventary/Script/SO/EquipmentSlotType.cs`
- Modify: `Assets/Inventary/Script/SO/ItemData.cs`
- Modify: `Assets/Inventary/Script/SO/ItemTypeInfo.cs`
- Modify: `Assets/Inventary/Script/Inventory.cs:444-449`
- Modify: `Assets/Editor/InventorySetup.cs:483-491`

**Interfaces:**
- Produces: `EquipmentSlotType { Weapon=0, Armor=1, Helmet=2, Pants=3, Accessory=4 }`
- Produces: `ItemData.equipSlot`, `ItemData.equipVisualId`, `ItemData.EquipmentSlot`
- Produces: `Inventory.IsCompatible(ItemData, EquipmentSlotType)` сравнивает слоты

- [ ] **Step 1: Заменить `Boots` на `Pants`**

```csharp
public enum EquipmentSlotType
{
    Weapon = 0,
    Armor = 1,
    Helmet = 2,
    Pants = 3,
    Accessory = 4
}
```

- [ ] **Step 2: Добавить поля в `ItemData`**

```csharp
[Header("Equipment")]
[Tooltip("Слот, в который надевается предмет. Для оружия всегда Weapon.")]
public EquipmentSlotType equipSlot = EquipmentSlotType.Armor;

[Tooltip("Имя объекта одежды на персонаже. Пусто — без изменения внешности.")]
public string equipVisualId;
```

Геттер:

```csharp
public EquipmentSlotType EquipmentSlot =>
    type == ItemType.Weapon ? EquipmentSlotType.Weapon : equipSlot;
```

В `OnValidate` добавить защиту:

```csharp
if (type == ItemType.Weapon)
    equipSlot = EquipmentSlotType.Weapon;
else if (type == ItemType.Armor && equipSlot == EquipmentSlotType.Weapon)
    equipSlot = EquipmentSlotType.Armor;
```

- [ ] **Step 3: Подписи слотов и список слотов**

В `EquipmentSlotInfo.GetDisplayName` заменить `Boots` на `Pants` → «Штаны».
В `Assets/Editor/InventorySetup.cs` в массиве `slotTypes` заменить
`EquipmentSlotType.Boots` на `EquipmentSlotType.Pants`.

- [ ] **Step 4: Совместимость в `Inventory`**

```csharp
private static bool IsCompatible(ItemData item, EquipmentSlotType slotType)
{
    if (item == null) return false;
    return item.EquipmentSlot == slotType;
}
```

- [ ] **Step 5: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок (старые чужие предупреждения допустимы).

- [ ] **Step 6: Commit**

```bash
git add Assets/Inventary/Script/SO/EquipmentSlotType.cs Assets/Inventary/Script/SO/ItemData.cs Assets/Inventary/Script/SO/ItemTypeInfo.cs Assets/Inventary/Script/Inventory.cs Assets/Editor/InventorySetup.cs
git commit -m "feat(inventory): add pants slot and explicit equipment slot"
```

---


### Task 2: Визуальная экипировка персонажа

**Files:**
- Create: `Assets/Inventary/Script/Character/CharacterEquipmentVisuals.cs`
- Create: `Assets/Inventary/Script/Character/PlayerEquipmentBridge.cs`

**Interfaces:**
- Consumes: `Inventory.OnItemEquipped`, `Inventory.OnItemUnequipped`,
  `Inventory.Equipped`, `ItemData.equipVisualId`
- Produces:
  - `CharacterEquipmentVisuals.Apply(EquipmentSlotType slot, string visualId)`
  - `CharacterEquipmentVisuals.GetVisualIds(EquipmentSlotType slot)`
  - `PlayerEquipmentBridge.Sync()`

- [ ] **Step 1: `CharacterEquipmentVisuals`**

```csharp
public class CharacterEquipmentVisuals : MonoBehaviour
{
    [Serializable]
    public class EquipmentVisualEntry
    {
        public EquipmentSlotType slot;
        public string visualId;
        public GameObject visual;
    }

    [SerializeField] private List<EquipmentVisualEntry> visuals = new();

    public IReadOnlyList<EquipmentVisualEntry> Visuals => visuals;
    public List<string> GetVisualIds(EquipmentSlotType slot);
    public void Apply(EquipmentSlotType slot, string visualId);
}
```

`Apply`: для каждой записи своего слота `visual.SetActive(!string.IsNullOrEmpty(visualId) && entry.visualId == visualId)`.
`Awake`: выключить все `visual`. `GetVisualIds`: уникальные имена слота по порядку.

- [ ] **Step 2: `PlayerEquipmentBridge`**

```csharp
public class PlayerEquipmentBridge : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private CharacterEquipmentVisuals visuals;

    public void Sync();
}
```

- `OnEnable` подписывается на `OnItemEquipped`/`OnItemUnequipped`;
  `Start` вызывает `Sync` (после всех `Awake`, чтобы не перезаписать визуалы
  выключением по умолчанию). Оба метода null-safe.
- `Sync`: для каждого слота `EquipmentSlotType` берёт `inventory.GetEquipped(slot)`
  и вызывает `visuals.Apply(slot, item != null ? item.equipVisualId : null)`.
- `HandleEquipped`: `visuals.Apply(slot, item != null ? item.equipVisualId : null)`.
- `HandleUnequipped`: `visuals.Apply(slot, null)`.

- [ ] **Step 3: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 4: Commit**

```bash
git add Assets/Inventary/Script/Character
git commit -m "feat(inventory): character equipment visuals"
```

---


### Task 3: Выпадающий список визуалов в инспекторе `ItemData`

**Files:**
- Create: `Assets/Inventary/Editor/ItemDataEditor.cs`

**Interfaces:**
- Consumes: `ItemData.equipVisualId`, `CharacterEquipmentVisuals.GetVisualIds(slot)`
  (появится в Task 2; до него редактор показывает текстовое поле)
- Produces: `[CustomEditor(typeof(ItemData))] ItemDataEditor`

- [ ] **Step 1: Создать редактор**

```csharp
[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
    public override void OnInspectorGUI();
}
```

Поведение:
- `DrawDefaultInspector()`.
- Если `ItemData.type == ItemType.Armor` — найти в открытой сцене
  `CharacterEquipmentVisuals` (`FindObjectsByType(..., FindObjectsInactive.Include, ...)`);
  собрать `GetVisualIds(equipSlot)`; нарисовать `EditorGUILayout.Popup` с первым
  пунктом «— без визуала —»; записать выбранное в `equipVisualId`.
- Если компонента в сцене нет — обычное текстовое поле с подсказкой.
- Изменения проводить через `serializedObject` + `ApplyModifiedProperties()`.

- [ ] **Step 2: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 3: Commit**

```bash
git add Assets/Inventary/Editor/ItemDataEditor.cs
git commit -m "feat(inventory): item visual dropdown in inspector"
```

---


### Task 4: Ядро лута — записи, контейнер, сервис

**Files:**
- Create: `Assets/Inventary/Script/World/LootEntry.cs`
- Create: `Assets/Inventary/Script/World/WorldLootContainer.cs`
- Create: `Assets/Inventary/Script/World/WorldLootService.cs`

**Interfaces:**
- Consumes: `Inventory.GetFreeSpace(ItemData)`, `Inventory.AddItem(ItemData, int)`
- Produces:
  - `LootEntry { ItemData item; int quantity; bool IsValid; }`
  - `WorldLootContainer.Add(ItemData, int)`,
    `TryTake(ItemData, int, Inventory)`, `TryTakeAll(Inventory)`,
    `IsEmpty`, `Entries`, `OnChanged`, `OnEmptied`
  - `WorldLootService.TryDrop(ItemData, int, Transform)`,
    `SpawnContainer(Vector3)`, `FindNearest(Vector3, float)`

- [ ] **Step 1: `LootEntry`**

```csharp
[Serializable]
public struct LootEntry
{
    public ItemData item;
    public int quantity;

    public LootEntry(ItemData item, int quantity);
    public bool IsValid => item != null && quantity > 0;
}
```

- [ ] **Step 2: `WorldLootContainer`**

```csharp
public class WorldLootContainer : MonoBehaviour
{
    [SerializeField] private List<LootEntry> entries = new();

    public event Action<WorldLootContainer> OnChanged;
    public event Action<WorldLootContainer> OnEmptied;
    public IReadOnlyList<LootEntry> Entries => entries;
    public bool IsEmpty => entries.Count == 0;

    public void Add(ItemData item, int quantity);
    public bool TryTake(ItemData item, int quantity, Inventory inventory);
    public bool TryTakeAll(Inventory inventory);
}
```

`TryTake`:
- найти запись по `item`; если нет — `false`;
- `int space = inventory.GetFreeSpace(item);`
- `int take = Mathf.Min(quantity, Mathf.Min(entry.quantity, space));`
- если `take <= 0` — `false`;
- `inventory.AddItem(item, take)`; уменьшить `entry.quantity`; удалить пустую запись;
- `OnChanged?.Invoke(this)`; если `IsEmpty` — `OnEmptied?.Invoke(this)` и
  `Destroy(gameObject)`; вернуть `true`.

`TryTakeAll`: пройти по копии списка, для каждой записи взять максимум, что влезает;
вернуть `true`, если взято хоть что-то; опустевший контейнер уничтожить.
`Add` объединяет запись с тем же `ItemData`, иначе добавляет новую.

- [ ] **Step 3: `WorldLootService`**

```csharp
public class WorldLootService : MonoBehaviour
{
    [SerializeField] private GameObject containerPrefab;
    [SerializeField] private float dropDistance = 1.5f;
    [SerializeField] private float mergeRadius = 2.5f;
    [SerializeField] private float groundRayHeight = 5f;
    [SerializeField] private LayerMask groundMask = ~0;

    public bool TryDrop(ItemData item, int quantity, Transform dropper);
    public WorldLootContainer SpawnContainer(Vector3 worldPosition);
    public WorldLootContainer FindNearest(Vector3 position, float radius);
}
```

`TryDrop`:
- `Vector3 point = dropper.position + dropper.forward * dropDistance;`
- луч вниз из `point + Vector3.up * groundRayHeight` по `groundMask`; при попадании
  взять `hit.point`;
- `NavMesh.SamplePosition(point, out hit, mergeRadius, NavMesh.AllAreas)` — прижать к навмешу;
- `FindNearest(point, mergeRadius)`; если найден — `Add`, иначе `SpawnContainer`.
- Если `containerPrefab == null` — `Debug.LogWarning` и `false`.

`SpawnContainer`: `Instantiate(containerPrefab, worldPosition, Quaternion.identity)`,
вернуть `GetComponent<WorldLootContainer>()`; подписаться на `OnEmptied`, чтобы
убрать из внутреннего списка.

- [ ] **Step 4: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 5: Commit**

```bash
git add Assets/Inventary/Script/World/LootEntry.cs Assets/Inventary/Script/World/WorldLootContainer.cs Assets/Inventary/Script/World/WorldLootService.cs
git commit -m "feat(loot): container, entries and world loot service"
```

---


### Task 5: Outline

**Files:**
- Create: `Assets/Inventary/Shaders/Outline.shader`
- Create: `Assets/Inventary/Script/World/OutlineTarget.cs`

**Interfaces:**
- Produces: `OutlineTarget.Show(bool visible)`

- [ ] **Step 1: Шейдер**

```hlsl
Shader "Inventory/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.35, 1)
        _OutlineWidth ("Outline Width", Float) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 pos = v.positionOS.xyz + normalize(v.normalOS) * _OutlineWidth;
                o.positionCS = TransformObjectToHClip(pos);
                return o;
            }
            half4 frag (Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    Fallback Off
}
```

- [ ] **Step 2: `OutlineTarget`**

```csharp
public class OutlineTarget : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;

    public void Show(bool visible);
}
```

`Awake`: один раз собрать копии — для каждого `MeshFilter` в детях (включая
выключенные), у которого есть `MeshRenderer` и нет `ParticleSystem`, создать
дочерний объект `"Outline"` с `MeshFilter` (тот же `sharedMesh`) и `MeshRenderer`
с `outlineMaterial`, `shadowCastingMode = Off`, `receiveShadows = false`.
Копии выключены. `Show` включает/выключает копии; повторный вызов не пересоздаёт.

- [ ] **Step 3: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок. В Unity проверить, что шейдер импортировался без ошибок.

- [ ] **Step 4: Commit**

```bash
git add Assets/Inventary/Shaders/Outline.shader Assets/Inventary/Script/World/OutlineTarget.cs
git commit -m "feat(loot): inverted hull outline"
```

---


### Task 6: Подбор и подход

**Files:**
- Create: `Assets/Inventary/Script/World/LootTakeResult.cs`
- Create: `Assets/Inventary/Script/World/LootPickupController.cs`

**Interfaces:**
- Consumes: `WorldLootContainer.TryTake/TryTakeAll`, `Inventory.GetFreeSpace`,
  `NavMeshAgent`, `Animator`, `PlayerController.IsAttacking`
- Produces:
  - `enum LootTakeResult { Taken, Walking, NoSpace, Failed }`
  - `LootPickupController.RequestTake(WorldLootContainer, LootEntry)`
  - `LootPickupController.RequestTakeAll(WorldLootContainer)`
  - `LootPickupController.IsApproaching`, `CancelApproach(string)`,
    `event Action<string> OnStatus`

- [ ] **Step 1: Перечисление**

```csharp
public enum LootTakeResult
{
    Taken,
    Walking,
    NoSpace,
    Failed
}
```

- [ ] **Step 2: `LootPickupController`**

```csharp
[DefaultExecutionOrder(1000)]
public class LootPickupController : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float pickupRange = 2.5f;
    [SerializeField] private float approachTimeout = 8f;
    [SerializeField] private float noProgressTimeout = 2f;

    public event Action<string> OnStatus;
    public bool IsApproaching { get; }

    public LootTakeResult RequestTake(WorldLootContainer container, LootEntry entry);
    public LootTakeResult RequestTakeAll(WorldLootContainer container);
    public void CancelApproach(string reason = null);
}
```

Логика:
- `Awake`: взять `NavMeshAgent`, `Animator`, `PlayerController` с того же объекта;
  создать `PlayerControls`, подписать `Player.Move.performed` на отмену при
  `sqrMagnitude > 0.01f`. Определить, есть ли у аниматора параметр `speedParameter`
  (`animator.parameters`), иначе не трогать его.
- `RequestTake`/`RequestTakeAll`:
  - пустой контейнер/запись → `Failed`;
  - `GetFreeSpace(item) == 0` (для `TakeAll` — суммарно ноль) → `NoSpace`;
  - если расстояние ≤ `pickupRange` — забрать сразу (`TryTake`/`TryTakeAll`),
    вернуть `Taken` или `Failed`;
  - иначе начать подход, вернуть `Walking`.
- Подход (`Update`, порядок 1000):
  - если идёт атака (`PlayerController.IsAttacking`) — стоять, скорость 0;
  - `Vector3 to = container.transform.position - transform.position; to.y = 0;`
  - если `to.magnitude <= pickupRange` — остановиться и забрать;
  - `agent.velocity = to.normalized * agent.speed;`
  - `transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 10f * Time.deltaTime);`
  - `animator.SetFloat(speedParameter, 1f)` (если параметр есть);
  - таймаут `approachTimeout` и отсутствие прогресса `noProgressTimeout` → отмена.
- Завершение/отмена: `agent.velocity = Vector3.zero`, `Speed = 0`,
  `controls.Disable()`, `OnStatus` с причиной; контейнер исчез — отмена.
- `OnDisable`: `CancelApproach(null)` и освободить `PlayerControls`.

- [ ] **Step 3: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 4: Commit**

```bash
git add Assets/Inventary/Script/World/LootTakeResult.cs Assets/Inventary/Script/World/LootPickupController.cs
git commit -m "feat(loot): pickup controller with approach"
```

---


### Task 7: Панель лута

**Files:**
- Create: `Assets/Inventary/Script/UI/LootRowUI.cs`
- Create: `Assets/Inventary/Script/UI/LootPanelUI.cs`

**Interfaces:**
- Consumes: `WorldLootContainer.Entries/OnChanged/OnEmptied`,
  `LootPickupController.RequestTake/RequestTakeAll/OnStatus`, `LootTakeResult`
- Produces:
  - `LootRowUI.Bind(LootEntry entry, Action onTake)`
  - `LootPanelUI.Show(WorldLootContainer, Vector2 screenPoint)`, `Hide()`,
    `Refresh()`, `ShowMessage(string)`, `IsVisible`

- [ ] **Step 1: `LootRowUI`**

```csharp
public class LootRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image placeholder;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Button takeButton;

    public void Bind(LootEntry entry, Action onTake);
}
```

`Bind`: имя `entry.item.DisplayName`, количество `×N`, иконка или `[PLACEHOLDER]`,
`takeButton.onClick` очищается и вешает `onTake`.

- [ ] **Step 2: `LootPanelUI`**

```csharp
public class LootPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private LootRowUI rowPrefab;
    [SerializeField] private Button takeAllButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Inventory inventory;
    [SerializeField] private LootPickupController pickupController;
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);

    public bool IsVisible { get; }
    public void Show(WorldLootContainer container, Vector2 screenPoint);
    public void Hide();
    public void Refresh();
    public void ShowMessage(string message);
}
```

Поведение:
- `Show`: если контейнер тот же и панель уже видима — только обновить позицию;
  иначе запомнить контейнер, подписаться на `OnChanged`/`OnEmptied`, `Refresh`,
  расположить панель, включить `root`/`CanvasGroup` (`alpha = 1`,
  `blocksRaycasts = true`). Это позволяет вызывать `Show` каждый кадр для
  слежения за сферой без пересборки строк.
- `Refresh`: удалить старые строки, создать по `rowPrefab` на запись,
  `Bind(entry, () => Take(entry))`. Пустой контейнер → `Hide`.
- `Take`: `LootTakeResult result = pickupController.RequestTake(container, entry);`
  `Taken` → `Refresh`; `Walking` → `Hide`; `NoSpace` → `ShowMessage("В инвентаре нет места.")`;
  `Failed` → `ShowMessage("Не удалось забрать.")`.
- `takeAllButton` → `RequestTakeAll`, те же реакции.
- `ShowMessage`: текст в `messageText`; очищается при следующем `Refresh`.
- Позиция: `RectTransformUtility.ScreenPointToLocalPointInRectangle`
  относительно родителя панели, плюс `screenOffset`; прижать к границам
  (`Mathf.Clamp`) по ширине/высоте родителя; если панель не влезает справа —
  отзеркалить смещение по X.
- `OnEnable`: подписаться на `pickupController.OnStatus` → `ShowMessage`, если
  панель видима; `Debug.Log` всегда.
- `OnDestroy`/`Hide`: отписаться от контейнера.

- [ ] **Step 3: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 4: Commit**

```bash
git add Assets/Inventary/Script/UI/LootRowUI.cs Assets/Inventary/Script/UI/LootPanelUI.cs
git commit -m "feat(loot): loot list panel"
```

---


### Task 8: Наведение, блокировка атаки и курсор

**Files:**
- Create: `Assets/Inventary/Script/World/PointerGuard.cs`
- Create: `Assets/Inventary/Script/World/LootPointerController.cs`
- Modify: `Assets/Scripts/Drop/PlayerInteraction.cs:65-85`
- Modify: `Assets/Ideals/Pause_menu.cs:44-52, 74-85, 157-158`

**Interfaces:**
- Consumes: `WorldLootContainer`, `OutlineTarget`, `LootPanelUI`,
  `InventoryPanelUI.IsOpen`
- Produces: `PointerGuard.IsBlockingAttack` (static bool)

- [ ] **Step 1: `PointerGuard`**

```csharp
public static class PointerGuard
{
    public static bool IsBlockingAttack { get; set; }
}
```

- [ ] **Step 2: `LootPointerController`**

```csharp
public class LootPointerController : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private LootPanelUI panel;
    [SerializeField] private InventoryPanelUI inventoryPanel;
    [SerializeField] private LootPickupController pickupController;
    [SerializeField] private LayerMask rayMask = ~0;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private float hideDelay = 0.2f;
}
```

`Update`:
- инвентарь открыт или `pickupController.IsApproaching` → скрыть панель,
  `PointerGuard.IsBlockingAttack = false`, выйти;
- `Mouse.current == null` или нет камеры → скрыть, выйти;
- луч из камеры; `Physics.Raycast(..., QueryTriggerInteraction.Collide)`;
  `GetComponentInParent<WorldLootContainer>()`;
- попадание: запомнить контейнер, `OutlineTarget.Show(true)`, панель
  `Show(container, camera.WorldToScreenPoint(container.transform.position + Vector3.up * 0.5f))`,
  `PointerGuard.IsBlockingAttack = true`, сбросить таймер;
- промах, но `EventSystem.current.IsPointerOverGameObject()` → панель не скрывать,
  guard `true`;
- иначе тикать `hideDelay`, затем `HideCurrent()` (`OutlineTarget.Show(false)`,
  `panel.Hide()`, guard `false`).
- `OnDisable`: `HideCurrent()`.

- [ ] **Step 3: Блокировка атаки**

В `PlayerInteraction.IsAttackBlocked()` первой строкой:

```csharp
// Клик по луту и его панели не должен запускать атаку.
if (PointerGuard.IsBlockingAttack) return true;
```

- [ ] **Step 4: Курсор**

В `Pause_menu.cs` убрать запирание и скрытие курсора (строки с
`Cursor.lockState = CursorLockMode.Locked` / `Cursor.visible = false`),
оставив свободный и видимый курсор. Комментарий: игра управляется мышью, курсор
нужен для наведения.

- [ ] **Step 5: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 6: Commit**

```bash
git add Assets/Inventary/Script/World/PointerGuard.cs Assets/Inventary/Script/World/LootPointerController.cs Assets/Scripts/Drop/PlayerInteraction.cs Assets/Ideals/Pause_menu.cs
git commit -m "feat(loot): hover, outline and attack guard"
```

---


### Task 9: Выброс в мир, удаление и кулаки

**Files:**
- Modify: `Assets/Inventary/Script/UI/InventoryPanelUI.cs`
- Modify: `Assets/Inventary/Script/UI/InventoryConfirmDialog.cs`
- Modify: `Assets/Inventary/Script/UI/EquipmentSlotUI.cs`

**Interfaces:**
- Consumes: `WorldLootService.TryDrop`, `Inventory.RemoveItem`,
  `WeaponManager.EquipWeapon`, `WeaponData` (Fists)
- Produces:
  - `InventoryPanelUI.DeleteSelected()`
  - `InventoryConfirmDialog.Show(string, Action, string confirmLabel, Action cancel = null)`
  - `EquipmentSlotUI.unarmedWeapon`

- [ ] **Step 1: Подпись кнопки подтверждения**

В `InventoryConfirmDialog`:
- `[SerializeField] private TextMeshProUGUI confirmLabelText;`
- перегрузка `Show(string message, Action confirmAction, string confirmLabel, Action cancelAction = null)`;
- при показе: `confirmLabelText.text = confirmLabel`, иначе оставить как есть.

- [ ] **Step 2: `InventoryPanelUI` — выброс и удаление**

Добавить поля:

```csharp
[SerializeField] private WorldLootService lootService;
[SerializeField] private Transform dropOrigin;
[SerializeField] private Button deleteButton;
```

`DropSelected`:
- сообщение `Выбросить {quantity} × {item.DisplayName}?`, подпись «Выбросить»;
- в подтверждении:
  - проверить, что слот всё ещё содержит тот же предмет;
  - `if (lootService == null || dropOrigin == null || !lootService.TryDrop(item, quantity, dropOrigin))`
    → `SetFooter("Не удалось выбросить предмет.")`, выйти;
  - `inventory.RemoveItem(index, quantity)`, `ClearSelection()`, `RefreshViews()`.

`DeleteSelected`:
- сообщение `Удалить навсегда {quantity} × {item.DisplayName}?`, подпись «Удалить»;
- `inventory.RemoveItem(index, quantity)`, `ClearSelection()`, `RefreshViews()`.

`Awake`: `deleteButton.onClick.AddListener(DeleteSelected)`.
`UpdateButtons`: `deleteButton.interactable = hasSelection`.

- [ ] **Step 3: Кулаки после снятия оружия**

В `EquipmentSlotUI`:
- `[SerializeField] private WeaponData unarmedWeapon;`
- в `HandleUnequipped`, если `slotType == EquipmentSlotType.Weapon` и
  `weaponManager != null`:
  - `unarmedWeapon != null` → `weaponManager.EquipWeapon(unarmedWeapon)`;
  - иначе один раз `Debug.LogWarning("[EquipmentSlotUI] Не назначен unarmedWeapon.")`.

- [ ] **Step 4: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 5: Commit**

```bash
git add Assets/Inventary/Script/UI/InventoryPanelUI.cs Assets/Inventary/Script/UI/InventoryConfirmDialog.cs Assets/Inventary/Script/UI/EquipmentSlotUI.cs
git commit -m "feat(inventory): drop to world, delete and unarmed weapon"
```

---


### Task 10: Сетап — одежда, предметы и 5 слотов

**Files:**
- Modify: `Assets/Editor/InventorySetup.cs`

**Interfaces:**
- Consumes: `ItemData.equipSlot/equipVisualId`, `CharacterEquipmentVisuals`,
  `PlayerEquipmentBridge`
- Produces: 5 тестовых `ItemData`, связи визуалов в сцене

- [ ] **Step 1: Тестовые предметы**

Заменить `CreateTestItems` на 5 предметов:

| Ассет | itemId | Имя | Тип | Слот | Иконка | Визуал |
|---|---|---|---|---|---|---|
| `Weapon_BarbarianSword.asset` | `loot_sword_barbarian` | Меч варвара | Weapon | Weapon | `_4` | — |
| `Armor_Leather.asset` | `loot_armor_leather` | Кожаная броня | Armor | Armor | `armor_icon` | `M_Swordsman_Top` |
| `Helmet_Mage.asset` | `loot_helmet_mage` | Шапка мага | Armor | Helmet | `_0` | `M_Mage_Headwear` |
| `Pants_Leather.asset` | `loot_pants_leather` | Кожаные штаны | Armor | Pants | `_2` | `M_Swordsman_Bottom` |
| `Accessory_Ring.asset` | `loot_accessory_ring` | Кольцо странника | Armor | Accessory | `one-ring` | `M_SlingBag` |

- Меч: `weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Combat/Weapons/Sword.asset")`.
- Иконки: `LoadSprites` для листа barbarian и `AssetDatabase.LoadAssetAtPath<Sprite>`
  для одиночных (`armor_icon.png`, `one-ring.jpg`). Для листа брать спрайт с
  `sprite.name.EndsWith("_4")` / `_0` / `_2`.
- `startingItems` в сцене — пустой список.

- [ ] **Step 2: Визуалы одежды в сцене**

Метод `SetupCharacterVisuals()`:
- найти `BasicHero_M` (`FindSceneGameObject("BasicHero_M")`);
- найти/создать `BasicHero_M/EquipmentVisuals` и папки `Armor`, `Helmet`, `Pants`,
  `Accessory` (пустые `GameObject`);
- перенести `SetParent(folder, true)`:
  - `M_Fighter/Top/M_Fighter_Top` → Armor;
  - `M_Archer/ArcherTopBody/M_Archer_Top` → Armor;
  - `M_Mage/M_Mage_Top/M_SlingBag` → Accessory;
  - `M_Archer/ArcherTopBody/M_ArrowQuiver` и `M_Arrow` → Accessory;
  - `M_Archer/ArcherCapeTopBody/M_Archer_Cape` → Accessory;
  - `M_Warlock/M_Warlock_TopCape/M_Warlock_Cape` → Accessory;
- зарегистрировать (имя объекта = `visualId`):
  - Armor: `M_Swordsman_Top`, `M_Archer_Top` (из `ArcherTopBody`), `M_Mage_Top`,
    `M_Fighter_Top`, `M_Rogue_Top`, `M_Warlock_Top`, `M_Warlock_TopCape`;
  - Helmet: `M_Mage_Headwear`, `M_Warlock_Hood`;
  - Pants: шесть `M_*_Bottom`;
  - Accessory: `M_SlingBag`, `M_ArrowQuiver`, `M_Archer_Cape`, `M_Warlock_Cape`;
- второй `M_Archer_Top` из `ArcherCapeTopBody` не регистрировать;
- искать вещи по имени среди всех детей `BasicHero_M` (включая выключенные), а не
  только по старому пути: повторный запуск сетапа не должен падать и не должен
  переносить уже перенесённое;
- если `EquipmentVisuals` уже существует — переиспользовать папки, а список
  `CharacterEquipmentVisuals` очистить и заполнить заново (без дублей);
- если объект не найден — `Debug.LogError` с путём и пропустить его, остальные
  визуалы продолжают работать;
- все зарегистрированные объекты выключить;
- добавить/найти `CharacterEquipmentVisuals` на `BasicHero_M` и заполнить
  `visuals` через `SerializedObject`;
- добавить/найти `PlayerEquipmentBridge` на `Player`, связать `inventory` (из панели)
  и `visuals`.

- [ ] **Step 3: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 4: Запустить сетап**

В Unity: `Tools → Inventory → Setup Test Data`. Дождаться диалога «Готово».
Проверить Console: нет ошибок, есть `[InventorySetup] Готово`.

- [ ] **Step 5: Проверить сцену**

В `Game.unity`:
- `BasicHero_M/EquipmentVisuals` существует, в папках лежат вещи;
- все вещи выключены;
- на `BasicHero_M` есть `CharacterEquipmentVisuals` с записями по слотам;
- на `Player` есть `PlayerEquipmentBridge`;
- панель инвентаря содержит 5 слотов (Оружие, Броня, Шлем, Штаны, Аксессуар).

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/InventorySetup.cs Assets/Inventary/TestData Assets/Scenes/Game.unity
git commit -m "feat(setup): test loot items and character visuals"
```

---


### Task 11: Сетап — контейнер, сервис, панель и связи

**Files:**
- Modify: `Assets/Editor/InventorySetup.cs`
- Create (сетапом): `Assets/Inventary/Prefabs/WorldLootContainer.prefab`,
  `Assets/Inventary/Prefabs/LootRow.prefab`,
  `Assets/Inventary/Materials/OutlineMaterial.mat`

**Interfaces:**
- Consumes: `WorldLootService`, `WorldLootContainer`, `OutlineTarget`,
  `LootPanelUI`, `LootRowUI`, `LootPointerController`, `LootPickupController`
- Produces: собранная сцена с лутом и панелью

- [ ] **Step 1: Материал и префаб контейнера**

`EnsureOutlineMaterial()`: загрузить/создать
`Assets/Inventary/Materials/OutlineMaterial.mat` с шейдером `Inventory/Outline`;
`_OutlineWidth = 0.06`, `_OutlineColor = (1, 0.82, 0.3, 1)`.

`CreateLootContainerPrefab()`:
- корень `WorldLootContainer` + `WorldLootContainer` + `OutlineTarget` +
  `SphereCollider` (radius 0.7, center `(0, 0.4, 0)`);
- дочерний `ItemEffect` — инстанс `Assets/Effects/ItemEffect.prefab`;
- `OutlineTarget.outlineMaterial` = материал;
- сохранить в `Assets/Inventary/Prefabs/WorldLootContainer.prefab`, временный удалить.

- [ ] **Step 2: Сервис и стартовый лут**

`BuildLootScene(...)`:
- найти/создать `LootService` с `WorldLootService`; задать `containerPrefab`,
  `dropDistance = 1.5`, `mergeRadius = 2.5`, `groundRayHeight = 5`, `groundMask = ~0`;
- найти `PlayerController`; точка `player.position + player.forward * 3`;
- удалить старый контейнер `[Loot] Starter` (по имени) и создать новый через
  `SpawnContainer`; добавить 5 тестовых предметов по 1 шт.;
- имя объекта контейнера — `[Loot] Starter`.

- [ ] **Step 3: Панель лута**

`BuildLootPanel(...)`:
- на `Canvas` найти/удалить `LootPanel`;
- создать `LootPanel` (Image + CanvasGroup + `LootPanelUI`), 380×360, скрыт;
- заголовок «Содержимое», `ScrollRect` + `Viewport` + `Content`
  (`VerticalLayoutGroup`, `ContentSizeFitter`), кнопка «Забрать всё», текст сообщений;
- создать `LootRow.prefab`: фон, иконка, `[PLACEHOLDER]`, имя, количество,
  кнопка «Забрать»; сохранить в `Assets/Inventary/Prefabs/LootRow.prefab`;
- связать поля `LootPanelUI`.

- [ ] **Step 4: Компоненты игрока и связи**

- `LootPointerController` на `Player`: `playerCamera = Camera.main`,
  `panel`, `inventoryPanel`, `pickupController`, `rayMask = ~0`,
  `maxDistance = 100`, `hideDelay = 0.2`;
- `LootPickupController` на `Player`: `inventory`, `agent`, `animator`,
  `speedParameter = "Speed"`, `pickupRange = 2.5`, `approachTimeout = 8`,
  `noProgressTimeout = 2`;
- `InventoryPanelUI`: `lootService`, `dropOrigin = player.transform`, `deleteButton`;
- `EquipmentSlotUI` слота Weapon: `unarmedWeapon = Fists.asset`
  (`Assets/Combat/Weapons/Fists.asset`);
- `InventoryConfirmDialog`: `confirmLabelText` = подпись кнопки подтверждения;
- панель собирается заново на 5 слотов + кнопка «Удалить».

- [ ] **Step 5: Компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 6: Запустить сетап и проверить**

`Tools → Inventory → Setup Test Data`; Console без ошибок. Проверить:
- перед игроком лежит `[Loot] Starter` с `ItemEffect`;
- на `Canvas` есть `LootPanel`;
- в `InventoryPanel` 5 слотов и кнопка «Удалить»;
- на `Player` — `LootPointerController` и `LootPickupController`.

- [ ] **Step 7: Ручная проверка в Play mode**

1. Навести мышь на сферу — Outline и панель со списком 5 предметов.
2. «Забрать» меч издалека — панель закрывается, персонаж идёт и берёт.
3. «Забрать всё» — контейнер исчезает, 5 предметов в инвентаре.
4. `I` — 5 слотов; надеть меч — меч в руке; снять — исчез.
5. Надеть броню/шлем/штаны/сумку — одежда меняется; снять — возвращается.
6. «Выбросить» — предмет у ног; «Удалить» — исчезает насовсем.
7. Клик по сфере/кнопкам не атакует; WASD отменяет подход; после Esc мышь работает.
8. Заполнить инвентарь, нажать «Забрать» — сообщение «В инвентаре нет места»,
   вещи остаются в контейнере.

- [ ] **Step 8: Commit**

```bash
git add Assets/Editor/InventorySetup.cs Assets/Inventary/Prefabs Assets/Inventary/Materials Assets/Scenes/Game.unity
git commit -m "feat(setup): loot container, panel and player wiring"
```

---


### Task 12: README и финальная приёмка

**Files:**
- Modify: `Assets/Inventary/README.md`

- [ ] **Step 1: README**

Добавить разделы:
- «Лут в мире»: `WorldLootContainer`, `WorldLootService`, `[Loot] Starter`,
  «Забрать»/«Забрать всё», подход, отмена WASD.
- «Визуальная экипировка»: `CharacterEquipmentVisuals`, `equipVisualId`,
  выпадающий список в инспекторе, таблица слотов и визуалов.
- «Выброс и удаление»: разница кнопок.
- «Курсор»: почему он свободен.
- «Меню сетапа»: `Setup Test Data` и `Rebuild Game Panel`.

- [ ] **Step 2: Финальная компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: 0 ошибок.

- [ ] **Step 3: Полная приёмка из дизайна**

Пройти все 14 пунктов раздела «Приёмка» дизайн-дока в Play mode. Записать
результат в конце ответа пользователю; при провале — вернуться к задаче-владельцу.

- [ ] **Step 4: Commit**

```bash
git add Assets/Inventary/README.md
git commit -m "docs(inventory): loot and equipment visuals"
```
