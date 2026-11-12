# Инвентарь, экипировка и книга умений — план реализации

> **Для агента-исполнителя:** обязательный скилл — `superpowers:executing-plans`
> (native) или `superpowers:subagent-driven-development`. Шаги отмечаются
> чекбоксами. План реализует утверждённый дизайн
> `docs/superpowers/specs/2026-11-12-inventory-combat-design.md`.

**Goal:** Довести инвентарь, слот оружия, три тестовых меча, способности
оружия с Block, глобальную магию и книгу умений до играбельного состояния в
`Game.unity`, не меняя `PlayerController`, `DestructibleObject`, `Garderob`
и `UiManager`.

**Architecture:** `WeaponManager.CurrentWeapon` — источник истины для оружия.
`WeaponSlotUI` — представление, которое принимает предмет из сетки, хранит
`ownedItem` и синхронизируется с гардеробом через `OnWeaponEquipped`.
`AbilitySystem` хранит кулдауны по `abilityId`, способности оружия в слотах
1–3 и глобальную магию в слотах 3–5. `CharacterMenuController` открывает
инвентарь (`I`) и книгу умений (`K`), ставит игру на паузу и временно
отключает `UiManager`. Setup-скрипты создают ассеты, prefab-ы и собирают
UI в `Game.unity`; `SampleScene` остаётся песочницей.

**Tech Stack:** Unity 6000.2.10f1, Assembly-CSharp (без asmdef), uGUI,
TextMeshPro, Input System (`Keyboard.current`), `JsonUtility`,
`AnimatorOverrideController`.

**Spec:** `docs/superpowers/specs/2026-11-12-inventory-combat-design.md`

## Global Constraints

- `Assets/Scripts/Player/PlayerController.cs` **не изменять**.
- `Assets/Scripts/DestructibleObject.cs` **не изменять**.
- `Assets/Garderob.cs` **не изменять**.
- `Assets/UiManager.cs` **не изменять**.
- `Assets/PlayerAnimations/PlayerController.controller` **не изменять**.
- `Assets/Scripts/Player/Weapon.cs`, `AnimationEventRelay.cs`,
  `AttackHitbox.cs`, `WeaponEquipButton.cs` **не изменять**.
- Урон — только по объектам с тегом `DammageObject`; `HarvestableObject`,
  `Enemy`, здоровье игрока не добавлять.
- `useAnimationEvents = true` → хитбокс только через `AnimationEventRelay`;
  `false` → только `PlayerController` по `normalizedTime` 0.15–0.55.
- Кулдауны — `Dictionary<string, float>` по `abilityId` (fallback —
  `ability.name`); при смене оружия не сбрасываются.
- Способности оружия занимают слоты 1–3; глобальная магия — только слоты
  3–5 (индексы 2–4). Одна магия не может стоять в двух слотах.
- Гардероб **не забирает** предмет из сетки: он меняет только
  `WeaponManager.CurrentWeapon`. Старый `ownedItem` уходит в
  `pendingReturns`, а не теряется.
- Слот оружия null-safe без `WeaponManager` (`SampleScene`).
- `Inventory` инициализирует `width * height` слотов; `Game.unity` — 5×4,
  `SampleScene` — 5×7.
- Теги `TrashArea`, `DescriptObject` добавляются setup-скриптом; код
  дополнительно ловит `UnityException` при `CompareTag`/`FindGameObjectWithTag`.
- Проект без Unity Test Framework: цикл проверки = компиляция
  `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo` + ручная
  проверка в Play mode.
- Существующие `Assets/Scripts/Cultivation/**` и `DarkCrossoardOfDesteny.slnx`
  — не наши изменения; не трогать.

## Review Focus

1. **Undefined-теги и drag-and-drop.** `CompareTag("TrashArea")`,
   `FindGameObjectWithTag("DescriptObject")`, `FindGameObjectWithTag("DragIcon")`
   бросают `UnityException`, если тег не объявлен; предмет не должен
   теряться при отказе слота.
2. **Гардероб vs `ownedItem`.** Смена через гардероб не должна оставлять
   старый предмет в слоте, дублировать его или терять при полном инвентаре;
   `UnequipWeapon` должен работать и без `ownedItem`.
3. **Кулдауны по `abilityId`.** Смена оружия и перенос магии между слотами
   не сбрасывают кулдаун; меле-способность чужого оружия не активируется.
4. **Block и магия.** Block не включает хитбокс, останавливает движение на
   ~1.5 с и корректно возвращает управление; отмена каста не оставляет
   Animator в состоянии `Attack`.
5. **Идемпотентность setup.** Повторный `Tools/Combat/0. Full Setup` не
   плодит панели/кнопки/компоненты, не теряет ссылки и не ломает
   `SampleScene` без `WeaponManager`.

---

### Task 1: Данные и helpers инвентаря

**Files:**
- Modify: `Assets/Inventary/Script/SO/ItemData.cs`
- Modify: `Assets/Inventary/Script/InventorySlot.cs`
- Modify: `Assets/Inventary/Script/Inventory.cs`

**Interfaces:**
- Produces: `ItemType.Armor`, `ItemType.Magic`.
- Produces: `ItemData.displayName` (`string`), `maxStack` (`int`),
  `itemType` (`ItemType`), `IsWeapon` (`bool`).
- Produces: `InventorySlot.IsEmpty` (`bool`).
- Produces: `Inventory.WeaponSlot` (`WeaponSlotUI`),
  `WeaponManagerRef` (`WeaponManager`).
- Produces: `Inventory.FindFirstEmptySlot(int exceptIndex = -1) -> int`.
- Produces: `Inventory.TryReturnItemToInventory(ItemData item, int quantity,
  int preferredIndex = -1) -> bool`.

- [ ] **Step 1: Расширить `ItemData`**

В `enum ItemType` добавить `Armor` и `Magic` после `Quest`. В конец класса
добавить только свойства-алиасы (без новых сериализуемых полей):

```csharp
public string displayName => itemName;
public int maxStack => maxStackSize;
public ItemType itemType => type;
public bool IsWeapon => type == ItemType.Weapon && weaponData != null;
```

- [ ] **Step 2: Добавить `InventorySlot.IsEmpty`**

```csharp
public bool IsEmpty => itemData == null || quantity <= 0;
```

- [ ] **Step 3: Добавить ссылки и helpers в `Inventory`**

После `DescriptText`:

```csharp
[Header("Equipment")]
[SerializeField] private WeaponManager weaponManager;
[SerializeField] private WeaponSlotUI weaponSlot;

public WeaponSlotUI WeaponSlot => weaponSlot;
public WeaponManager WeaponManagerRef => weaponManager;
```

В `Awake` после `InitializeSlots()`:

```csharp
if (weaponManager == null)
    weaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
if (weaponSlot == null)
    weaponSlot = FindFirstObjectByType<WeaponSlotUI>(FindObjectsInactive.Include);
```

После `GetSlotCount()`:

```csharp
public int FindFirstEmptySlot(int exceptIndex = -1)
{
    for (int i = 0; i < slots.Count; i++)
    {
        if (i == exceptIndex) continue;
        if (slots[i] == null || slots[i].IsEmpty) return i;
    }
    return -1;
}

public bool TryReturnItemToInventory(ItemData item, int quantity, int preferredIndex = -1)
{
    if (item == null || quantity <= 0) return false;

    if (preferredIndex >= 0 && preferredIndex < slots.Count &&
        (slots[preferredIndex] == null || slots[preferredIndex].IsEmpty))
    {
        return SetSlot(preferredIndex, item, quantity);
    }

    int empty = FindFirstEmptySlot();
    return empty >= 0 && SetSlot(empty, item, quantity);
}
```

- [ ] **Step 4: Убрать мутацию `ItemData` и null-check `DescriptText`**

В `InternalAddItem` удалить строку:

```csharp
slots[i].itemData.description = item.description;
```

Заменить `DescriptText.text = ...` на:

```csharp
if (DescriptText != null)
    DescriptText.text = "В инвентаре нет места";
```

- [ ] **Step 5: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Inventary/Script/SO/ItemData.cs Assets/Inventary/Script/InventorySlot.cs Assets/Inventary/Script/Inventory.cs
git commit -m "feat(inventory): add item aliases and slot helpers"
```

---

### Task 2: `WeaponManager` — default weapon, material override, cast API

**Files:**
- Modify: `Assets/Scripts/Combat/WeaponData.cs`
- Modify: `Assets/Scripts/Combat/WeaponManager.cs`

**Interfaces:**
- Produces: `WeaponData.weaponMaterialOverride` (`Material`).
- Produces: `WeaponManager.DefaultWeapon` (`WeaponData`).
- Produces: `WeaponManager.PlayCastAnimation(AnimationClip clip,
  bool autoResume = true) -> bool`.
- Produces: `WeaponManager.StopCastAnimation()`.

- [ ] **Step 1: Добавить материал в `WeaponData`**

В блок `[Header("Visual")]`:

```csharp
public Material weaponMaterialOverride;
```

- [ ] **Step 2: Добавить `DefaultWeapon` и fallback кости**

В `WeaponManager` после `startingWeapon`:

```csharp
[SerializeField] private WeaponData defaultWeapon;

