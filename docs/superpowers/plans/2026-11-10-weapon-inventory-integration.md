# Интеграция оружия с инвентарём — план реализации

> **Для агента-исполнителя:** обязательный скилл — `superpowers:executing-plans`
> (native) или `superpowers:subagent-driven-development`. Шаги отмечаются
> чекбоксами. План продолжает уже выполненный
> `docs/superpowers/plans/2026-11-09-weapon-ability-system.md`.

**Goal:** Добавить слот оружия в инвентарь и HUD, перенос оружия
drag-and-drop из сетки в слот, глобальный набор магических способностей,
сохраняющийся между сменами оружия, и сохранение экипированного оружия в
`inventory_save.json` — не переписывая гардероб и существующий инвентарь.

**Architecture:** `WeaponSlotUI` — отдельный drop-target, который забирает
`ItemData` из сетки через новые методы `Inventory.TakeSlot/SetSlot`, вызывает
`WeaponManager.EquipWeapon(item.weaponData)` и синхронизируется с гардеробом
через новое событие `WeaponManager.OnWeaponEquipped`. `AbilitySystem` хранит
глобальный список магии и накладывает его поверх способностей оружия по
`AbilityData.hotkey`, сохраняя кулдауны по идентичности способности.
`InventorySaveSystem` дописывает в JSON `equippedWeaponId` и
`weaponSlotItemId`. Setup-скрипты создают `SwordItem`/`FistsItem`,
`WeaponSlot.prefab` и обвязывают обе сцены.

**Tech Stack:** Unity 6000.2.10f1, Assembly-CSharp (без asmdef), uGUI,
TextMeshPro, Input System (`Keyboard.current`), `JsonUtility`,
`AnimatorOverrideController`.

**Spec:** `docs/superpowers/specs/2026-11-09-weapon-ability-system-design.md`

## Global Constraints

- `Assets/Garderob.cs` **не изменять вообще**: `OnWeaponChanged` и
  `EquipWeapon(WeaponData)` уже есть, переименований нет.
- `Assets/Scripts/DestructibleObject.cs`, `Assets/Scripts/Drop/PlayerInteraction.cs`,
  `Assets/PlayerAnimations/PlayerController.controller` — не изменять.
- `Assets/Scripts/Player/PlayerController.cs` — не изменять; нужные методы и
  события уже добавлены в прошлой задаче.
- `HarvestableObject`/`Enemy`/`IInteractable`/здоровье не трогать.
- Урон — только через существующий тег `DammageObject` и
  `DestructibleObject.OnTriggerEnter`.
- `WeaponManager.EquipWeapon`: early-return при том же оружии сохраняется.
- `WeaponManager.startingWeapon` экипируется только если `currentWeapon == null`
  (чтобы сохранённое оружие не перебивалось стартовыми кулаками).
- `AbilitySystem.SetAbilities` больше не сбрасывает все кулдауны: кулдаун
  сохраняется, если та же `AbilityData` осталась в слотах.
- Глобальная магия накладывается поверх оружия по `AbilityData.hotkey`
  (`Alpha1..Alpha5`), при конфликте побеждает глобальная магия.
- Существующие `Knife1`/`KnifeDefault` не получают `WeaponData` автоматически:
  без `weaponData` слот оружия их отклоняет.
- `Game.unity`: HUD-слот только отображает и сохраняет оружие; инвентарной
  сетки там нет. Drag-and-drop работает в `SampleScene.unity`.
- `SampleScene.unity`: слот оружия — полноценный drop-target; игрока там нет,
  поэтому `WeaponManager` может быть `null`.
- `InventorySaveSystem` должен читать старый JSON без новых полей.
- Проект без Unity Test Framework: цикл проверки = компиляция Roslyn
  (`dotnet build Assembly-CSharp-Editor.csproj`) + ручная проверка в Play mode.

## Review Focus

1. **Drop на слот оружия vs мусорка/другая ячейка.** Raycast может попасть в
   дочерний `Icon`; `trashArea` может быть `null`; слот оружия не должен
   срабатывать как `TrashArea` и не должен терять предмет при отказе.
2. **Гардероб vs слот: последнее действие побеждает.** Кнопка гардероба и
   drop в слот вызывают один и тот же `EquipWeapon`; стартовые кулаки не должны
   переэкипироваться после загрузки сохранения.
3. **Глобальная магия и кулдауны.** Магия остаётся в слоте 1–5 при смене
   оружия, её кулдаун не сбрасывается, даже если слот сместился; меле-способность
   чужого оружия не активируется.
4. **Сохранение/загрузка.** Старый JSON без новых полей, пустой инвентарь в
   `Game.unity`, display-only слот, отсутствие `WeaponManager` в `SampleScene`.
5. **Идемпотентность setup-скриптов.** Повторный запуск не плодит панели,
   компоненты, кнопки и ассеты; обе сцены сохраняются, неактивный
   `GarderobCanvas` находится через `FindObjectsInactive.Include`.

---

### Task 1: Данные оружия и API перемещения слотов

