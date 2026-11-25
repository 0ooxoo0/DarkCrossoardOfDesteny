# Инвентарь (Inventary)

Система предметов и экипировки игрока. Работает в `Assets/Scenes/Game.unity`:
панель открывается клавишей **I**, игра ставится на паузу, курсор всегда свободен
и виден (он нужен для наведения на лут).

Инвентарь ничего не знает про оружие, скиллы и культивацию. Он хранит предметы,
стаки и экипировку и поднимает события. Внешние системы (бой, культивация)
подписываются на эти события сами.

---

## 1. Где что лежит

### Данные
| Файл | Назначение |
|---|---|
| `Script/SO/ItemData.cs` | ScriptableObject предмета + enum `ItemType` / `ItemUseKind`. |
| `Script/SO/EquipmentSlotType.cs` | Слоты экипировки: Weapon, Armor, Helmet, Pants, Accessory. |
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
| `Script/UI/EquipmentSlotUI.cs` | Слот экипировки (Weapon, Armor, Helmet, Pants, Accessory). |
| `Script/UI/InventoryDragController.cs` | Общий drag&drop между сеткой и экипировкой. |
| `Script/UI/InventoryTooltipUI.cs` | Тултип: имя, категория, описание, цена. |
| `Script/UI/InventoryConfirmDialog.cs` | Подтверждение выброса и удаления. |
| `Script/UI/InventoryTheme.cs` | ScriptableObject палитры и шрифта. |
| `Script/UI/LootPanelUI.cs` | Экранная панель содержимого контейнера. |
| `Script/UI/LootRowUI.cs` | Строка списка: иконка, имя, количество, «Забрать». |

### Лут в мире
| Файл | Назначение |
|---|---|
| `Script/World/WorldLootContainer.cs` | Контейнер: список записей, `TryTake`, `TryTakeAll`. |
| `Script/World/WorldLootService.cs` | Создание контейнеров, выброс, слияние близких. |
| `Script/World/LootEntry.cs` | Запись «предмет + количество». |
| `Script/World/LootPickupController.cs` | Подбор: сразу или с подходом персонажа. |
| `Script/World/LootPointerController.cs` | Луч мыши, подсветка, показ панели. |
| `Script/World/OutlineTarget.cs` | Контур (вывернутая копия меша). |
| `Script/World/PointerGuard.cs` | Флаг «клик занят лутом, не атакой». |
| `Script/World/LootTakeResult.cs` | Результат попытки забрать. |

### Визуальная экипировка
| Файл | Назначение |
|---|---|
| `Script/Character/CharacterEquipmentVisuals.cs` | Включает/выключает готовую одежду персонажа. |
| `Script/Character/PlayerEquipmentBridge.cs` | Подписчик на события `Inventory`, применяет визуалы. |
| `Editor/ItemDataEditor.cs` | Выпадающий список визуалов в инспекторе `ItemData`. |

### Редактор
| Файл | Назначение |
|---|---|
| `Assets/Editor/InventorySetup.cs` | Миграция ассетов, предметы, префабы, сцена, лут. |

### Ассеты, которые создаёт сетап
* `Assets/Inventary/TestData/InventoryTheme.asset` — палитра.
* `Assets/Inventary/TestData/*.asset` — 5 тестовых предметов.
* `Assets/Inventary/Prefabs/InventorySlot.prefab` — слот сетки.
* `Assets/Inventary/Prefabs/EquipmentSlot.prefab` — слот экипировки.
* `Assets/Inventary/Prefabs/LootRow.prefab` — строка списка лута.
* `Assets/Inventary/Prefabs/WorldLootContainer.prefab` — контейнер с `ItemEffect`.
* `Assets/Inventary/Materials/OutlineMaterial.mat` — материал контура.
* `Assets/Inventary/Shaders/Outline.shader` — шейдер контура.

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
| Drag из сетки в слот экипировки | Надеть предмет в подходящий слот; надетое ранее вернётся в сетку. |
| Drag из слота экипировки в сетку | Снять предмет (в свободный слот или обменять с подходящим). |
| Двойной клик по слоту экипировки | Снять предмет в первый свободный слот. |
| Наведение на контейнер лута | Контур + панель содержимого. |
| **Забрать** в панели лута | Взять строку; издалека персонаж сначала подойдёт. |
| **Забрать всё** | Взять сколько влезает; пустой контейнер исчезает. |
| **WASD** во время подхода | Отменить подход. |