public WeaponData DefaultWeapon => defaultWeapon != null ? defaultWeapon : startingWeapon;
```

В `Awake` после поиска `RightHand`:

```csharp
if (rightHand == null)
    rightHand = FindChildByName(transform, "R_wrist_joint");
```

Добавить приватный рекурсивный helper:

```csharp
private static Transform FindChildByName(Transform root, string childName)
{
    foreach (Transform child in root)
    {
        if (child.name == childName) return child;
        Transform found = FindChildByName(child, childName);
        if (found != null) return found;
    }
    return null;
}
```

- [ ] **Step 3: Материал меча**

В `CreateSword` после установки `localScale` и до отключения коллайдеров:

```csharp
if (weapon.weaponMaterialOverride != null)
{
    foreach (Renderer renderer in weaponModelInstance.GetComponentsInChildren<Renderer>(true))
    {
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0) continue;
        for (int i = 0; i < materials.Length; i++)
            materials[i] = weapon.weaponMaterialOverride;
        renderer.sharedMaterials = materials;
    }
}
```

- [ ] **Step 4: `PlayCastAnimation` с `autoResume` и `StopCastAnimation`**

Заменить сигнатуру:

```csharp
public bool PlayCastAnimation(AnimationClip clip, bool autoResume = true)
```

Добавить поле `private bool castAutoResume = true;` и в начале метода:

```csharp
castAutoResume = autoResume;
```

В `RestoreAfterCast` вызывать `ResumeMovement()` только при `castAutoResume`:

```csharp
if (castAutoResume && playerController != null)
    playerController.ResumeMovement();
```

Добавить:

```csharp
public void StopCastAnimation()
{
    if (castRestoreRoutine != null)
    {
        StopCoroutine(castRestoreRoutine);
        castRestoreRoutine = null;
    }

    RestoreWeaponController();
    DestroyCastOverride();
    SuppressHitbox = false;
    ApplyHitbox(false);

    if (playerController != null)
    {
        playerController.ResetHitboxState();
        playerController.ResumeMovement();
    }
}
```

В `EquipWeapon` заменить блок остановки каста (строки с `StopCoroutine`/
`RestoreWeaponController`/`DestroyCastOverride`) на:

```csharp
StopCastAnimation();
if (abilitySystem != null) abilitySystem.CancelCurrentAbility();
```

- [ ] **Step 5: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Combat/WeaponData.cs Assets/Scripts/Combat/WeaponManager.cs
git commit -m "feat(combat): add default weapon and cast control"
```

---

### Task 3: `WeaponSlotUI`, `ItemTooltipUI` и `Inventory.EquipWeaponFromSlot`/`UnequipWeapon`

**Files:**
- Create: `Assets/Inventary/Script/ItemTooltipUI.cs`
- Modify: `Assets/Scripts/UI/WeaponSlotUI.cs`
- Modify: `Assets/Inventary/Script/Inventory.cs`

**Interfaces:**
- Consumes: Task 1 helpers; Task 2 `DefaultWeapon`.
- Produces: `ItemTooltipUI.Show(ItemData item, RectTransform anchor)`,
  `Show(string title, string description, Sprite icon, RectTransform anchor)`,
  `Hide()`.
- Produces: `WeaponSlotUI.DisplayItem` (`ItemData`),
  `OwnedQuantity` (`int`), `HasOwnedItem` (`bool`),
  `EnsureInitialized()`, `TryAcceptItem(int sourceIndex, Inventory sourceInventory) -> bool`,
  `ReturnOwnedItemToInventory(Inventory target, int preferredIndex = -1) -> bool`,
  `ClearOwnedItem()`.
- Produces: `Inventory.EquipWeaponFromSlot(int slotIndex) -> bool`,
  `Inventory.UnequipWeapon() -> bool`.

> **Ruling (preflight):** `ItemTooltipUI` переносится из Task 4 в Task 3,
> потому что `WeaponSlotUI` ссылается на этот тип, а Task 4, наоборот,
> потребляет `Inventory.EquipWeaponFromSlot` из Task 3. Иначе задачи
> образуют цикл и не компилируются по отдельности.

- [ ] **Step 0: Создать `ItemTooltipUI.cs` (перенесено из Task 4 Step 1)**

Глобальное пространство имён, `using TMPro; using UnityEngine; using UnityEngine.UI;`.

Поля:

```csharp
[SerializeField] private Image icon;
[SerializeField] private TMP_Text nameText;
[SerializeField] private TMP_Text descriptionText;
[SerializeField] private TMP_Text typeText;
[SerializeField] private RectTransform root;
private bool isShowing;
```

`Awake`: если `root == null` — `GetComponent<RectTransform>()`; у всех
`Graphic` выставить `raycastTarget = false`; скрыть `root.gameObject`
**только если `!isShowing`** (иначе первый `Show` на неактивном объекте
активирует его, `Awake` тут же спрячет, и тултип не появится).

`Show(ItemData item, RectTransform anchor)`:
- `null` → `Hide()`;
- заполнить иконку/название/описание/тип (`item.type.ToString()`);
- вызвать `Show(item.itemName, item.description, item.icon, anchor)`
  (именно `item.icon`, а не иконку самого тултипа).

`Show(string title, string description, Sprite iconSprite, RectTransform anchor)`:
- если `root == null` — `root = GetComponent<RectTransform>()`;
- `isShowing = true;`
- заполнить тексты (все поля проверить на `null`); иконка
  `enabled = iconSprite != null`;
- `root.gameObject.SetActive(true); root.SetAsLastSibling();`;
- `LayoutRebuilder.ForceRebuildLayoutImmediate(root);` — иначе
  `root.rect.width` может быть 0 при первом показе;
- позиция: справа от `anchor` (`anchor.rect.width * 0.5f + root.rect.width * 0.5f + 8f`);
  если правый край выходит за экран — слева; по вертикали выровнять по центру
  `anchor`, затем ограничить экраном.

`Hide()`: `isShowing = false;` если `root != null` —
`root.gameObject.SetActive(false)`.

- [ ] **Step 1: Добавить состояние и `EnsureInitialized` в `WeaponSlotUI`**

Добавить `using UnityEngine.EventSystems;`. Поля:

```csharp
private readonly List<PendingReturn> pendingReturns = new List<PendingReturn>();
private bool initialized;
private bool subscribedWeapon;
private bool subscribedInventory;
private bool isFlushing;
[SerializeField] private ItemTooltipUI tooltip;
[SerializeField] private Button unequipButton;
private WeaponData displayedWeapon;
private bool appliedInitialWeapon;

[System.Serializable]
private struct PendingReturn
{
    public ItemData item;
    public int quantity;

    public PendingReturn(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }
}

public ItemData DisplayItem => displayItem;
public int OwnedQuantity => ownedQuantity;
public bool HasOwnedItem => ownedItem != null;
```

`Awake`, `OnEnable`, `Start` вызывают `EnsureInitialized()`. Старые подписки
из `Start`/`OnDestroy` заменяются на подписки внутри `EnsureInitialized`
(иначе `OnWeaponEquipped` будет подписан дважды).
`EnsureInitialized()`:

- идемпотентна;
- находит `inventory`, `weaponManager`, `saveSystem` через
  `FindFirstObjectByType(..., FindObjectsInactive.Include)`;
- подписывается на `weaponManager.OnWeaponEquipped` (флаг `subscribedWeapon`);
- подписывается на `inventory.OnInventoryChanged` (флаг `subscribedInventory`);
  обработчик вызывает **только** `TryFlushPendingReturns()`, не
  `ApplyEquippedWeapon` — иначе смена оружия зациклится;
- вызывает `ApplyEquippedWeapon(weaponManager.CurrentWeapon)` только один раз:
  при первом вызове с найденным `weaponManager` (`appliedInitialWeapon`).
  Если менеджер появился позже, применить его `CurrentWeapon` в момент
  подписки. Повторные вызовы **не** вызывают `ApplyEquippedWeapon(null)` —
  иначе предмет в `SampleScene` без `WeaponManager` уедет в `pendingReturns`;
- вызывает `TryFlushPendingReturns()`.

`OnDestroy` отписывается от обоих событий.

- [ ] **Step 2: `TryAcceptItem` и `TryAccept`**

`TryAccept(InventorySlotUI source)` делегирует:

```csharp
public bool TryAccept(InventorySlotUI source)
{
    if (source == null) return false;
    return TryAcceptItem(source.GetSlotIndex(), source.GetInventory());
}
```

`TryAcceptItem`:

1. Отклонить `displayOnly`, `sourceInventory == null`, пустой слот,
   `!CanAccept(item)`, `ownedItem == item`.
2. `InventorySlot taken = sourceInventory.TakeSlot(sourceIndex);`
   если `null` — `false`.
3. Запомнить `previousItem`/`previousQuantity`.
4. Записать `ownedItem = taken.itemData`,
   `ownedQuantity = Mathf.Max(1, taken.quantity)`.
5. Если `previousItem != null`: сначала
   `sourceInventory.SetSlot(sourceIndex, previousItem, previousQuantity)`;
   если не вышло — `sourceInventory.TryReturnItemToInventory(previousItem,
   previousQuantity)`; если и это не вышло — `pendingReturns.Add(...)`.