**Files:**
- Modify: `Assets/Inventary/Script/SO/ItemData.cs`
- Modify: `Assets/Inventary/Script/Inventory.cs`

**Interfaces:**
- Produces: `ItemData.weaponData` (`WeaponData`).
- Produces: `Inventory.TakeSlot(int slotIndex) -> InventorySlot`.
- Produces: `Inventory.SetSlot(int slotIndex, ItemData item, int count) -> bool`.

- [ ] **Step 1: Добавить `weaponData` в `ItemData`**

В конец класса, после блока `[Header("Usage")]`:

```csharp
[Header("Weapon")]
public WeaponData weaponData;
```

Существующие поля и их порядок не менять.

- [ ] **Step 2: Добавить `TakeSlot` в `Inventory`**

После `MoveItem`:

```csharp
public InventorySlot TakeSlot(int slotIndex)
{
    if (slotIndex < 0 || slotIndex >= slots.Count) return null;

    InventorySlot taken = slots[slotIndex];
    slots[slotIndex] = new InventorySlot(null, 0);
    OnInventoryChanged?.Invoke(this);
    return taken;
}
```

- [ ] **Step 3: Добавить `SetSlot` в `Inventory`**

После `TakeSlot`:

```csharp
public bool SetSlot(int slotIndex, ItemData item, int count)
{
    if (slotIndex < 0 || slotIndex >= slots.Count) return false;

    if (item == null || count <= 0)
    {
        slots[slotIndex] = new InventorySlot(null, 0);
    }
    else
    {
        int max = Mathf.Max(1, item.maxStackSize);
        slots[slotIndex] = new InventorySlot(item, Mathf.Clamp(count, 1, max));
    }

    OnInventoryChanged?.Invoke(this);
    return true;
}
```

- [ ] **Step 4: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Inventary/Script/SO/ItemData.cs Assets/Inventary/Script/Inventory.cs
git commit -m "feat(inventory): add weapon data and slot transfer APIs"
```

---

### Task 2: `WeaponSlotUI` и drag-and-drop из сетки

**Files:**
- Create: `Assets/Scripts/UI/WeaponSlotUI.cs`
- Modify: `Assets/Inventary/Script/InventorySlotUI.cs`

**Interfaces:**
- Consumes: `ItemData.weaponData`, `Inventory.TakeSlot`, `Inventory.SetSlot` (Task 1).
- Produces: `WeaponSlotUI.SetVisualReferences(Image, TMP_Text, Image)`.
- Produces: `WeaponSlotUI.CanAccept(ItemData) -> bool`.
- Produces: `WeaponSlotUI.TryAccept(InventorySlotUI) -> bool`.
- Produces: `WeaponSlotUI.RestoreOwnedItem(ItemData, int = 1)`.
- Produces: `WeaponSlotUI.ApplyEquippedWeapon(WeaponData)`.
- Produces: `WeaponSlotUI.OwnedItem`, `OwnedWeapon`, `IsDisplayOnly`.

- [ ] **Step 1: Создать `WeaponSlotUI.cs`**

Глобальное пространство имён. Поля:

```csharp
[Header("Visual")]
[SerializeField] private Image icon;
[SerializeField] private TMP_Text nameLabel;
[SerializeField] private Image background;

[Header("References")]
[SerializeField] private Inventory inventory;
[SerializeField] private WeaponManager weaponManager;
[SerializeField] private InventorySaveSystem saveSystem;

[Header("Mode")]
[SerializeField] private bool displayOnly;
[SerializeField] private List<ItemData> weaponItems = new List<ItemData>();

public ItemData OwnedItem => ownedItem;
public WeaponData OwnedWeapon => ownedItem != null ? ownedItem.weaponData : null;
public bool IsDisplayOnly => displayOnly;
```

Приватное состояние: `ownedItem`, `ownedQuantity`, `displayItem`, `currentLabel`,
`isEquipped`.

`SetVisualReferences(Image iconImage, TMP_Text label, Image backgroundImage)`
присваивает ссылки.

`Awake`/`Start`:
- `inventory ??= FindFirstObjectByType<Inventory>()`;
- `weaponManager ??= FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include)`;
- `saveSystem ??= FindFirstObjectByType<InventorySaveSystem>(FindObjectsInactive.Include)`;
- если `weaponManager != null`: подписаться на `OnWeaponEquipped` и вызвать
  `ApplyEquippedWeapon(weaponManager.CurrentWeapon)`;
- иначе `RefreshVisual()`.
- В `OnDestroy` отписаться.

`CanAccept`:
```csharp
return !displayOnly && item != null &&
       item.type == ItemType.Weapon && item.weaponData != null;
