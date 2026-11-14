# Инвентарь (Inventary)

Система предметов и экипировки игрока. Работает в `Assets/Scenes/Game.unity`:
панель открывается клавишей **I**, игра ставится на паузу, курсор освобождается.

Инвентарь ничего не знает про оружие, скиллы и культивацию. Он хранит предметы,
стаки и экипировку и поднимает события. Внешние системы (бой, культивация)
подписываются на эти события сами.

---

## 1. Где что лежит

### Данные
| Файл | Назначение |
|---|---|
| `Script/SO/ItemData.cs` | ScriptableObject предмета + enum `ItemType` / `ItemUseKind`. |
| `Script/SO/EquipmentSlotType.cs` | Слоты экипировки: Weapon, Armor, Helmet, Boots, Accessory. |
| `Script/SO/ItemTypeInfo.cs` | Русские подписи и цвета для UI (не игровая логика). |
| `Script/InventorySlot.cs` | Пара «предмет + количество», `IsEmpty`, `CanStackWith`. |

### Логика
| Файл | Назначение |
|---|---|
| `Script/Inventory.cs` | Единая точка правды: сетка, стаки, экипировка, события. |
| `Script/Json/InventorySaveSystem.cs` | JSON-сохранение сетки, экипировки и оружия. |

### UI
| Файл | Назначение |
|---|---|
| `Script/UI/InventoryPanelUI.cs` | Панель: открытие по I, пауза, кнопки, выбранный слот. |
| `Script/UI/InventoryUI.cs` | Сетка: создаёт вьюхи слотов из префаба. |
| `Script/UI/InventorySlotUI.cs` | Один слот: иконка, количество, тултип, drag&drop. |
| `Script/UI/EquipmentSlotUI.cs` | Слот экипировки (рабочий — Weapon). |
| `Script/UI/InventoryDragController.cs` | Общий drag&drop между сеткой и экипировкой. |
| `Script/UI/InventoryTooltipUI.cs` | Тултип: имя, категория, описание, цена. |
| `Script/UI/InventoryConfirmDialog.cs` | Подтверждение выброса. |
| `Script/UI/InventoryTheme.cs` | ScriptableObject палитры и шрифта. |

### Редактор
| Файл | Назначение |
|---|---|
| `Assets/Editor/InventorySetup.cs` | Миграция ассетов, тестовые предметы, префабы, сборка панели. |

### Ассеты, которые создаёт сетап
* `Assets/Inventary/TestData/InventoryTheme.asset` — палитра.
* `Assets/Inventary/TestData/*.asset` — 4 тестовых предмета.
* `Assets/Inventary/Prefabs/InventorySlot.prefab` — слот сетки.
* `Assets/Inventary/Prefabs/EquipmentSlot.prefab` — слот экипировки.

---

## 2. Как запустить (один раз)

1. Откройте Unity и дождитесь компиляции.
2. Меню **Tools → Inventory → Setup Test Data**.
   Скрипт сам мигрирует старые `ItemData`, создаст тему, префабы, предметы и
   соберёт `InventoryPanel` в `Game.unity`.
3. Откройте `Assets/Scenes/Game.unity`, нажмите Play, нажмите **I**.

Если панель нужно пересобрать без пересоздания предметов:
**Tools → Inventory → Rebuild Game Panel**.

---

## 3. Управление

| Действие | Результат |
|---|---|
| **I** | Открыть/закрыть панель. |
| **Esc** | Закрыть панель (если открыта). |
| ЛКМ по слоту | Выбрать слот (для кнопок справа внизу). |
| Двойной клик | Применить предмет: оружие/броня надеваются, расходник уходит в `OnItemUseRequested`. |
| Drag из сетки в сетку | Обмен слотами; одинаковые стаки сливаются. |
| Drag из сетки в слот Weapon | Надеть оружие; надетое ранее вернётся в освободившийся слот. |
| Drag из слота Weapon в сетку | Снять оружие (в свободный слот или обменять с подходящим). |
| Двойной клик по слоту экипировки | Снять предмет в первый свободный слот. |

Кнопки:
* **Сортировать** — по типу, затем по имени; пустые слоты в конец.
* **Выбросить** — удаляет выбранный стек после подтверждения.
* **Использовать** — то же, что двойной клик.

Пока панель открыта, игра на паузе, а `PlayerController` и `PlayerInteraction`
отключены (список `Disable While Open` в инспекторе панели), чтобы камера не
крутилась за окном.

---

## 4. API для других систем