Кнопки:
* **Сортировать** — по типу, затем по имени; пустые слоты в конец.
* **Выбросить** — кладёт выбранный стек в мир рядом с игроком.
* **Удалить** — стирает выбранный стек насовсем, не создавая лут.
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
   * `equipSlot` — слот для брони (Armor / Helmet / Pants / Accessory);
   * `equipVisualId` — какой объект одежды включить (выпадающий список в инспекторе);
   * `maxStackSize` — для оружия и брони принудительно 1;
   * `sellPrice` — цена (0 = нельзя продать).
3. Положите ссылку в `InventorySaveSystem.allItems`, иначе после загрузки
   сохранения предмет не восстановится.

Для стартового набора используйте поле `Inventory → Starting Items`.

---

## 6. Тестовые данные

Сетап создаёт пять предметов в `Assets/Inventary/TestData/`:

| Ассет | Тип · слот | Визуал | Иконка |
|---|---|---|---|
| `Weapon_BarbarianSword` — Меч варвара | Weapon · Weapon | — (модель через `weaponData = Sword`) | `barbarian…_4` |
| `Armor_Leather` — Кожаная броня | Armor · Armor | `M_Swordsman_Top` | `armor_icon.png` |
| `Helmet_Mage` — Шапка мага | Armor · Helmet | `M_Mage_Headwear` | `barbarian…_0` |
| `Pants_Leather` — Кожаные штаны | Armor · Pants | `M_Swordsman_Bottom` | `barbarian…_2` |
| `Accessory_Ring` — Кольцо странника | Armor · Accessory | `M_SlingBag` | `one-ring.jpg` |

Все пять лежат по одной штуке в стартовом контейнере `[Loot] Starter` в трёх
метрах перед игроком. Стартовый инвентарь пуст: сначала подойдите и заберите лут.

`BootsStr2.png` не используется: отдельной модели обуви в проекте нет, слот
`Boots` заменён на `Pants`.

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

## 8. Лут в мире

* `WorldLootContainer` лежит на земле как один `ItemEffect` (сфера + партиклы),
  хранит список `LootEntry` и сам уничтожается, когда опустеет.
* `WorldLootService` — единственное место создания контейнеров. Выброс появляется
  в 1.5 м перед игроком, прижимается к земле и навмешу, а выбросы в радиусе 2.5 м
  сливаются в один контейнер.
* Наведение мышью (`LootPointerController`) показывает контур и панель
  `LootPanelUI` со списком: иконка, имя, количество, кнопка «Забрать»;
  снизу «Забрать всё».
* Подбор (`LootPickupController`): в пределах 2.5 м предмет забирается сразу,
  иначе персонаж идёт сам. Подход отменяют WASD, исчезновение контейнера,
  таймаут 8 с и отсутствие прогресса 2 с; атака приостанавливает подход до её
  окончания. По прибытии скорость и `Speed` сбрасываются.
* Если места нет, берётся столько, сколько влезает; остаток остаётся в контейнере.
  При нулевом свободном месте панель пишет «В инвентаре нет места».
* Клик по контейнеру и по панели не запускает атаку: `PointerGuard` проверяется
  в `PlayerInteraction.IsAttackBlocked()`.

### Таблицы лута

* `LootTable` — ScriptableObject со списком `LootRoll` (предмет, минимум/максимум, шанс,
  гарантированный выпал). Ассеты лежат в `Assets/Inventary/Data/Loot`.
* `LootRoll` умеет бросать количество и шанс, `LootDrop` — уже вычисленный результат.
* `HarvestableObject` получает `lootTable` в инспекторе: при смерти он отдаёт `LootTable`
  в `WorldLootService` и предметы падают контейнером. Если поле пустое — работает прежний
  `DropManager`, поэтому старые деревья и камни ничего не теряют.
* Таблицы создаёт `Tools → Cultivation → 4. Setup World Content (Game)`:
  `Loot_Tree`, `Loot_Rock`, `Loot_Plant`, `Loot_Enemy`, `Loot_Animal`.

Контейнеры не сохраняются: после перезагрузки сцены остаётся только то, что
описано в сцене (стартовый `[Loot] Starter`).

---

## 9. Визуальная экипировка