```

`TryAccept(InventorySlotUI source)`:
1. Отклонить, если `displayOnly`, `source == null`, слот пуст или `CanAccept`
   вернул `false`.
2. Запомнить `incoming = source.GetSlot().itemData`,
   `incomingQuantity = source.GetSlot().quantity`,
   `sourceIndex = source.GetSlotIndex()`, `sourceInventory = source.GetInventory()`.
3. `sourceInventory.TakeSlot(sourceIndex)`.
4. Если в слоте оружия уже был предмет — вернуть его в освободившуюся ячейку:
   `sourceInventory.SetSlot(sourceIndex, previousItem, previousQuantity)`.
5. Записать `ownedItem/ownedQuantity/displayItem`.
6. Если `weaponManager != null` — `weaponManager.EquipWeapon(incoming.weaponData)`.
7. Вызвать `ApplyEquippedWeapon(incoming.weaponData)` — это выставит
   `isEquipped = true` даже когда `EquipWeapon` сделал early-return, потому что
   то же оружие уже было экипировано.
8. Если `saveSystem != null` — `saveSystem.SaveInventory()`.
9. Вернуть `true`.

`RestoreOwnedItem(ItemData item, int quantity = 1)`:
- при `displayOnly` просто выставить `displayItem = item` и обновить вид;
- иначе записать `ownedItem`, `ownedQuantity = item != null ? Mathf.Max(1, quantity) : 0`,
  `displayItem = ownedItem`, обновить вид.

`ApplyEquippedWeapon(WeaponData weapon)`:
- `displayOnly`: `displayItem = FindItemForWeapon(weapon)`,
  `currentLabel = displayItem != null ? displayItem.itemName :
  (weapon != null ? weapon.displayName : string.Empty)`, `isEquipped = weapon != null`.
- иначе: `isEquipped = ownedItem != null && ownedItem.weaponData == weapon`,
  `displayItem = ownedItem`, `currentLabel = ownedItem != null ? ownedItem.itemName : string.Empty`.
- Вызвать `RefreshVisual()`.

`FindItemForWeapon` ищет в `weaponItems` элемент с `weaponData == weapon`.

`RefreshVisual`:
- иконка: `displayItem != null ? displayItem.icon : null`; `icon.enabled` только
  если спрайт есть;
- цвет иконки: полный, если `isEquipped || displayOnly`, иначе alpha `0.45`;
- `nameLabel.text = currentLabel`;
- фон: активный `new Color(0.22f, 0.42f, 0.22f, 0.9f)`, обычный
  `new Color(0.08f, 0.08f, 0.08f, 0.9f)`.

- [ ] **Step 2: Научить `InventorySlotUI` класть оружие в слот**

В `OnEndDrag` добавить `WeaponSlotUI weaponSlot = null;` и в цикле raycast:

```csharp
if (targetSlot == null)
    targetSlot = result.gameObject.GetComponentInParent<InventorySlotUI>();
if (weaponSlot == null)
    weaponSlot = result.gameObject.GetComponentInParent<WeaponSlotUI>();
if (trashArea == null)
    trashArea = result.gameObject;
```

Убрать ранний `break`, чтобы найти все цели. Заменить развилку на:

```csharp
if (targetSlot != null && targetSlot != this)
{
    inventory.MoveItem(slotIndex, targetSlot.slotIndex);
}
else if (weaponSlot != null)
{
    if (!weaponSlot.TryAccept(this)) ReturnToOriginalPosition();
}
else if (trashArea != null && trashArea.CompareTag("TrashArea"))
{
    ShowDropConfirmation();
}
else
{
    ReturnToOriginalPosition();
}
```

`GetComponentInParent` обязателен: raycast попадает в `Icon`/`NameLabel`, а не
только в корень. `CompareTag` вместо `trashArea.tag` убирает NRE при пустом
raycast.

- [ ] **Step 3: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/UI/WeaponSlotUI.cs Assets/Inventary/Script/InventorySlotUI.cs
git commit -m "feat(inventory): add weapon slot UI and drag-to-equip"
```

---

### Task 3: Событие экипировки, глобальная магия, клики по слотам

**Files:**
- Modify: `Assets/Scripts/Combat/WeaponManager.cs`
- Modify: `Assets/Scripts/Combat/AbilitySystem.cs`
- Modify: `Assets/Scripts/UI/AbilitySlotUI.cs`
- Modify: `Assets/Scripts/UI/AbilityBarUI.cs`

**Interfaces:**
- Produces: `WeaponManager.OnWeaponEquipped` (`event Action<WeaponData>`).
- Produces: `AbilitySystem.SetGlobalAbilities(List<AbilityData>)`.
- Produces: `AbilitySystem.ActivateAbility(int) -> bool`.
- Produces: клик по `AbilitySlotUI` активирует способность своего слота.

- [ ] **Step 1: `WeaponManager` — событие и защита стартового оружия**

Добавить `using System;`. После `CurrentWeapon`:

```csharp
public event Action<WeaponData> OnWeaponEquipped;
```

В `Start` заменить:

```csharp
if (startingWeapon != null && currentWeapon == null)
    EquipWeapon(startingWeapon);
```

В конце `EquipWeapon`, после `playerController.ResetHitboxState()`:

```csharp
OnWeaponEquipped?.Invoke(weapon);
```

- [ ] **Step 2: `AbilitySystem` — глобальные способности и кулдауны**

Добавить поле:

```csharp
[SerializeField] private List<AbilityData> globalAbilities = new List<AbilityData>();
```