```csharp
// Добавление с автоматическим стакингом. Атомарно: если места мало — не добавит ничего.
bool added = inventory.AddItem(item, 5);

// Удаление
inventory.RemoveItem(slotIndex, 2);   // из конкретного слота
int removed = inventory.RemoveItem(item, 3); // из любых слотов, вернёт реально удалённое

// Экипировка
inventory.TryEquip(slotIndex, EquipmentSlotType.Weapon);
inventory.Unequip(EquipmentSlotType.Weapon);
inventory.TryMoveEquippedToSlot(EquipmentSlotType.Weapon, gridSlotIndex);

// Перемещение
inventory.SwapSlots(from, to);

// Применение: оружие/броня наденутся сразу, остальное уйдёт подписчикам
inventory.RequestUse(slotIndex);

// Чтение
InventorySlot slot = inventory.GetSlot(index);
IReadOnlyDictionary<EquipmentSlotType, InventorySlot> equipped = inventory.Equipped;
```

События (подписываться в `OnEnable`, отписываться в `OnDisable`):

```csharp
inventory.OnInventoryChanged   += inv => { /* любое изменение */ };
inventory.OnItemAdded          += (item, count) => { };
inventory.OnItemRemoved        += (item, count) => { };
inventory.OnItemEquipped       += (item, slotType) => { };
inventory.OnItemUnequipped     += (item, slotType) => { };
inventory.OnItemUseRequested   += (item, inv, slotIndex) => { /* применить эффект */ };
```

`OnItemUseRequested` — единственная точка, через которую расходники попадают во
внешние системы. Инвентарь сам ничего не лечит и не изучает.

---

## 5. Как добавить предмет

1. **Create → Inventory → Item**.
2. Заполните:
   * `itemId` — уникальный, латиницей (`herb_mortal`), используется сохранением;
   * `itemName` — имя для игрока;
   * `description` — текст тултипа;
   * `icon` — спрайт с прозрачным фоном (если пусто — слот покажет `[PLACEHOLDER]`);
   * `type` — Weapon / Armor / Consumable / Quest / Misc;
   * `useKind` — способ применения (только для расходников и материалов);
   * `maxStackSize` — для оружия и брони принудительно 1;
   * `sellPrice` — цена (0 = нельзя продать).
3. Положите ссылку в `InventorySaveSystem.allItems`, иначе после загрузки
   сохранения предмет не восстановится.

Для стартового набора используйте поле `Inventory → Starting Items`.

---

## 6. Тестовые данные

Сетап создаёт четыре предмета в `Assets/Inventary/TestData/`:

| Ассет | Тип | Стак | Цена |
|---|---|---|---|
| `Sword_Ash` — Пепельный клинок | Weapon | 1 | 12 |
| `Sword_Ember` — Клинок алой зари | Weapon | 1 | 18 |
| `Potion_Healing` — Зелье лечения | Consumable · Potion | 20 | 4 |
| `Material_IronOre` — Железная руда | Misc | 50 | 1 |

Иконки берутся из `Assets/Inventary/Image/InventoryItems/Items1.png`, если там
есть спрайты; иначе слот рисует цветной квадрат с подписью `[PLACEHOLDER]`.

У тестовых мечей **не назначен `weaponData`**, поэтому в руку модель не берётся:
надевание проверяется по слоту экипировки. Чтобы меч появился в руке, назначьте
`weaponData` (ассеты в `Assets/Combat/Weapons/`).

---

## 7. Миграция старых ItemData

Старые категории (`Potion`, `Pill`, `Scroll`, `BreakthroughMaterial`, `DaoSeed`)
переносятся так:

| Было | Стало `type` | Стало `useKind` |
|---|---|---|
| Weapon | Weapon | None |
| Potion | Consumable | Potion |
| Quest | Quest | None |
| Pill | Consumable | Pill |
| Scroll | Consumable | Scroll |
| BreakthroughMaterial | Misc | BreakthroughMaterial |
| DaoSeed | Consumable | DaoSeed |

Миграция идемпотентна: у ассетов есть скрытое поле `dataVersion`, повторный
запуск ничего не портит. Меню: **Tools → Inventory → Migrate Item Data**.

`CultivationItemBridge` теперь выбирает эффект по `useKind`, а не по `ItemType`.

---

## 8. Ограничения и что дальше

* Реально работает только слот **Weapon**. Armor/Helmet/Boots/Accessory
  отрисовываются и принимают дроп, но брони в проекте пока нет.
* `useKind = Potion` пишет в лог: эффекта нет, пока нет системы здоровья.
* Экипировка сохраняется в JSON (`equippedItems`), но `InventorySaveSystem`
  восстанавливает только те предметы, которые есть в его списке `allItems`.
* `[PLACEHOLDER]` намеренно бросается в глаза — это заглушка, а не баг.
* Legacy-сцена `Assets/Inventary/Scenes/SampleScene.unity` использует старый
  префаб `Item.prefab` и остаётся без нового drag&drop. Для проверки используйте
  `Game.unity`.
* Если в `Game.unity` уже висит `UiManager`, Esc закроет и его панель тоже:
  обе системы слушают Escape независимо.