6. Если `weaponManager != null` — `weaponManager.EquipWeapon(incoming.weaponData)`.
7. `ApplyEquippedWeapon(weaponManager != null
   ? weaponManager.CurrentWeapon : incoming.weaponData)`.
8. `saveSystem?.SaveInventory()`.
9. `TryFlushPendingReturns()`, вернуть `true`.

- [ ] **Step 3: `ApplyEquippedWeapon`, `pendingReturns`, `ClearOwnedItem`**

`ApplyEquippedWeapon(WeaponData weapon)`:

- `displayOnly`: `displayItem = FindItemForWeapon(weapon)`,
  `currentLabel = displayItem != null ? displayItem.itemName :
  (weapon != null ? weapon.displayName : string.Empty)`, `isEquipped = weapon != null`.
- иначе:
  - если `ownedItem != null && ownedItem.weaponData != weapon` — добавить
    `PendingReturn(ownedItem, ownedQuantity)` в `pendingReturns`, очистить
    `ownedItem`/`ownedQuantity`;
  - `displayItem = ownedItem != null && ownedItem.weaponData == weapon
    ? ownedItem : FindItemForWeapon(weapon)`;
  - `currentLabel = displayItem != null ? displayItem.itemName :
    (weapon != null ? weapon.displayName : string.Empty)`;
  - `isEquipped = weapon != null`.
- `displayedWeapon = weapon; RefreshVisual(); TryFlushPendingReturns();`.

В `RefreshVisual` добавить состояние кнопки «Снять»:

```csharp
if (unequipButton != null)
{
    // Не требуем isEquipped: оружие могло быть надето через гардероб,
    // тогда ownedItem == null, но снять его всё равно можно.
    unequipButton.interactable = weaponManager != null &&
        weaponManager.CurrentWeapon != weaponManager.DefaultWeapon;
}
```

`TryFlushPendingReturns()`:

```csharp
private void TryFlushPendingReturns()
{
    if (isFlushing || inventory == null || pendingReturns.Count == 0) return;

    isFlushing = true;
    try
    {
        for (int i = pendingReturns.Count - 1; i >= 0; i--)
        {
            PendingReturn pending = pendingReturns[i];
            if (inventory.TryReturnItemToInventory(pending.item, pending.quantity))
                pendingReturns.RemoveAt(i);
        }
    }
    finally
    {
        isFlushing = false;
    }
}
```

`ReturnOwnedItemToInventory(Inventory target, int preferredIndex = -1)`:
если `ownedItem == null` или `target == null` — `false`; иначе
`target.TryReturnItemToInventory(ownedItem, ownedQuantity, preferredIndex)`;
при успехе `ClearOwnedItem()` и `true`.

`ClearOwnedItem()` очищает `ownedItem`, `ownedQuantity`, `displayItem`,
`currentLabel`, `isEquipped`, вызывает `RefreshVisual()`.

- [ ] **Step 4: Тултип слота оружия**

`WeaponSlotUI` реализует `IPointerEnterHandler`, `IPointerExitHandler`.
Добавить `public void SetTooltip(ItemTooltipUI value)`.

`OnPointerEnter`:

```csharp
if (tooltip == null) return;
if (displayItem != null)
    tooltip.Show(displayItem, (RectTransform)transform);
else if (displayedWeapon != null)
    tooltip.Show(displayedWeapon.displayName, string.Empty, null, (RectTransform)transform);
```

`OnPointerExit` вызывает `tooltip?.Hide()`.

- [ ] **Step 5: `Inventory` — экипировка и снятие**

Добавить:

```csharp
public bool EquipWeaponFromSlot(int slotIndex)
{
    InventorySlot slot = GetSlot(slotIndex);
    if (slot == null || slot.itemData == null || !slot.itemData.IsWeapon)
        return false;

    WeaponSlotUI target = FindFunctionalWeaponSlot();
    if (target == null) return false;

    target.EnsureInitialized();
    return target.TryAcceptItem(slotIndex, this);
}

public bool UnequipWeapon()
{
    WeaponSlotUI slot = FindFunctionalWeaponSlot();
    if (slot == null) return false;
    if (weaponManager == null)
        weaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
    if (weaponManager == null) return false;

    slot.EnsureInitialized();

    // Сначала проверяем fallback: иначе при отсутствии DefaultWeapon
    // предмет уже вернётся в сетку, а старое оружие останется надетым.
    WeaponData fallback = weaponManager.DefaultWeapon;
    if (fallback == null)
    {
        Debug.LogWarning("[Inventory] Не задан DefaultWeapon для снятия оружия.");
        return false;
    }

    if (slot.OwnedItem != null)
    {
        int empty = FindFirstEmptySlot();
        if (empty < 0)
        {
            Debug.LogWarning("[Inventory] Нет свободного слота для снятия оружия.");
            return false;
        }

        if (!slot.ReturnOwnedItemToInventory(this, empty)) return false;
    }

    weaponManager.EquipWeapon(fallback);

    InventorySaveSystem saveSystem =
        FindFirstObjectByType<InventorySaveSystem>(FindObjectsInactive.Include);
    if (saveSystem != null) saveSystem.SaveInventory();

    return true;
}

private WeaponSlotUI FindFunctionalWeaponSlot()
{
    if (weaponSlot != null && !weaponSlot.IsDisplayOnly) return weaponSlot;

    WeaponSlotUI[] slots = FindObjectsByType<WeaponSlotUI>(
        FindObjectsInactive.Include, FindObjectsSortMode.None);

    foreach (WeaponSlotUI candidate in slots)
    {
        if (candidate != null && !candidate.IsDisplayOnly) return candidate;
    }

    return null;
}
```

- [ ] **Step 6: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 7: Commit**

```powershell
git add Assets/Scripts/UI/WeaponSlotUI.cs Assets/Inventary/Script/Inventory.cs
git commit -m "feat(inventory): make weapon slot functional"
```

---

### Task 4: Инвентарный UI и интеграция тултипа

**Files:**
- Modify: `Assets/Inventary/Script/InventoryUI.cs`
- Modify: `Assets/Inventary/Script/InventorySlotUI.cs`

**Interfaces:**
- Consumes: Task 1 `Inventory.FindFirstEmptySlot`; Task 3
  `Inventory.EquipWeaponFromSlot`, `ItemTooltipUI`.
- Produces: `InventoryUI.EnsureInitialized()`, `descriptionText`, `tooltip`.
- Produces: `InventorySlotUI.SetTooltip(ItemTooltipUI)`,
  `SetDescriptionText(TextMeshProUGUI)`.

- [ ] **Step 1: Проверить `ItemTooltipUI`**

`Assets/Inventary/Script/ItemTooltipUI.cs` уже создан в Task 3 Step 0.
Не создавать повторно; использовать его `Show`/`Hide`.

- [ ] **Step 2: `InventoryUI.EnsureInitialized`**

Добавить поля:

```csharp
[SerializeField] private TextMeshProUGUI descriptionText;
[SerializeField] private ItemTooltipUI tooltip;

private bool initialized;
```

`Awake`/`OnEnable`/`Start` вызывают `EnsureInitialized()`. Старые `Start` и
`InitializeUI` **заменяются** на `EnsureInitialized` (не оставлять оба пути:
иначе слоты создадутся дважды и подписка на `OnInventoryChanged` удвоится).

`EnsureInitialized()`:
- если `initialized` — выход;
- найти `inventory` через `FindFirstObjectByType<Inventory>(FindObjectsInactive.Include)`;
- если `inventory == null || slotPrefab == null || slotParent == null` —
  `Debug.LogWarning` и выход;
- если `inventory.GetSlotCount() <= 0` — выход **без** `initialized = true`:
  `Awake` мог отработать раньше `Inventory.Awake`, тогда `Start` повторит
  инициализацию после инициализации слотов;
- создать `slotUIs` по `inventory.GetSlotCount()`;
- для каждого слота: `Instantiate(slotPrefab, slotParent)`,
  `Initialize(i, inventory)`, `SetTooltip(tooltip)`,
  `SetDescriptionText(descriptionText)`;
- `inventory.OnInventoryChanged += UpdateUI`;
- `initialized = true; UpdateUI(inventory);`.

`UpdateUI` проверяет `slotUIs == null` и `inventory == null`.
`OnDestroy` отписывается. Добавить `using TMPro;` для
`TextMeshProUGUI`.

- [ ] **Step 3: `InventorySlotUI` — клик-эквип и тултип**

Добавить поле `private ItemTooltipUI tooltip;` и методы:

```csharp
public void SetTooltip(ItemTooltipUI value) => tooltip = value;
public void SetDescriptionText(TextMeshProUGUI value) => DescriptText = value;
```

`OnPointerClick`:

```csharp
if (eventData.button != PointerEventData.InputButton.Left) return;

if (eventData.clickCount == 1)
{
    if (currentSlot?.itemData != null && currentSlot.itemData.IsWeapon && inventory != null)
        inventory.EquipWeaponFromSlot(slotIndex);
}
else if (eventData.clickCount == 2)
{
    UseItem();
}
```

`UseItem` для `ItemType.Weapon` вызывает
`inventory.EquipWeaponFromSlot(slotIndex)`, а не пишет в лог.

- [ ] **Step 4: `InventorySlotUI` — безопасные теги и звук**

Добавить helper:

```csharp
private TextMeshProUGUI FindDescriptionText()
{
    if (DescriptText != null) return DescriptText;

    try
    {
        GameObject go = GameObject.FindGameObjectWithTag("DescriptObject");
        if (go != null) DescriptText = go.GetComponent<TextMeshProUGUI>();
    }
    catch (UnityException) { }

    return DescriptText;
}

private static bool HasTag(GameObject target, string tagName)
{
    if (target == null) return false;
    try { return target.CompareTag(tagName); }
    catch (UnityException) { return false; }
}
```

`OnPointerEnter`:
- обновить `Descript`;
- `TextMeshProUGUI text = FindDescriptionText(); if (text != null) text.text = Descript;`
- `if (SoundManager.Instance != null) SoundManager.Instance.PlaySound("Move", 0.3f);`
- `if (currentSlot?.itemData != null) tooltip?.Show(currentSlot.itemData, (RectTransform)transform);`

`OnPointerExit`:
```csharp
TextMeshProUGUI text = FindDescriptionText();
if (text != null) text.text = "";
tooltip?.Hide();
```
(нельзя писать `FindDescriptionText()?.text = ""` — null-conditional
оператор нельзя использовать как цель присваивания).

В `FindAndAssignDragIcon` обернуть
`GameObject.FindGameObjectWithTag("DragIcon")` в `try/catch (UnityException)`.
После поиска выставить `raycastTarget = false` у root `Image` **и** у
дочерней картинки (или у всех `Graphic` в `dragIcon`) — иначе drag-icon
перехватывает `RaycastAll` и drop на `TrashArea`/слот оружия не срабатывает.

Добавить helper для картинки перетаскивания (root `DragIcon.prefab` тоже
содержит `Image`, поэтому `GetComponentInChildren<Image>(true)` вернул бы
фон, а не дочернюю иконку):

```csharp
private static Image FindDragImage(GameObject dragIcon)
{
    if (dragIcon == null) return null;

    Transform child = dragIcon.transform.childCount > 0
        ? dragIcon.transform.GetChild(0)
        : null;

    Image image = child != null ? child.GetComponent<Image>() : null;
    return image != null ? image : dragIcon.GetComponent<Image>();
}
```

В `OnBeginDrag`:
- `tooltip?.Hide();`
- до проверки `dragIcon` запомнить `originalParent`/`originalPosition`;
- если `dragIcon == null` — восстановить `canvasGroup.alpha = 1f`,
  `canvasGroup.blocksRaycasts = true`, `isDragging = false` и выйти;
- заменить `dragIcon.transform.GetChild(0).GetComponent<Image>()` на
  `FindDragImage(dragIcon)` с null-check.

В `OnEndDrag`:
- если `EventSystem.current == null` — вернуть слот и выйти;
- если `inventory == null` — вернуть слот и выйти;
- заменить `trashArea.CompareTag("TrashArea")` на проверку тега у объекта
  **и его родителей** — подпись/дочерний Graphic мусорки может оказаться
  первым результатом `RaycastAll`:

```csharp
private static bool HasTagInParents(GameObject target, string tagName)
{
    Transform current = target != null ? target.transform : null;
    while (current != null)
    {
        if (HasTag(current.gameObject, tagName)) return true;
        current = current.parent;
    }
    return false;
}
```

В `OnEndDrag` использовать `HasTagInParents(trashArea, "TrashArea")`.

- [ ] **Step 5: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Inventary/Script/ItemTooltipUI.cs Assets/Inventary/Script/InventoryUI.cs Assets/Inventary/Script/InventorySlotUI.cs
git commit -m "feat(inventory): add click equip and item tooltip"
```

---

### Task 5: `AbilityData` и `AbilitySystem` — Block, кулдауны, loadout

**Files:**
- Modify: `Assets/Scripts/Combat/AbilityData.cs`
- Modify: `Assets/Scripts/Combat/AbilitySystem.cs`

**Interfaces:**
- Consumes: Task 2 `PlayCastAnimation(clip, autoResume)`, `StopCastAnimation`.
- Produces: `AbilityType.Block`, `AbilityData.blockDuration` (`float`).
- Produces: `AbilitySystem.GlobalAbilities` (`IReadOnlyList<AbilityData>`),
  `GetGlobalAbility(int)`, `AssignGlobalAbility(int, AbilityData) -> bool`,
  `ClearGlobalSlot(int)`, `IsWeaponSlot(int) -> bool`.
- Produces: кулдауны по `abilityId` через `Dictionary<string, float>`.

- [ ] **Step 1: Расширить `AbilityData`**

```csharp
public enum AbilityType
{
    Melee,
    Magic,
    Block
}
```

В конец класса:

```csharp
[Header("Block")]
public float blockDuration = 1.5f;
```

- [ ] **Step 2: Заменить кулдауны по индексу на словарь**

Удалить `private readonly float[] cooldownEnd`.
Добавить:

```csharp
private const int WeaponAbilitySlotLimit = 3;
private readonly Dictionary<string, float> cooldownEndById = new Dictionary<string, float>();
private readonly Dictionary<int, AbilityData> globalLoadout = new Dictionary<int, AbilityData>();
[SerializeField] private AbilityData[] defaultGlobalSlots = new AbilityData[SlotCount];
```

Helper:

```csharp
private static string CooldownKey(AbilityData ability)
{
    if (ability == null) return string.Empty;
    return string.IsNullOrEmpty(ability.abilityId) ? ability.name : ability.abilityId;
}
```

`GetCooldownRemaining(index)`:

```csharp
AbilityData ability = GetAbility(index);
if (ability == null) return 0f;
return cooldownEndById.TryGetValue(CooldownKey(ability), out float end)
    ? Mathf.Max(0f, end - Time.time)
    : 0f;
```

`GetCooldownFraction` — остаток / `ability.cooldown`.
`IsAbilityAvailable` — `GetCooldownRemaining(index) > 0f` вместо
`Time.time < cooldownEnd[index]`.
`TryActivate` — `cooldownEndById[CooldownKey(ability)] = Time.time + ability.cooldown`.
При неудачном старте melee/block — `cooldownEndById.Remove(CooldownKey(ability))`.

- [ ] **Step 3: Глобальный loadout 3–5**

`Awake` после поиска ссылок:

```csharp
if (defaultGlobalSlots != null)
{
    for (int i = 2; i < SlotCount && i < defaultGlobalSlots.Length; i++)
    {
        if (defaultGlobalSlots[i] != null)
            globalLoadout[i] = defaultGlobalSlots[i];
    }
}
RebuildSlots();
```

`RebuildSlots`:

```csharp
AbilityData[] next = new AbilityData[SlotCount];

for (int i = 0; i < WeaponAbilitySlotLimit && i < weaponAbilities.Count; i++)
    next[i] = weaponAbilities[i];

List<KeyValuePair<int, AbilityData>> assignments =
    new List<KeyValuePair<int, AbilityData>>(globalLoadout);
globalLoadout.Clear();

foreach (KeyValuePair<int, AbilityData> pair in assignments)
{
    AbilityData ability = pair.Value;
    if (ability == null || !globalAbilities.Contains(ability)) continue;

    int slot = pair.Key;
    if (slot < 2 || slot >= SlotCount || next[slot] != null)
        slot = FirstFreeGlobalSlot(next);

    if (slot < 0) continue; // остаётся в globalAbilities, но не в слотах

    next[slot] = ability;
    globalLoadout[slot] = ability;
}

for (int i = 0; i < SlotCount; i++)
    abilities[i] = next[i];

OnAbilitiesChanged?.Invoke();
```

`FirstFreeGlobalSlot(AbilityData[] array)` перебирает индексы 2..4 и
возвращает первый `null`.

`IsWeaponSlot(int slot)`:

```csharp
return slot >= 0 && slot < WeaponAbilitySlotLimit &&
       slot < weaponAbilities.Count && weaponAbilities[slot] != null;