Добавить `private readonly List<AbilityData> weaponAbilities = new List<AbilityData>();`.

В `Awake` в конце вызвать `RebuildSlots();`.

Заменить `SetAbilities` на:

```csharp
public void SetAbilities(List<AbilityData> list)
{
    weaponAbilities.Clear();
    if (list != null) weaponAbilities.AddRange(list);
    RebuildSlots();
}

public void SetGlobalAbilities(List<AbilityData> list)
{
    globalAbilities.Clear();
    if (list != null) globalAbilities.AddRange(list);
    RebuildSlots();
}
```

Добавить `RebuildSlots`:
1. `AbilityData[] next = new AbilityData[SlotCount];`
2. Скопировать `weaponAbilities` по индексам 0..4.
3. Для каждой `globalAbilities`:
   - `int slot = HotkeyToSlot(ability.hotkey);`
   - если `slot < 0` — первый свободный слот `FirstFreeSlot(next)`;
   - если `slot >= 0` — `next[slot] = ability` (глобальная магия перекрывает
     оружие).
4. Сохранить копии `AbilityData[] oldAbilities = (AbilityData[])abilities.Clone();`
   и `float[] oldCooldown = (float[])cooldownEnd.Clone();`.
5. Для каждого слота:
   - `abilities[i] = next[i];`
   - `int oldIndex = IndexOf(oldAbilities, next[i]);`
   - `cooldownEnd[i] = oldIndex >= 0 ? oldCooldown[oldIndex] : 0f;`
6. `OnAbilitiesChanged?.Invoke();`.

`HotkeyToSlot(KeyCode hotkey)`:
```csharp
int index = (int)hotkey - (int)KeyCode.Alpha1;
return index >= 0 && index < SlotCount ? index : -1;
```

`IndexOf(AbilityData[] array, AbilityData value)` возвращает первый индекс
совпадения или `-1`; `null` → `-1`.

`FirstFreeSlot(AbilityData[] array)` возвращает первый `null` или `-1`.

- [ ] **Step 3: `AbilitySystem` — публичная активация и защита**

Добавить:

```csharp
public bool ActivateAbility(int index) => TryActivate(index);
```

В начало `TryActivate` добавить:

```csharp
if (index < 0 || index >= SlotCount) return false;
if (Time.timeScale <= 0f) return false;
if (uiManager != null && uiManager.IsOpen) return false;
```

Остальные проверки (слот, активная способность, кулдаун, совместимость)
сохранить.

- [ ] **Step 4: `AbilitySlotUI` — клик**

Добавить `using UnityEngine.EventSystems;`, интерфейс
`IPointerClickHandler`, поле `private int slotIndex;`.

В `SetAbility` сохранить `this.slotIndex = slotIndex;`.

Добавить:

```csharp
public void OnPointerClick(PointerEventData eventData)
{
    if (eventData.button != PointerEventData.InputButton.Left) return;

    AbilitySystem system = FindFirstObjectByType<AbilitySystem>();
    if (system != null) system.ActivateAbility(slotIndex);
}
```

- [ ] **Step 5: `AbilityBarUI` — кликабельный fallback-слот**

В `BuildFallbackSlot` у корневого `Image` выставить
`background.raycastTarget = true;` (сейчас `false`, из-за чего клик не
доходит до `AbilitySlotUI`). У `Icon`/`CooldownOverlay` оставить `false`.

- [ ] **Step 6: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 7: Commit**

```powershell
git add Assets/Scripts/Combat/WeaponManager.cs Assets/Scripts/Combat/AbilitySystem.cs Assets/Scripts/UI/AbilitySlotUI.cs Assets/Scripts/UI/AbilityBarUI.cs
git commit -m "feat(combat): add equip event and global magic loadout"
```

---

### Task 4: Сохранение экипированного оружия

**Files:**
- Modify: `Assets/Inventary/Script/Json/InventorySaveSystem.cs`

**Interfaces:**
- Consumes: `WeaponSlotUI.OwnedItem`, `OwnedWeapon`, `IsDisplayOnly`,
  `RestoreOwnedItem`, `ApplyEquippedWeapon` (Task 2);
  `WeaponManager.CurrentWeapon`, `EquipWeapon` (Task 3).
- Produces: JSON-поля `equippedWeaponId`, `weaponSlotItemId`.
- Produces: `[SerializeField] bool loadOnStart`.

- [ ] **Step 1: Расширить `InventorySaveData`**

```csharp
[System.Serializable]
public class InventorySaveData
{
    public List<InventorySlotSaveData> slots = new List<InventorySlotSaveData>();
    public string equippedWeaponId;
    public string weaponSlotItemId;
}
```

Старые JSON без этих полей читаются: `JsonUtility` оставит `null`.

- [ ] **Step 2: Добавить поля в `InventorySaveSystem`**

```csharp
[Header("Weapon")]
[SerializeField] private WeaponSlotUI weaponSlot;
[SerializeField] private WeaponManager weaponManager;
[SerializeField] private List<WeaponData> allWeapons = new List<WeaponData>();
[SerializeField] private bool loadOnStart;
```