`CharacterEquipmentVisuals` висит на `BasicHero_M` и хранит список
`{ слот, visualId, объект }`. Сетап раскладывает существующую одежду по папкам
`BasicHero_M/EquipmentVisuals/{Armor,Helmet,Pants,Accessory}` и выключает её.
`PlayerEquipmentBridge` на `Player` подписан на `OnItemEquipped`/`OnItemUnequipped`
и включает ровно ту вещь, чей `visualId` указан в `ItemData.equipVisualId`.

| Слот | Доступные `visualId` |
|---|---|
| Armor | `M_Swordsman_Top`, `M_Archer_Top`, `M_Mage_Top`, `M_Fighter_Top`, `M_Rogue_Top`, `M_Warlock_Top`, `M_Warlock_TopCape` |
| Helmet | `M_Mage_Headwear`, `M_Warlock_Hood` |
| Pants | `M_Swordsman_Bottom`, `M_Archer_Bottom`, `M_Mage_Bottom`, `M_Fighter_Bottom`, `M_Rogue_Bottom`, `M_Warlock_Bottom` |
| Accessory | `M_SlingBag`, `M_ArrowQuiver`, `M_Archer_Cape`, `M_Warlock_Cape` |

Список в инспекторе `ItemData` подтягивается из сцены (`ItemDataEditor`), так что
ручной ввод имени не нужен. Пустое значение — предмет не меняет внешность.
Второй скелет не создаётся, скиннинг остаётся от исходной модели.

---

## 10. Выброс и удаление

* **Выбросить** — создаёт контейнер (или дополняет ближайший) и только после
  успеха убирает предмет из инвентаря. Если контейнер создать не удалось,
  предмет остаётся, а в подвале панели появляется сообщение.
* **Удалить** — безвозвратно убирает предмет, лут в мире не появляется.
* Обе кнопки спрашивают подтверждение и подписывают кнопку подтверждения
  соответствующим словом.
* Снятое оружие возвращает кулаки: у слота Weapon в инспекторе задан
  `unarmedWeapon = Fists.asset`.

---

## 11. Курсор

В геймплее курсор всегда свободный и видимый: без него не навести мышь на лут.
`Pause_menu` больше не запирает и не прячет курсор ни после Esc, ни после Alt.
Инвентарь на время открытия сам освобождает курсор и возвращает прежнее
состояние при закрытии.

---

## 12. Меню сетапа

* **Tools → Inventory → Setup Test Data** — полная сборка: миграция, 5 предметов,
  префабы, панель, одежда персонажа, лут и связи на игроке. Идемпотентно:
  повторный запуск не плодит объекты и не теряет ссылки.
* **Tools → Inventory → Rebuild Game Panel** — только панель инвентаря и лут,
  без пересоздания ассетов предметов.
* **Tools → Inventory → Migrate Item Data** — только миграция `ItemData`.

После сетапа сцена `Game.unity` сохраняется автоматически.

---

## 13. Ограничения и что дальше

* Броня, шлем, штаны и аксессуар работают: надеваются, меняют одежду и
  сохраняются. Отдельной модели обуви нет — слота `Boots` больше нет.
* `useKind = Pill` с `healAmount > 0` лечит через `PlayerHealth` (мост `CultivationItemBridge`);
  пилюли с опытом, Прозрением и Ци работают так же — эффект выбирается по `useKind` и полям предмета.
* `ItemData.healAmount` — единственное «медицинское» поле предмета. Инвентарь по-прежнему ничего
  не знает о здоровье: лечение делает подписчик на `OnItemUseRequested`.
* Качество пилюли — это отдельные ассеты (`Pill_QiReturn_Normal`, `_Veins`, `_Cloud`): инвентарь
  хранит их как обычные стеки, алхимия выбирает вариант сама.
* Экипировка сохраняется в JSON (`equippedItems`), но `InventorySaveSystem`
  восстанавливает только те предметы, которые есть в его списке `allItems`.
* `[PLACEHOLDER]` намеренно бросается в глаза — это заглушка, а не баг.
* Legacy-сцена `Assets/Inventary/Scenes/SampleScene.unity` использует старый
  префаб `Item.prefab` и остаётся без нового drag&drop. Для проверки используйте
  `Game.unity`.
* Если в `Game.unity` уже висит `UiManager`, Esc закроет и его панель тоже:
  обе системы слушают Escape независимо.
* Луч наведения не фильтрует слои (`rayMask = ~0`): если между камерой и
  контейнером окажется коллайдер игрока, панель не появится. При необходимости
  задайте `rayMask` в инспекторе `LootPointerController`.