```

`AssignGlobalAbility(int slot, AbilityData ability)`:
- `slot < 2 || slot >= SlotCount` → `false`;
- `ability == null || !globalAbilities.Contains(ability)` → `false`;
- `IsWeaponSlot(slot)` → `false`;
- удалить эту же способность из других ключей `globalLoadout`;
- `globalLoadout[slot] = ability; RebuildSlots(); return true;`.

`ClearGlobalSlot(int slot)` — только 2..4; `globalLoadout.Remove(slot)`;
`RebuildSlots()`.

`GetGlobalAbility(int slot)` — `TryGetValue`.
`GlobalAbilities => globalAbilities`.

`SetGlobalAbilities(List<AbilityData> list)`:
- обновить `globalAbilities`;
- удалить из `globalLoadout` способности, которых больше нет в списке;
- `RebuildSlots()`.

`SetAbilities` больше не сбрасывает `globalLoadout`; только
`weaponAbilities` + `RebuildSlots()`.

- [ ] **Step 4: Block**

`ExecuteAbility`:

```csharp
switch (ability.type)
{
    case AbilityType.Melee: yield return ExecuteMelee(index, ability); break;
    case AbilityType.Block: yield return ExecuteBlock(index, ability); break;
    default: yield return ExecuteMagic(index, ability); break;
}
```

`ExecuteBlock`:

```csharp
private IEnumerator ExecuteBlock(int index, AbilityData ability)
{
    if (weaponManager != null) weaponManager.SuppressHitbox = true;
    if (playerController != null) playerController.StopMovement();

    bool started = true;
    if (ability.castAnimation != null && weaponManager != null)
        started = weaponManager.PlayCastAnimation(ability.castAnimation, autoResume: false);

    if (!started)
    {
        cooldownEndById.Remove(CooldownKey(ability));
        if (weaponManager != null) weaponManager.SuppressHitbox = false;
        if (playerController != null) playerController.ResumeMovement();
        yield break;
    }

    float clipTime = ability.castAnimation != null ? ability.castAnimation.length / 1.25f : 0f;
    float duration = Mathf.Max(ability.blockDuration, clipTime);
    float elapsed = 0f;

    while (elapsed < duration)
    {
        // PlayerController.EndAttack может вернуть движение раньше —
        // держим StopMovement на протяжении всего блока.
        if (playerController != null) playerController.StopMovement();
        elapsed += Time.deltaTime;
        yield return null;
    }

    if (weaponManager != null)
    {
        weaponManager.SuppressHitbox = false;
        weaponManager.StopCastAnimation();
    }
    if (playerController != null) playerController.ResumeMovement();
}
```

`CancelCurrentAbility` дополнительно вызывает
`weaponManager.StopCastAnimation()`.

- [ ] **Step 5: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Combat/AbilityData.cs Assets/Scripts/Combat/AbilitySystem.cs
git commit -m "feat(combat): add block and ability-id cooldowns"
```

---

### Task 6: UI хотбара — номера слотов и цвет Block

**Files:**
- Modify: `Assets/Scripts/UI/AbilitySlotUI.cs`
- Modify: `Assets/Scripts/UI/AbilityBarUI.cs`

**Interfaces:**
- Consumes: Task 5 `AbilityType.Block`.
- Produces: подпись слота всегда `slotIndex + 1`; цвет Block — серый.

- [ ] **Step 1: `AbilitySlotUI.SetAbility`**

Заменить `keyLabel.text = GetKeyLabel(hotkey, slotIndex);` на:

```csharp
if (keyLabel != null)
    keyLabel.text = (slotIndex + 1).ToString();
```

Цвет плейсхолдера:

```csharp
if (ability == null)
    iconBaseColor = new Color(0.15f, 0.15f, 0.15f, 1f);
else if (ability.type == AbilityType.Melee)
    iconBaseColor = new Color(0.75f, 0.2f, 0.2f, 1f);
else if (ability.type == AbilityType.Block)
    iconBaseColor = new Color(0.55f, 0.55f, 0.55f, 1f);
else
    iconBaseColor = new Color(0.2f, 0.35f, 0.9f, 1f);
```

`GetKeyLabel` можно удалить, если он больше не используется.

- [ ] **Step 2: `AbilityBarUI.RefreshAbilities`**

Оставить вызов `slot.SetAbility(ability, i, hotkey)`; `AbilitySlotUI`
игнорирует `hotkey`. Убедиться, что `OnAbilitiesChanged` подписан и
`RefreshAbilities` вызывается (уже есть).

- [ ] **Step 3: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/UI/AbilitySlotUI.cs Assets/Scripts/UI/AbilityBarUI.cs
git commit -m "feat(ui): show slot numbers and block color"
```

---

### Task 7: `CharacterMenuController` и `SkillBookUI`

**Files:**
- Create: `Assets/Inventary/Script/CharacterMenuController.cs`
- Create: `Assets/Scripts/UI/SkillBookUI.cs`
- Modify: `Assets/Scripts/Combat/AbilitySystem.cs`

**Interfaces:**
- Consumes: Task 4 `InventoryUI.EnsureInitialized`; Task 5
  `AbilitySystem.GlobalAbilities`, `GetAbility`, `GetGlobalAbility`,
  `AssignGlobalAbility`, `ClearGlobalSlot`, `IsWeaponSlot`.
- Produces: `CharacterMenuController.AnyOpen` (`static bool`),
  `OpenInventory()`, `OpenSkillBook()`, `CloseAll()`.
- Produces: `SkillBookUI.EnsureInitialized()`, `Refresh()`.
- Produces: `AbilitySystem` gates `CharacterMenuController.AnyOpen`.

- [ ] **Step 1: Создать `CharacterMenuController.cs`**

Добавить `using UnityEngine.InputSystem;` (используется
`Keyboard.current`). Поля:

```csharp
[SerializeField] private GameObject inventoryPanel;
[SerializeField] private GameObject skillBookPanel;
[SerializeField] private InventoryUI inventoryUI;
[SerializeField] private SkillBookUI skillBookUI;
[SerializeField] private PlayerController playerController;
[SerializeField] private PlayerInteraction playerInteraction;
[SerializeField] private UiManager uiManager;

public static bool AnyOpen { get; private set; }
```

`Update`:
- `Keyboard.current == null` → выход;
- `uiManager != null && uiManager.IsOpen` → выход;
- `Escape` и `AnyOpen` → `CloseAll()`;
- `I` → если открыт инвентарь — `CloseAll()`, иначе `OpenInventory()`;
- `K` → если открыта книга — `CloseAll()`, иначе `OpenSkillBook()`.

`OpenInventory`:
1. `skillBookPanel?.SetActive(false)`;
2. `inventoryPanel?.SetActive(true)`;
3. `inventoryUI?.EnsureInitialized()`;
4. `EnterMenu()`.

`OpenSkillBook` — аналогично с `skillBookUI?.EnsureInitialized()`.

`EnterMenu` (только при `!AnyOpen`):
- `Time.timeScale = 0f`;
- если `playerController != null` — `playerController.enabled = false`;
- если `playerInteraction != null` — `playerInteraction.enabled = false`;
- если `uiManager != null` — запомнить `uiManagerWasEnabled = uiManager.enabled`
  и выставить `uiManager.enabled = false`; иначе `uiManagerWasEnabled = false`;
- `AnyOpen = true`.

`CloseAll`:
- скрыть обе панели;
- `Time.timeScale = 1f`;
- если `playerController != null` — включить;
  если `playerInteraction != null` — включить;
- если `uiManager != null` — `uiManager.enabled = uiManagerWasEnabled`;
- `AnyOpen = false`.

`OnDisable`/`OnDestroy` вызывают `CloseAll()`, если `AnyOpen`.

- [ ] **Step 2: Создать `SkillBookUI.cs`**

Добавить `using TMPro; using UnityEngine.UI;` (TMP_Text, Button, Image,
layout-компоненты). Поля:

```csharp
[SerializeField] private AbilitySystem abilitySystem;
[SerializeField] private CharacterMenuController menuController;
[SerializeField] private RectTransform magicListParent;
[SerializeField] private Button[] slotButtons = new Button[AbilitySystem.SlotCount];
[SerializeField] private Image[] slotIcons = new Image[AbilitySystem.SlotCount];
[SerializeField] private TMP_Text[] slotLabels = new TMP_Text[AbilitySystem.SlotCount];
[SerializeField] private TMP_Text descriptionText;
[SerializeField] private TMP_Text hintText;
```

`EnsureInitialized`:
- найти `abilitySystem` через `FindFirstObjectByType<AbilitySystem>(FindObjectsInactive.Include)`;
- подписаться на `OnAbilitiesChanged` (флаг);
- `BuildIfNeeded()`;
- `Refresh()`.

`BuildIfNeeded`:
- если массивы слотов `null`, короче 5 **или содержат `null`-элементы** —
  создать layout (массив длины 5 с пустыми ссылками — тоже повод строить):
  - заголовок «Книга умений»;
  - слева `MagicList` (`VerticalLayoutGroup`, `ContentSizeFitter`);
  - справа `Slots` (`HorizontalLayoutGroup`) с 5 кнопками 80×80;
  - снизу `DescriptionText` и `HintText`.
- каждая кнопка слота: `Image` фон, дочерний `Icon` (stretch),
  `KeyLabel` (top-left), `Button.onClick` → `OnSlotClicked(index)`.
- `magicListParent` — контейнер списка магии.

`Refresh`:
- для каждого слота: `abilitySystem.GetAbility(i)`, иконка, подпись
  `(i+1)`, цвет по типу (Melee — красноватый, Magic — синий, Block — серый,
  пусто — тёмный);
- пересоздать список магии из `abilitySystem.GlobalAbilities`:
  сначала `foreach (Transform child in magicListParent) Destroy(child.gameObject);`,
  затем кнопка с иконкой и именем, `onClick` → `SelectAbility(ability)`;
- показать описание выбранной/первой способности.

`OnSlotClicked(int index)`:
- если выбрана магия:
  - `abilitySystem.IsWeaponSlot(index)` → hint «Слот занят способностью оружия»;
  - `abilitySystem.AssignGlobalAbility(index, selected)` → hint «Назначено»;
  - иначе hint «Магия назначается только в слоты 3–5»;
- иначе если `abilitySystem.GetGlobalAbility(index) != null` →
  `abilitySystem.ClearGlobalSlot(index)`;
- иначе показать описание способности слота.

`SelectAbility(AbilityData ability)` — сохранить выбор и показать описание.

`ShowDescription(AbilityData ability)` — текст (`requiredWeapon` — enum
`WeaponType`, не `WeaponData`):

```text
<displayName>
Тип: <type>
Кулдаун: <cooldown> c
Требуется: <requiredWeapon == WeaponType.Any ? "Любое" : requiredWeapon.ToString()>
```

`OnDestroy` отписывается от `OnAbilitiesChanged`.

- [ ] **Step 3: Гейт `AbilitySystem`**

В `Update` и `TryActivate` после проверки `uiManager` добавить:

```csharp
if (CharacterMenuController.AnyOpen) return;
```

(в `TryActivate` — `return false;`).

- [ ] **Step 4: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Inventary/Script/CharacterMenuController.cs Assets/Scripts/UI/SkillBookUI.cs Assets/Scripts/Combat/AbilitySystem.cs
git commit -m "feat(ui): add character menu and skill book"
```