Добавить `Start`:
```csharp
private void Start()
{
    if (loadOnStart) LoadInventory();
}
```

- [ ] **Step 3: Обновить `SaveInventory`**

- Сначала загрузить существующий JSON, если файл есть:
  ```csharp
  InventorySaveData saveData = null;
  if (File.Exists(savePath))
  {
      try { saveData = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(savePath)); }
      catch { saveData = null; }
  }
  if (saveData == null) saveData = new InventorySaveData();
  if (saveData.slots == null) saveData.slots = new List<InventorySlotSaveData>();
  ```
  Это критично: `Game.unity` не имеет инвентарной сетки и не должен затирать
  `slots`, сохранённые из `SampleScene`.
- Если `inventory != null` — очистить `saveData.slots` и заново собрать сетку
  (существующая логика). Если `inventory == null` — оставить `slots` из
  прочитанного файла без изменений.
- `equippedWeaponId`:
  - `weaponManager != null && weaponManager.CurrentWeapon != null` →
    `weaponManager.CurrentWeapon.weaponId`;
  - иначе `weaponSlot != null && weaponSlot.OwnedWeapon != null` →
    `weaponSlot.OwnedWeapon.weaponId`.
- `weaponSlotItemId` — только если слот не display-only:
  `weaponSlot != null && !weaponSlot.IsDisplayOnly && weaponSlot.OwnedItem != null`
  → `weaponSlot.OwnedItem.itemId`.
- Если `inventory == null && weaponManager == null && weaponSlot == null` —
  выйти без записи файла.
- Запись файла — как сейчас: `JsonUtility.ToJson(saveData, true)`.

- [ ] **Step 4: Обновить `LoadInventory`**

- После чтения JSON:
  - если `saveData == null` — `Debug.LogWarning` и выйти (битый/пустой файл);
  - если `inventory != null`: `ClearInventory()` и восстановить сетку;
    добавить проверку `if (saveData.slots != null)`;
  - вызвать `RestoreWeapon(saveData)`.
- `RestoreWeapon`:
  ```csharp
  private void RestoreWeapon(InventorySaveData saveData)
  {
      if (weaponSlot != null && !weaponSlot.IsDisplayOnly)
      {
          ItemData slotItem = FindItemById(saveData.weaponSlotItemId);
          weaponSlot.RestoreOwnedItem(slotItem);
      }

      WeaponData weapon = FindWeaponById(saveData.equippedWeaponId);
      if (weapon == null && weaponSlot != null)
          weapon = weaponSlot.OwnedWeapon;

      if (weapon != null && weaponManager != null)
          weaponManager.EquipWeapon(weapon);
  }
  ```
- `FindWeaponById(string weaponId)` ищет в `allWeapons` по
  `weapon.weaponId`; пустой id → `null`.
- В `FindItemById` и `FindWeaponById` добавить защиту от `null` списка:
  `if (allItems == null) return null;` / `if (allWeapons == null) return null;`.
- Для display-only слота `RestoreOwnedItem` не вызывать: HUD обновится сам
  через `WeaponManager.OnWeaponEquipped`.

- [ ] **Step 5: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Inventary/Script/Json/InventorySaveSystem.cs
git commit -m "feat(inventory): persist equipped weapon and weapon slot"
```

---

### Task 5: Setup-шаг 1 — предметы оружия и prefab слота

**Files:**
- Modify: `Assets/Editor/WeaponAssetSetup.cs`
- Generated after запуска: `Assets/Combat/Items/SwordItem.asset`,
  `Assets/Combat/Items/FistsItem.asset`, `Assets/Combat/UI/WeaponSlot.prefab`

**Interfaces:**
- Consumes: `ItemData.weaponData`, `WeaponSlotUI.SetVisualReferences` (Tasks 1–2).
- Produces: `SwordItem.asset` (`itemId = "sword_item"`,
  `weaponData = Sword.asset`).
- Produces: `FistsItem.asset` (`itemId = "fists_item"`,
  `weaponData = Fists.asset`).
- Produces: `WeaponSlot.prefab` с `WeaponSlotUI` и визуальными ссылками.

- [ ] **Step 1: Добавить пути и папку**

Константы:
```csharp
private const string ItemsDir = CombatRoot + "/Items";
private const string SwordItemPath = ItemsDir + "/SwordItem.asset";
private const string FistsItemPath = ItemsDir + "/FistsItem.asset";
private const string WeaponSlotPrefabPath = UiDir + "/WeaponSlot.prefab";
private const string SwordIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_5_sword_.png";
private const string MagicIconPath =
    "Assets/Ideals/Menu/UI/Clean Vector Icons/T_3_magic_fire_.png";
```

В `CreateAssets` добавить `EnsureFolder(ItemsDir);`. Путь к анимации
`AmineSAADI@FIGHTING_SWAY.fbx` уже правильный — не менять.

- [ ] **Step 2: Иконки способностей**

В `CreateAbilityAssets`:
- `swordSlash.icon = AssetDatabase.LoadAssetAtPath<Sprite>(SwordIconPath);`
- `magicBurst.icon = AssetDatabase.LoadAssetAtPath<Sprite>(MagicIconPath);`
- `fistsJab.icon` оставить `null` (в UI будет цветной плейсхолдер).

- [ ] **Step 3: Сделать магию глобальной**

В `CreateWeaponAssets` заменить способности меча на:

```csharp
sword.abilities = new List<AbilityData> { abilities.SwordSlash };
```

`MagicBurst` больше не принадлежит мечу: он будет назначен в
`AbilitySystem.globalAbilities` на prefab игрока (Task 6).

- [ ] **Step 4: Создать `SwordItem` и `FistsItem`**

В конце `CreateWeaponAssets`, где `fists` и `sword` уже созданы, вызвать
`CreateItemAssets(fists, sword)`:

```csharp
private static void CreateItemAssets(WeaponData fists, WeaponData sword)
{
    ItemData swordItem = CreateOrLoadAsset<ItemData>(SwordItemPath);
    swordItem.itemId = "sword_item";
    swordItem.itemName = "Меч";
    swordItem.description = "Одноручный меч.";
    swordItem.icon = AssetDatabase.LoadAssetAtPath<Sprite>(SwordIconPath);
    swordItem.type = ItemType.Weapon;
    swordItem.stackable = false;
    swordItem.maxStackSize = 1;
    swordItem.consumable = false;
    swordItem.weaponData = sword;
    EditorUtility.SetDirty(swordItem);

    ItemData fistsItem = CreateOrLoadAsset<ItemData>(FistsItemPath);
    fistsItem.itemId = "fists_item";
    fistsItem.itemName = "Кулаки";
    fistsItem.description = "Без оружия.";
    fistsItem.icon = null;
    fistsItem.type = ItemType.Weapon;
    fistsItem.stackable = false;
    fistsItem.maxStackSize = 1;
    fistsItem.consumable = false;
    fistsItem.weaponData = fists;
    EditorUtility.SetDirty(fistsItem);
}
```

- [ ] **Step 5: Создать `WeaponSlot.prefab`**

Если ассета нет:
- root `WeaponSlot` 96×96: `RectTransform`, `CanvasRenderer`, `Image`
  (тёмный фон, `raycastTarget = true`), `WeaponSlotUI`;
- `Icon`: stretch, padding 8, `Image`, `raycastTarget = false`;
- `NameLabel`: низ, высота 22, `TextMeshProUGUI`, fontSize 12, центр,
  `raycastTarget = false`;
- вызвать `slotUi.SetVisualReferences(iconImage, nameLabel, backgroundImage)`;
- `PrefabUtility.SaveAsPrefabAsset(root, WeaponSlotPrefabPath)`, удалить
  временный объект.

Вызвать `CreateWeaponSlotPrefab()` из `CreateAssets` после
`CreateAbilitySlotPrefab()`.

- [ ] **Step 6: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 7: Запустить шаг 1**

В Unity: `Tools/Combat/1. Create Weapon Assets` (или `execute_menu_item`,
если MCP-мост доступен).
Expected:
- `Assets/Combat/Items/SwordItem.asset`, `FistsItem.asset` существуют;
- `Assets/Combat/UI/WeaponSlot.prefab` существует;
- `Sword.asset` содержит только `SwordSlash`; `MagicBurst` — отдельный ассет;
- повторный запуск не создаёт дубликаты.

- [ ] **Step 8: Commit**

```powershell
git add Assets/Editor/WeaponAssetSetup.cs
git commit -m "feat(editor): generate weapon items and weapon slot prefab"
```

Сгенерированные `Assets/Combat/**` коммитятся после ручной проверки (Task 7).

---

### Task 6: Setup-шаг 2 — обвязка `Game.unity` и `SampleScene.unity`

**Files:**
- Modify: `Assets/Editor/WeaponSceneSetup.cs`
- Modified by script: `Assets/Player.prefab`, `Assets/Scenes/Game.unity`,
  `Assets/Inventary/Scenes/SampleScene.unity`

**Interfaces:**
- Consumes: `Fists.asset`, `Sword.asset`, `SwordItem.asset`, `FistsItem.asset`,
  `MagicBurst.asset`, `AbilitySlot.prefab`, `WeaponSlot.prefab` (Task 5).
- Produces: `AbilitySystem.globalAbilities = { MagicBurst }` на `Player.prefab`.
- Produces: HUD `WeaponSlotPanel` + `WeaponSaveSystem` в `Game.unity`.
- Produces: `WeaponSlotPanel` + тестовая кнопка «Меч» в `SampleScene.unity`.

- [ ] **Step 1: Загрузить новые ассеты и пути**

Добавить `using System.Collections.Generic;` в начало файла.

Константы:
```csharp
private const string SampleScenePath = "Assets/Inventary/Scenes/SampleScene.unity";
private const string SwordItemPath = "Assets/Combat/Items/SwordItem.asset";
private const string FistsItemPath = "Assets/Combat/Items/FistsItem.asset";
private const string MagicBurstPath = "Assets/Combat/Abilities/MagicBurst.asset";
private const string WeaponSlotPrefabPath = "Assets/Combat/UI/WeaponSlot.prefab";
```

В `WireAll` загрузить `swordItem`, `fistsItem`, `magicBurst`, `weaponSlotPrefab`
и добавить их в проверку «не найдены ассеты шага 1».

- [ ] **Step 2: `Player.prefab` — глобальная магия**

`WirePlayerPrefab` принимает `fists`, `magicBurst`. В `Assign(abilitySystem, ...)`
добавить:

```csharp
SerializedProperty global = so.FindProperty("globalAbilities");
global.ClearArray();
global.InsertArrayElementAtIndex(0);
global.GetArrayElementAtIndex(0).objectReferenceValue = magicBurst;
```

- [ ] **Step 3: `Game.unity` — HUD-слот и сохранение**

`WireGameScene(...)`:
1. Открыть `Game.unity` (существующая логика `OpenSceneIfNeeded`).
2. `Canvas` + `AbilityBarUI` — как сейчас.
3. `GarderobCanvas` + `WeaponPanel` — как сейчас.
4. Найти `WeaponManager` (IncludeInactive) и `InventorySaveSystem`. Если
   save-системы нет — создать GameObject `WeaponSaveSystem` с компонентом.
5. Создать `WeaponSlotPanel` под `Canvas`:
   - `displayOnly = true`;
   - `inventory = null`;
   - `weaponManager` — найденный;
   - `saveSystem` — найденный/созданный;
   - `weaponItems = { SwordItem, FistsItem }`.
6. Настроить save-систему:
   - `weaponSlot` — новый слот;
   - `weaponManager` — найденный;
   - добавить `SwordItem` и `FistsItem` в `allItems` через
     `EnsureObjectInList` (не очищать существующий список);
   - добавить `Fists` и `Sword` в `allWeapons` через `EnsureObjectInList`;
   - `loadOnStart = true`;
   - `inventory` оставить `null`.
7. Сохранить сцену.

- [ ] **Step 4: `SampleScene.unity` — drop-target и сохранение**

`WireSampleScene(...)`:
1. Открыть `SampleScene.unity`.
2. Найти `Inventory` и `InventoryCanvas`.
3. Найти `InventorySaveSystem` (в сцене объект называется
   `InventarySaveSystem`). Если нет — создать.
4. Создать `WeaponSlotPanel` под `InventoryCanvas`:
   - `displayOnly = false`;
   - `inventory` — найденный;
   - `weaponManager = null`;
   - `saveSystem` — найденный;
   - `weaponItems = { SwordItem, FistsItem }`.
5. Настроить save-систему:
   - `inventory` — найденный;
   - `weaponSlot` — новый слот;
   - `weaponManager = null`;
   - добавить `SwordItem` и `FistsItem` в `allItems` (не удаляя Case, Knife1,
     KnifeDefault, potion);
   - `allWeapons = { Fists, Sword }`;
   - `loadOnStart = false` (загрузка остаётся по существующей кнопке Load).
6. Создать тестовую кнопку `SwordItemButton` в панели `Save` (GridLayoutGroup,
   где лежат Case/Potion):
   - `Button` + `Image` + TMP-подпись «Меч»;
   - persistent call на `Inventory.AddTestItem` с object-аргументом `SwordItem`
     (`m_Mode = 2`, `m_ObjectArgumentAssemblyTypeName = "ItemData, Assembly-CSharp"`).
     Явно обнулить остальные аргументы: `m_IntArgument = 0`,
     `m_FloatArgument = 0`, `m_StringArgument = ""`, `m_BoolArgument = false`.
   - Если кнопка уже есть — удалить и создать заново.
7. Сохранить сцену, в конце открыть `Game.unity`.

- [ ] **Step 5: Хелперы setup-скрипта**

`CreateWeaponSlotPanel(Transform parent, string name, GameObject weaponSlotPrefab,
Inventory inventory, WeaponManager weaponManager, InventorySaveSystem saveSystem,
List<ItemData> weaponItems, bool displayOnly) -> WeaponSlotUI`:
- удалить существующую панель с тем же именем;
- создать панель `Image + VerticalLayoutGroup + ContentSizeFitter +
  LayoutElement` (`ignoreLayout = true`), якорь top-right, отступ 24;
- заголовок TMP «Оружие»;
- `PrefabUtility.InstantiatePrefab(weaponSlotPrefab, panel.transform)`,
  `LayoutElement.preferredWidth/Height = 96`;
- через `SerializedObject` назначить `inventory`, `weaponManager`, `saveSystem`,
  `displayOnly`, `weaponItems`.

`SetObjectList<T>(SerializedProperty, IList<T>)` и
`EnsureObjectInList(SerializedProperty, Object)` — для `allItems`/`allWeapons`.
`EnsureObjectInList` не должен дублировать уже существующие ссылки.

- [ ] **Step 6: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 7: Запустить шаг 2**

В Unity: `Tools/Combat/2. Wire Player & Scene` (или `execute_menu_item`).
Expected:
- на `Player.prefab` есть `WeaponManager`, `AbilitySystem`,
  `AnimationEventRelay`; `AbilitySystem.globalAbilities = { MagicBurst }`;
- в `Game.unity` на `Canvas` есть `AbilityBarUI` и `WeaponSlotPanel`
  (display-only), на сцене — `WeaponSaveSystem`;
- в `SampleScene.unity` под `InventoryCanvas` есть `WeaponSlotPanel`, в панели
  `Save` — кнопка «Меч»; `InventorySaveSystem` ссылается на слот;
- повторный запуск не создаёт дубликаты.

- [ ] **Step 8: Commit**

```powershell
git add Assets/Editor/WeaponSceneSetup.cs
git commit -m "feat(editor): wire weapon slot into game and sample scenes"
```

Сгенерированные `Player.prefab`, `Game.unity`, `SampleScene.unity` коммитятся
после ручной проверки (Task 7).

---

### Task 7: Финальная проверка

**Files:**
- Verify only.

- [ ] **Step 1: Полная компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`.

Если Unity ещё не добавил новые файлы в сгенерированный `.csproj`, временно
добавить `<Compile Include="Assets/Scripts/UI/WeaponSlotUI.cs" />` в
`Assembly-CSharp.csproj`, собрать и убрать временную строку.

- [ ] **Step 2: Проверить неприкосновенность**

Run:
```powershell
git diff -- Assets/Garderob.cs Assets/Scripts/DestructibleObject.cs Assets/Scripts/Player/PlayerController.cs Assets/PlayerAnimations/PlayerController.controller
```
Expected: пустой вывод. `Garderob.cs` не изменялся, переименований нет.

- [ ] **Step 3: `Game.unity` — Play mode**

- HUD-слот справа сверху показывает «Кулаки» (FistsItem) после старта.
- Панель способностей: слот 1 — «Джеб», слот 2 — «Магический взрыв»
  (глобальная магия), хотя у кулаков в `WeaponData.abilities` только джеб.
- Кнопка гардероба «Меч»: модель в правой руке, HUD-слот показывает «Меч»,
  слот 1 — «Удар мечом», слот 2 — «Магический взрыв».
- Нажать 2 (магия) с кулаками и с мечом: создаётся временная зона
  `MagicHitbox_Temp` с тегом `DammageObject`.
- Нанести магией урон, затем переключить оружие: кулдаун магии не сбрасывается,
  если `MagicBurst` остался в том же слоте; при смене hotkey слот меняется,
  кулдаун сохраняется по идентичности способности.
- Клик по слоту способности активирует её (при открытом `UiManager` — нет).
- Выйти из Play mode после экипировки меча: в
  `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\inventory_save.json`
  есть `"equippedWeaponId": "sword"`, а массив `slots` не затёрт пустым
  значением.
- Снова войти в Play mode: меч восстановлен, стартовые кулаки его не перебили.
- Открыть `SampleScene.unity`, нажать Load: предметы сетки (Case, Knife1,
  KnifeDefault, potion) на месте — `Game.unity` не затёр `slots`.

- [ ] **Step 4: `SampleScene.unity` — drag-and-drop**

- Нажать тестовую кнопку «Меч»: `SwordItem` появляется в сетке.
- Перетащить `SwordItem` на слот оружия: предмет исчезает из сетки, появляется
  в слоте, в консоли нет ошибок (игрока/`WeaponManager` в сцене нет — это
  нормально).
- Перетащить зелье на слот: отказ, предмет возвращается, слот не меняется.
- Перетащить меч на `TrashArea`: работает существующая логика подтверждения,
  NRE нет.
- Нажать Save (существующая кнопка) или выйти: в JSON есть
  `"weaponSlotItemId": "sword_item"` и `"equippedWeaponId": "sword"`.
- Перезапустить Play mode, нажать Load: меч восстанавливается в слоте оружия,
  а не в сетке.

- [ ] **Step 5: Проверить идемпотентность setup**

Повторно выполнить `Tools/Combat/1. Create Weapon Assets` и
`Tools/Combat/2. Wire Player & Scene`.
Expected: нет дубликатов `WeaponSlotPanel`, `WeaponPanel`, `WeaponSaveSystem`,
`SwordItemButton`; ссылки не потеряны.

- [ ] **Step 6: Commit сгенерированных ассетов и сцен**

```powershell
git add Assets/Combat Assets/Player.prefab Assets/Scenes/Game.unity Assets/Inventary/Scenes/SampleScene.unity
git commit -m "feat(combat): generate weapon assets and wire inventory scenes"
```

---

## Порядок применения для пользователя

1. Дождаться компиляции скриптов в Unity (или выполнить `dotnet build`).
2. `Tools/Combat/1. Create Weapon Assets`.
3. `Tools/Combat/2. Wire Player & Scene`.
4. Открыть `Game.unity`, пройти проверку Task 7 Step 3.
5. Открыть `SampleScene.unity`, нажать «Меч», перетащить в слот, проверить
   сохранение/загрузку.
6. При необходимости подстроить в инспекторе иконки, позицию меча и хитбокса,
   `vfxPrefab` магии.