---

### Task 8: Сохранение функционального слота

**Files:**
- Modify: `Assets/Inventary/Script/Json/InventorySaveSystem.cs`

**Interfaces:**
- Consumes: Task 3 `WeaponSlotUI.EnsureInitialized`,
  `RestoreOwnedItem`, `OwnedItem`, `IsDisplayOnly`; Task 2 `WeaponManager`.
- Produces: корректные `equippedWeaponId`/`weaponSlotItemId` для
  функционального слота.

- [ ] **Step 1: `Start` и `RestoreWeapon`**

`Start`:

```csharp
if (loadOnStart)
{
    if (weaponSlot != null) weaponSlot.EnsureInitialized();
    LoadInventory();
}
```

`RestoreWeapon`:
- если `weaponSlot != null && !weaponSlot.IsDisplayOnly` —
  `weaponSlot.EnsureInitialized()` перед `RestoreOwnedItem`;
- `RestoreOwnedItem(slotItem)` оставить;
- `weaponManager.EquipWeapon(weapon)` оставить.

- [ ] **Step 2: Исправить `SaveInventory` — очищать старые id

В текущем коде `weaponSlotItemId` пишется только при `OwnedItem != null`,
поэтому после снятия оружия в файле остаётся старый id и при загрузке
предмет восстанавливается в слот (дубликат). Перед условиями выставить:

```csharp
saveData.equippedWeaponId = string.Empty;
saveData.weaponSlotItemId = string.Empty;
```

Затем существующие условия: `equippedWeaponId` — из
`weaponManager.CurrentWeapon`, иначе из `weaponSlot.OwnedWeapon`;
`weaponSlotItemId` — только для `!IsDisplayOnly` и `OwnedItem != null`.

- [ ] **Step 3: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Inventary/Script/Json/InventorySaveSystem.cs
git commit -m "feat(inventory): save functional weapon slot"
```

---

### Task 9: Setup-шаг 1 — клипы, способности, три меча, предметы

**Files:**
- Modify: `Assets/Editor/WeaponAssetSetup.cs`

**Interfaces:**
- Consumes: Task 1 `ItemData` aliases; Task 2
  `WeaponData.weaponMaterialOverride`; Task 5 `AbilityType.Block`,
  `AbilityData.blockDuration`.
- Produces: 8 `AbilityData`, 4 `WeaponData`, 4 `ItemData`, 2 материала,
  `Fists.overrideController`, `Sword.overrideController`.

- [ ] **Step 1: Добавить константы путей**

```csharp
private const string MaterialsDir = CombatRoot + "/Materials";
private const string FistsLowKickPath =
    "Assets/Unity Technologies/Combat animations - Kickboxing and Muay Thai V1/Animations/AmineSAADI@LOW_KICK_RIGHT.fbx";
private const string FistsBlockPath =
    "Assets/Unity Technologies/Combat animations - Kickboxing and Muay Thai V1/Animations/AmineSAADI@KICK_BLOCK.fbx";
private const string MagicCastPath =
    "Assets/Unity Technologies/Combat animations - Kickboxing and Muay Thai V1/Animations/AmineSAADI@CROSS 01.fbx";
private const string SwordAttack2Path =
    "Assets/Unity Technologies/Sword_sheath_AnimSet/Animation/Humanoid/attack_02.fbx";
private const string SwordBlockPath =
    "Assets/Unity Technologies/Sword_sheath_AnimSet/Animation/Humanoid/defense_01.fbx";
private const string SwordCrimsonPath = WeaponsDir + "/SwordCrimson.asset";
private const string SwordAzurePath = WeaponsDir + "/SwordAzure.asset";
private const string SwordCrimsonItemPath = ItemsDir + "/SwordCrimsonItem.asset";
private const string SwordAzureItemPath = ItemsDir + "/SwordAzureItem.asset";
private const string FistsLowKickAbilityPath = AbilitiesDir + "/FistsLowKick.asset";
private const string FistsBlockAbilityPath = AbilitiesDir + "/FistsBlock.asset";
private const string SwordHeavyPath = AbilitiesDir + "/SwordHeavy.asset";
private const string SwordBlockAbilityPath = AbilitiesDir + "/SwordBlock.asset";
private const string FrostNovaPath = AbilitiesDir + "/FrostNova.asset";
private const string SwordCrimsonMaterialPath = MaterialsDir + "/SwordCrimson.mat";
private const string SwordAzureMaterialPath = MaterialsDir + "/SwordAzure.mat";
private const string SwordSourceMaterialPath =
    "Assets/Assets/DarkFantasy/PolygonDarkFantasy/Materials/Alts/PolygonDarkFantasy_Mat_01_A.mat";
private const string MagicBurstVfxPath =
    "Assets/Unity Technologies/AllIn1VfxToolkit/Demo & Assets/Demo/Prefabs/Magic Explosive Spell.prefab";
private const string FrostNovaVfxPath =
    "Assets/Unity Technologies/AllIn1VfxToolkit/Demo & Assets/Demo/Prefabs/Blue Impact.prefab";
```

Иконки:

```csharp
private const string FistsJabIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_10_hand_fire_.png";
private const string FistsLowKickIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_10_hammer_.png";
private const string FistsBlockIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_1_shield_.png";
private const string SwordHeavyIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_2_sword45_.png";
private const string SwordBlockIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_4_shield_bezel_.png";
private const string SwordCrimsonIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_2_sword45_.png";
private const string SwordAzureIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_4_sword_saw_.png";
private const string FrostNovaIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_4_frost_.png";
```

- [ ] **Step 2: Настроить клипы**

В `CreateAssets` добавить `EnsureFolder(MaterialsDir);` и вызовы:

```csharp
if (!ConfigureClip(FistsLowKickPath, "LOW_KICK_RIGHT", false, true)) return;
if (!ConfigureClip(FistsBlockPath, "KICK_BLOCK", false, false)) return;
if (!ConfigureClip(MagicCastPath, "CROSS 01", false, false)) return;
if (!ConfigureClip(SwordAttack2Path, "attack_02", false, true)) return;
if (!ConfigureClip(SwordBlockPath, "defense_01", false, false)) return;
```

Загрузить клипы:

```csharp
AnimationClip fistsLowKick = LoadClip(FistsLowKickPath, "LOW_KICK_RIGHT");
AnimationClip fistsBlock = LoadClip(FistsBlockPath, "KICK_BLOCK");
AnimationClip magicCast = LoadClip(MagicCastPath, "CROSS 01");
AnimationClip swordAttack2 = LoadClip(SwordAttack2Path, "attack_02");
AnimationClip swordBlock = LoadClip(SwordBlockPath, "defense_01");
```

Добавить их в проверку на `null`.

- [ ] **Step 3: 8 `AbilityData`**

`CreateAbilityAssets` возвращает структуру с полями:

```csharp
FistsJab, FistsLowKick, FistsBlock,
SwordSlash, SwordHeavy, SwordBlock,
MagicBurst, FrostNova
```

Значения:

| Ability | id | type | cooldown | castAnimation | requiredWeapon | icon |
|---|---|---|---|---|---|---|
| FistsJab | `fists_jab` | Melee | 0.8 | `JAB` | Fists | `T_10_hand_fire_.png` |
| FistsLowKick | `fists_low_kick` | Melee | 1.5 | `LOW_KICK_RIGHT` | Fists | `T_10_hammer_.png` |
| FistsBlock | `fists_block` | Block | 4 | `KICK_BLOCK` | Fists | `T_1_shield_.png` |
| SwordSlash | `sword_slash` | Melee | 1.2 | `attack_01` | Sword | `T_5_sword_.png` |
| SwordHeavy | `sword_heavy` | Melee | 2.0 | `attack_02` | Sword | `T_2_sword45_.png` |
| SwordBlock | `sword_block` | Block | 4 | `defense_01` | Sword | `T_4_shield_bezel_.png` |
| MagicBurst | `magic_burst` | Magic | 3 | `CROSS 01` | Any | `T_3_magic_fire_.png` |
| FrostNova | `frost_nova` | Magic | 6 | `CROSS 01` | Any | `T_4_frost_.png` |

Для Block выставить `blockDuration = 1.5f`.
Для `MagicBurst`:
`magicRange = 2.5f`, `magicRadius = 2f`, `magicSpawnDistance = 1.5f`,
`vfxDelay = 0.2f`, `vfxLifetime = 2f`, `magicHitboxLifetime = 0.2f`,
`vfxPrefab = Magic Explosive Spell.prefab`.
Для `FrostNova`:
`magicRange = 3f`, `magicRadius = 3f`, `magicSpawnDistance = 1.2f`,
`vfxDelay = 0.2f`, `vfxLifetime = 2f`, `magicHitboxLifetime = 0.25f`,
`vfxPrefab = Blue Impact.prefab`.
`damageTags = { "DammageObject" }`, `damageLayers = ~0`.
`hotkey`: `Alpha1` для меле-способностей, `Alpha2` для магии (подсказка,
активация всё равно по номеру слота).

- [ ] **Step 4: Оружие и предметы**

`CreateWeaponAssets`:
- Fists: `abilities = { FistsJab, FistsLowKick, FistsBlock }`,
  `weaponMaterialOverride = null`.
- Sword: `abilities = { SwordSlash, SwordHeavy, SwordBlock }`,
  `weaponMaterialOverride = null`.
- SwordCrimson: тот же `animatorOverride`/`weaponModelPrefab`, те же
  `abilities = { SwordSlash, SwordHeavy, SwordBlock }`, `useAnimationEvents = true`,
  те же `weaponLocal*`/`hitbox*`, что у Sword; `weaponId = "sword_crimson"`,
  `displayName = "Багровый меч"`, `weaponMaterialOverride = SwordCrimson.mat`.
- SwordAzure: то же, что SwordCrimson; `weaponId = "sword_azure"`,
  `displayName = "Лазурный меч"`, `weaponMaterialOverride = SwordAzure.mat`.
- `CreateItemAssets` создаёт 4 предмета:
  - `FistsItem`: `fists_item`, «Кулаки», icon null;
  - `SwordItem`: `sword_item`, «Меч», `T_5_sword_.png`;
  - `SwordCrimsonItem`: `sword_crimson_item`, «Багровый меч»,
    `T_2_sword45_.png`;
  - `SwordAzureItem`: `sword_azure_item`, «Лазурный меч»,
    `T_4_sword_saw_.png`.
  Все `type = Weapon`, `stackable = false`, `maxStackSize = 1`,
  `consumable = false`.

- [ ] **Step 5: Материалы**

> **Ruling (preflight):** материалы создаются в `CreateAssets` **до** вызова
> `CreateWeaponAssets` — иначе на первом запуске `SwordCrimson.mat`/
> `SwordAzure.mat` ещё не существуют, и `weaponMaterialOverride` останется
> `null`. Порядок: `EnsureFolder(MaterialsDir)` → клипы → способности →
> материалы → `CreateWeaponAssets` → `CreateItemAssets`.

Создать/обновить:

```csharp
private static Material CreateOrUpdateMaterial(string path, Color color)
{
    Material source = AssetDatabase.LoadAssetAtPath<Material>(SwordSourceMaterialPath);
    if (source == null)
    {
        Debug.LogError("[WeaponAssetSetup] Не найден исходный материал: " +
                       SwordSourceMaterialPath);
        return null;
    }

    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

    if (material == null)
    {
        material = new Material(source);
        AssetDatabase.CreateAsset(material, path);
    }

    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
    if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    EditorUtility.SetDirty(material);
    return material;
}
```

Цвета: crimson `(0.55f, 0.06f, 0.06f, 1f)`, azure
`(0.08f, 0.25f, 0.65f, 1f)`.

- [ ] **Step 6: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 7: Запустить шаг 1**

В Unity: `Tools/Combat/1. Create Weapon Assets`.
Expected:
- 8 ability-ассетов, 4 weapon-ассета, 4 item-ассета, 2 материала;
- `Fists.asset`/`Sword*.asset` содержат по 3 способности;
- у `JAB`, `LOW_KICK_RIGHT`, `attack_01`, `attack_02` по 2 AnimationEvent;
- у block/magic клипов событий нет;
- повторный запуск не создаёт дубликаты.

- [ ] **Step 8: Commit**

```powershell
git add Assets/Editor/WeaponAssetSetup.cs
git commit -m "feat(editor): generate swords abilities and materials"
```

Сгенерированные `Assets/Combat/**` коммитятся после ручной проверки.

---

### Task 10: Setup-шаг 2 — сцены и `Player.prefab`

**Files:**
- Modify: `Assets/Editor/WeaponSceneSetup.cs`

**Interfaces:**
- Consumes: Task 9 assets.
- Produces: `Player.prefab` с `startingWeapon/defaultWeapon = Fists`,
  `globalAbilities = { MagicBurst, FrostNova }`, `defaultGlobalSlots[3..4]`.
- Produces: `Game.unity` HUD-слот и save-система со всеми 4 предметами.
- Produces: `SampleScene.unity` функциональный слот и 3 кнопки мечей.

- [ ] **Step 1: Загрузить новые ассеты**

Добавить константы:

```csharp
private const string SwordCrimsonPath = "Assets/Combat/Weapons/SwordCrimson.asset";
private const string SwordAzurePath = "Assets/Combat/Weapons/SwordAzure.asset";
private const string SwordCrimsonItemPath = "Assets/Combat/Items/SwordCrimsonItem.asset";
private const string SwordAzureItemPath = "Assets/Combat/Items/SwordAzureItem.asset";
private const string FrostNovaPath = "Assets/Combat/Abilities/FrostNova.asset";
```

В `WireAll` загрузить `swordCrimson`, `swordAzure`, `swordCrimsonItem`,
`swordAzureItem`, `frostNova`; добавить в проверку.

- [ ] **Step 2: `Player.prefab`**

В `WirePlayerPrefab`:
- `startingWeapon` = fists;
- `defaultWeapon` = fists;
- `globalAbilities` = `{ MagicBurst, FrostNova }`;
- `defaultGlobalSlots` — массив размера 5, `[3] = MagicBurst`,
  `[4] = FrostNova` (0-based; это слоты 4 и 5).

- [ ] **Step 3: `Game.unity`**

`WireGameScene`:
- `weaponItems` HUD-слота — все 4 предмета;
- `allItems` — все 4, `allWeapons` — все 4; добавлять через
  `EnsureObjectInList`, чтобы не стереть существующие предметы/оружие;
- HUD-слот остаётся `displayOnly = true`;
- `InventorySaveSystem.weaponSlot` пока HUD; `CombatFullSetup` заменит
  его на функциональный слот.

- [ ] **Step 4: `SampleScene.unity`**

`WireSampleScene`:
- функциональный слот с `weaponItems` — все 4;
- `allItems` — все 4, `allWeapons` — все 4; через `EnsureObjectInList`,
  чтобы не стереть существующие записи;
- заменить `CreateTestSwordButton` на общий
  `CreateTestItemButton(Inventory, ItemData, string buttonName, string label)`;
- создать 3 кнопки:
  - `SwordItemButton` — «Меч»;
  - `SwordCrimsonItemButton` — «Багровый»;
  - `SwordAzureItemButton` — «Лазурный».
- Каждая кнопка — persistent call на `Inventory.AddTestItem` с
  object-аргументом соответствующего `ItemData`.

- [ ] **Step 5: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 6: Запустить шаг 2**

В Unity: `Tools/Combat/2. Wire Player & Scene`.
Expected:
- `Player.prefab` содержит `WeaponManager`, `AbilitySystem`,
  `AnimationEventRelay`; `defaultWeapon = Fists`;
- `Game.unity` — HUD-слот со всеми 4 предметами, save-система со всеми
  ассетами;
- `SampleScene.unity` — функциональный слот и 3 кнопки;
- повторный запуск не создаёт дубликаты.

- [ ] **Step 7: Commit**

```powershell
git add Assets/Editor/WeaponSceneSetup.cs
git commit -m "feat(editor): wire three swords into scenes"
```

Сгенерированные `Player.prefab`, `Game.unity`, `SampleScene.unity`
коммитятся после ручной проверки.

---

### Task 11: `CombatFullSetup` — полная сборка `Game.unity`

**Files:**
- Create: `Assets/Editor/CombatFullSetup.cs`

**Interfaces:**
- Consumes: Task 9 `WeaponAssetSetup.CreateAssets()`; Task 10
  `WeaponSceneSetup.WireAll()`; Task 7 `CharacterMenuController`,
  `SkillBookUI`; Task 3 `WeaponSlotUI`; Task 8 `InventorySaveSystem`.
- Produces: `Tools/Combat/0. Full Setup`.

- [ ] **Step 1: Каркас меню и теги**

Нужны `using UnityEditor; using UnityEditor.Events;
using UnityEditor.SceneManagement; using UnityEngine;
using UnityEngine.SceneManagement; using UnityEngine.UI; using TMPro;`.
Каркас:

```csharp
[MenuItem("Tools/Combat/0. Full Setup")]
public static void Run()
{
    if (EditorApplication.isPlaying)
    {
        EditorUtility.DisplayDialog("Combat Setup", "Выйдите из Play mode.", "OK");
        return;
    }

    EnsureTag("TrashArea");
    EnsureTag("DescriptObject");
    WeaponAssetSetup.CreateAssets();
    WeaponSceneSetup.WireAll();
    BuildGameUi();
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
}
```

`EnsureTag(string tag)`:
- открыть `ProjectSettings/TagManager.asset` через
  `AssetDatabase.LoadAllAssetsAtPath(...)[0]`;
- `SerializedObject`, свойство `tags`;
- если тега нет — добавить;
- `ApplyModifiedPropertiesWithoutUndo()`.

- [ ] **Step 2: `InventorySystem` и ссылки**

В `BuildGameUi`:
- открыть `Game.unity` (`OpenSceneIfNeeded`);
- найти `Inventory` (IncludeInactive); если нет — создать
  `InventorySystem` с `Inventory` + `InventorySaveSystem`;
- найти `InventorySaveSystem` (в сцене может называться
  `WeaponSaveSystem`); если нет — создать;
- найти `WeaponManager`, `AbilitySystem`, `Canvas`, `UiManager`,
  `PlayerController`, `PlayerInteraction`;
- `Inventory` выставить `width = 5`, `height = 4` через `SerializedObject`;
- `Inventory.DescriptText` назначить на созданный `DescriptionText`.

- [ ] **Step 3: `InventoryPanel`**

Создать/пересоздать `InventoryPanel` под `Canvas` (неактивный в конце):
- full-screen `Image` (тёмный фон, `raycastTarget = true`);
- заголовок «Инвентарь»;
- `Grid` с `GridLayoutGroup` 5×4
  (`constraint = FixedColumnCount`, `constraintCount = 5`),
  `cellSize = 90×90`, `spacing = 6`;
- `InventoryUI` (`slotPrefab = Item.prefab`, `slotParent = Grid`,
  `inventory`, `descriptionText`, `tooltip`);
- `DragIcon` — instantiate `DragIcon.prefab`, имя `DragIcon`, неактивен;
- `DescriptionText` — TMP внизу, тег `DescriptObject`;
- `ItemTooltipUI` — панель 260×140 с `icon`, `nameText`,
  `descriptionText`, `typeText`; неактивна;
- `InventoryWeaponSlotPanel` — заголовок «Оружие», `WeaponSlot.prefab`,
  кнопка «Снять»;
- `TestItemsPanel` — 3 кнопки «Добавить: Меч / Багровый / Лазурный»;
  каждая кнопка — persistent call на `Inventory.AddTestItem` с
  object-аргументом соответствующего `ItemData` (как в
  `WeaponSceneSetup.CreateTestItemButton`, через `UnityEventTools`);
  без этого кнопки в `Game.unity` будут пустыми;
- кнопка «Закрыть» — persistent call на `CharacterMenuController.CloseAll`.

`WeaponSlotUI` назначить:
`inventory`, `weaponManager`, `saveSystem`, `displayOnly = false`,
`weaponItems` — все 4 предмета, `tooltip` — `ItemTooltipUI`,
`unequipButton` — кнопка «Снять».

Кнопка «Снять» — persistent call на `Inventory.UnequipWeapon`;
`WeaponSlotUI.RefreshVisual` сам делает её неактивной на кулаках.

- [ ] **Step 4: `SkillBookPanel` и `CharacterMenuController`**

Создать `SkillBookPanel` под `Canvas` (неактивный):
- `RectTransform` full-screen, тёмный фон;
- компонент `SkillBookUI`;
- назначить `abilitySystem`, `menuController`;
- дочерние элементы `SkillBookUI` создаёт в рантайме.

Добавить `CharacterMenuController` на `Canvas`:
- `inventoryPanel`, `skillBookPanel`, `inventoryUI`, `skillBookUI`,
  `playerController`, `playerInteraction`, `uiManager`.

- [ ] **Step 5: `TrashArea` и save-система**

Создать `TrashArea` **под `InventoryPanel`** (а не под `Canvas`), чтобы
зона не перехватывала клики, когда инвентарь закрыт:
- `Image` 100×100 в правом нижнем углу, `raycastTarget = true`;
- тег `TrashArea`;
- подпись «Мусор» с `raycastTarget = false` — иначе подпись станет первым
  результатом `RaycastAll`, и drop не увидит тег без проверки родителей
  (в Task 4 добавлена `HasTagInParents`).

`InventorySaveSystem`:
- `inventory` = найденный `Inventory`;
- `weaponSlot` = функциональный `WeaponSlotUI`;
- `weaponManager` = найденный;
- `allItems` — 4 предмета, `allWeapons` — 4 оружия; через
  `EnsureObjectInList`, чтобы не стереть существующие записи;
- `loadOnStart = true`.

`AbilitySystem`:
- `globalAbilities` = `{ MagicBurst, FrostNova }` (ровно 2, через `SetObjectList`);
- `defaultGlobalSlots` — явно `arraySize = 5`, `[3] = MagicBurst`,
  `[4] = FrostNova` (0-based; hotkeys 4 и 5).

- [ ] **Step 6: Сохранить сцену и проверить**

- `EditorSceneManager.MarkSceneDirty(scene); SaveScene(scene);`
- `AssetDatabase.SaveAssets(); AssetDatabase.Refresh();`
- повторный запуск: панели пересоздаются, дубликатов нет.

- [ ] **Step 7: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 8: Запустить полный setup**

В Unity: `Tools/Combat/0. Full Setup`.
Expected:
- в `Game.unity` есть `InventoryPanel`, `SkillBookPanel`,
  `CharacterMenuController`, `TrashArea`, `InventorySystem`;
- `InventorySaveSystem.weaponSlot` — функциональный слот;
- `AbilitySystem` содержит обе магии в слотах 4–5;
- `SampleScene` не сломан.

- [ ] **Step 9: Commit**

```powershell
git add Assets/Editor/CombatFullSetup.cs
git commit -m "feat(editor): add full combat ui setup"
```

---

### Task 12: Финальная проверка

**Files:**
- Verify only.

- [ ] **Step 1: Полная компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

Если Unity ещё не добавил новые файлы в `.csproj`, временно добавить
`<Compile Include="Assets/Inventary/Script/ItemTooltipUI.cs" />` и
`<Compile Include="Assets/Inventary/Script/CharacterMenuController.cs" />`
в `Assembly-CSharp.csproj`, собрать и убрать временные строки.

- [ ] **Step 2: Проверить неприкосновенность**

Run:

```powershell
git diff -- Assets/Garderob.cs Assets/Scripts/DestructibleObject.cs Assets/Scripts/Player/PlayerController.cs Assets/UiManager.cs Assets/PlayerAnimations/PlayerController.controller
```

Expected: пустой вывод.

- [ ] **Step 3: `Game.unity` — Play mode**

1. `Tools/Combat/0. Full Setup`, затем Play.
2. `I` открывает инвентарь, `K` — книгу умений, Escape закрывает;
   движение и способности на паузе не работают.
3. Кнопки «Добавить меч» кладут мечи в сетку; тултип показывает
   название/иконку/описание.
4. Клик по мечу экипирует; drag в слот оружия тоже работает.
5. Гардероб меняет оружие; слот показывает новое; старый предмет
   вернулся в сетку; хотбар обновился.
6. Гардероб не забирает предмет: `SwordItem` остаётся в сетке, слот
   показывает меч; кнопка «Снять» возвращает кулаки.
7. Заполнить инвентарь до отказа, сменить оружие через гардероб: старый
   предмет не теряется и возвращается после освобождения места.
8. Кулдаун способности не сбрасывается при смене оружия и возврате.
9. Кулаки: JAB, LOW_KICK, Block; меч: attack_01, attack_02, Block.
   Block останавливает движение на ~1.5 с и не включает хитбокс.
10. Магия: назначить MagicBurst/FrostNova в слоты 4–5; каст с VFX;
    временный trigger уничтожает `DestructibleObject`.
11. `useAnimationEvents = true/false` — проверить, что работает только
    один путь.
12. `SampleScene`: инвентарь и три меча добавляются, drag/клик не падают;
    hover по предмету не бросает `UnityException`.

- [ ] **Step 4: Проверить YAML сцен**

Run:

```powershell
Select-String -Path 'Assets/Scenes/Game.unity' -Pattern 'InventoryPanel|SkillBookPanel|CharacterMenuController|TrashArea|InventoryWeaponSlotPanel'
Select-String -Path 'Assets/Inventary/Scenes/SampleScene.unity' -Pattern 'SwordItemButton|SwordCrimsonItemButton|SwordAzureItemButton'
```

Expected: объекты есть, дубликатов имён нет.

- [ ] **Step 5: Проверить идемпотентность setup**

Повторно выполнить `Tools/Combat/1. Create Weapon Assets`,
`Tools/Combat/2. Wire Player & Scene`, `Tools/Combat/0. Full Setup`.
Expected: нет дубликатов панелей/кнопок/компонентов, ссылки не потеряны.

- [ ] **Step 6: Commit сгенерированных ассетов**

```powershell
git add Assets/Combat Assets/Player.prefab Assets/Scenes/Game.unity Assets/Inventary/Scenes/SampleScene.unity ProjectSettings/TagManager.asset
git commit -m "feat(combat): generate inventory and ability assets"
```

---

## Порядок применения для пользователя

1. Дождаться компиляции скриптов в Unity (или выполнить `dotnet build`).
2. `Tools/Combat/0. Full Setup`.
3. Открыть `Game.unity`, пройти проверку Task 12 Step 3.
4. Открыть `SampleScene.unity`, проверить три кнопки мечей и drag/клик.
5. При необходимости подстроить в инспекторе иконки, позицию меча,
   хитбокс и цвета материалов.
