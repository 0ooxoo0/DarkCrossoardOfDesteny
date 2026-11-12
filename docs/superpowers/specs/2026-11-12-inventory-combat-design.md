# Дизайн: инвентарь, экипировка, книга умений и боевые способности

Дата: 2026-11-12
Статус: на утверждении
Проект: `D:\.Programm\UnitySave\DarkCrossoardOfDesteny`
Предыдущий этап: `docs/superpowers/specs/2026-11-09-weapon-ability-system-design.md`

## 1. Зачем это нужно

Предыдущий этап дал игроку оружие, `AnimatorOverrideController`, хитбоксы,
`AbilitySystem` и хотбар. Но пользоваться этим как игрой ещё нельзя:

- инвентарь существует только в `SampleScene` и не связан с боевой сценой;
- слот оружия в `Game.unity` — display-only, из него нельзя экипировать;
- клик по предмету ничего не делает, тултипа нет;
- у оружия по одной способности, Block отсутствует;
- магия не назначается в слоты и не имеет меню;
- смена оружия через гардероб не синхронизирует слот оружия и не сохраняет
  кулдауны;
- нет 2–3 визуально разных тестовых мечей.

Цель этапа — довести эту систему до играбельного состояния в `Game.unity`,
не ломая движение, поворот, существующий урон по `DestructibleObject`,
гардероб и `SampleScene` как песочницу.

## 2. Цели

1. Инвентарь на `ScriptableObject` (`ItemData`, `InventorySlot`, `Inventory`)
   с `AddItem`, `RemoveItem`, `EquipWeaponFromSlot`, `UnequipWeapon`.
2. Сетка инвентаря 5×4, drag-and-drop, клик-эквип оружия, тултип с названием,
   иконкой и описанием.
3. Отдельный слот оружия: принимает предмет из сетки, вызывает
   `WeaponManager.EquipWeapon`, показывает текущее оружие и синхронизируется
   со сменой через гардероб.
4. Три визуально разных тестовых меча: один prefab-меч и три варианта
   материала (обычный, багровый, лазурный). Каждый — отдельный `WeaponData`
   и `ItemData`, чтобы его можно было добавить, подобрать и экипировать.
5. Два оружия: Fists (по умолчанию) и Sword. Анимации:
   - кулаки: `FIGHTING SWAY` idle, `JAB` attack;
   - меч: `idle`, `run02_front`, `attack_01`.
   Hybrid C: готовый `AnimatorOverrideController`-ассет, иначе runtime-сборка.
6. Минимум 3 способности на оружие, включая Block. `AbilityData` с
   `abilityId`, `displayName`, `icon`, `cooldown`, `type`
   (Melee/Magic/Block), `castAnimation`, `vfxPrefab`, `requiredWeapon`.
7. `AbilitySystem`: слоты 1–5, активация цифрами, кулдауны, проверка
   `requiredWeapon`. Хотбар 5 слотов с радиальным кулдауном, создаётся в
   рантайме под существующим Canvas; prefab слота — отдельный ассет.
8. Смена оружия вызывает `AbilitySystem.SetAbilities`, обновляет хотбар;
   кулдауны сохраняются по `abilityId`.
9. Магия: отдельные глобальные способности, назначаемые в меню скиллов.
   Минимум одна магическая анимация и VFX-плейсхолдер. Урон через
   `OverlapSphere` + временный trigger с тегом `DammageObject`.
   `DestructibleObject` не трогаем.
10. Урон:
    - кулаки — существующий `Cube`-коллайдер;
    - меч — дочерний trigger на клинке у `R_wrist_joint`;
    - `useAnimationEvents = true` → только `AnimationEventRelay`;
    - `false` → только `PlayerController` по `normalizedTime` 0.15–0.55;
    - одновременно два пути не работают.
11. Урон только по `DestructibleObject` с тегом `DammageObject`.
    `HarvestableObject`/`Enemy` вне рамок.
12. `PlayerController.cs` не переписывать; использовать существующие
    `StopMovement`/`ResumeMovement`/`PlayAttackAnimation`/`ResetHitboxState`.
    `DestructibleObject.cs` не трогать. Поворот не ломать.
13. Не добавлять здоровье игрока, лут-дропы, крафт и другие системы без
    явного запроса.

## 3. Не-цели и анти-цели

**Не-цели:**

- здоровье игрока, смерть, урон по игроку;
- лут-дропы, сундуки, крафт, экономика;
- AI врагов и урон по `Enemy`/`HarvestableObject`;
- сохранение назначений магии между запусками (кулдауны живут в сессии);
- отдельные 3D-модели мечей (используются материалы);
- переписывание `PlayerController`, `DestructibleObject`, `Garderob`,
  `UiManager`;
- новые состояния/переходы в `PlayerController.controller`;
- снаряды магии (магия — мгновенная зона);
- hold-to-block.

**Анти-цели (это будет считаться провалом, даже если код компилируется):**

- одновременно работают AnimationEvent и `PlayerController`-путь хитбокса;
- клик/гардероб ломает поворот или движение после закрытия меню;
- `DestructibleObject` перестаёт уничтожаться от кулаков/меча/магии;
- смена оружия через гардероб оставляет в слоте старое оружие или теряет
  предмет из инвентаря;
- открытый инвентарь/книга умений не блокирует способности и движение;
- существующие кнопки гардероба или `SampleScene` перестают работать.

## 4. Ограничения и факты проекта

- Игрок: `Assets/Player.prefab`, Humanoid `BaseMale`, кость правой руки
  `R_wrist_joint` (через `Animator.GetBoneTransform(HumanBodyBones.RightHand)`
  с fallback на дочерний `R_wrist_joint`).
- Базовый контроллер: `Assets/PlayerAnimations/PlayerController.controller`.
  В нём `Locomotion` (BlendTree `idle`/`Sprint` по `Speed`) и `Attack`
  (клип `PunchRight`, триггер `Attack`, выход по `0.92`).
- `WeaponManager` уже:
  - подписан на `OnWeaponChanged` **всех** `Garderob` в сцене;
  - переключает `AnimatorOverrideController`;
  - создаёт меч в правой руке и trigger-хитбокс;
  - передаёт способности в `AbilitySystem`;
  - вызывает `OnWeaponEquipped`.
- `Garderob.cs` уже содержит только два нужных публичных члена:
  `event Action<WeaponData> OnWeaponChanged` и `void EquipWeapon(WeaponData)`.
  В этом этапе `Garderob.cs` не меняется.
- `PlayerController` уже содержит:
  - `StopMovement()`, `ResumeMovement()`, `PlayAttackAnimation()`,
    `ResetHitboxState()`, `IsAttacking`;
  - проверку `WeaponManager.UseAnimationEvents` в `SetWeaponHitbox`;
  - окно хитбокса `hitboxStart = 0.15`, `hitboxEnd = 0.55`.
  Поэтому `PlayerController.cs` в этом этапе **не меняется**.
- `DestructibleObject.OnTriggerEnter` уничтожает объект по тегу
  `DammageObject`. Это единственный обработчик урона; он не меняется.
- `AbilitySystem` уже имеет 5 слотов, Melee/Magic, VFX, временный
  trigger-хитбокс и `requiredWeapon`. Кулдауны сейчас хранятся по индексу
  слота и сбрасываются при смене оружия — это надо заменить.
- `AbilityBarUI` уже строит 5 слотов в рантайме из
  `Assets/Combat/UI/AbilitySlot.prefab` (fallback — кодом).
- `WeaponSlotUI` уже умеет принимать предмет drag-and-drop и работать в
  display-only режиме; ему не хватает клик-эквипа, синхронизации с
  гардеробом, тултипа и публичных методов для `Inventory`.
- `Inventory` и `InventorySlot` уже есть; в `Inventory` есть
  `AddItem`/`RemoveItem`/`MoveItem`/`TakeSlot`/`SetSlot`, но нет
  `EquipWeaponFromSlot`/`UnequipWeapon`/`FindFirstEmptySlot`.
- `InventorySlotUI` уже реализует drag-and-drop, но:
  - клик по оружию только пишет в лог;
  - `SoundManager.Instance` вызывается без null-check (в `Game.unity`
    `SoundManager` отсутствует — hover даст NullReferenceException);
  - `DescriptText` ищется по тегу `DescriptObject` без проверки;
  - `Inventory.InternalAddItem` пишет `slots[i].itemData.description = ...`,
    то есть мутирует общий `ItemData`-ассет — это надо убрать.
- `InventorySaveSystem` уже сохраняет `equippedWeaponId` и
  `weaponSlotItemId`, но в `Game.unity` слот оружия был display-only,
  поэтому `weaponSlotItemId` не сохранялся. Новый функциональный слот
  исправит это.
- `Game.unity`: есть `Canvas`, `PauseMenu`, `GarderobCanvas`, display-only
  `WeaponSlotPanel`, `AbilityBarUI`. Инвентаря и книги умений нет.
  Кнопка «Инвентарь» в `PauseMenu` имеет null-target; чинить её не
  обязательно — инвентарь открывается клавишей `I`.
- `SampleScene`: есть инвентарь, non-display `WeaponSlotUI`, тестовая
  кнопка меча; нет `WeaponManager`/`AbilitySystem`. Остаётся песочницей.
- В проекте включены оба Input System (`activeInputHandler: 2`);
  используем `UnityEngine.InputSystem.Keyboard.current`.
- `UiManager` управляет Escape, `Time.timeScale = 0`, отключает
  `PlayerController`/`PlayerInteraction`. Новые меню не должны с ним
  конфликтовать.
- У импортированных клипов `events: []`; AnimationEvent ставит Editor-скрипт
  через `AnimationUtility.SetAnimationEvents`.
- Модель меча: `Assets/Assets/DarkFantasy/PolygonDarkFantasy/Prefabs/Weapons/SM_Wep_Sword_01.prefab`,
  материал `.../Materials/Alts/PolygonDarkFantasy_Mat_01_A.mat` (URP Lit,
  есть `_BaseColor` и `_Color`).
- Иконки: `Assets/Ideals/Menu/UI/Clean Vector Icons/`
  (`T_10_hand_fire_.png`, `T_10_hammer_.png`, `T_1_shield_.png`,
  `T_5_sword_.png`, `T_2_sword45_.png`, `T_4_sword_saw_.png`,
  `T_4_shield_bezel_.png`, `T_3_magic_fire_.png`, `T_4_frost_.png`).
- VFX: `Assets/Unity Technologies/AllIn1VfxToolkit/Demo & Assets/Demo/Prefabs/Magic Explosive Spell.prefab`,
  `.../Blue Impact.prefab`.

## 5. Рассмотренные подходы и принятые решения

Ниже — развилки, которые реально меняют архитектуру. Пользователь выбрал
варианты 1A, 2C, 3A, 4B, 5A, 6A; Block и меню обсуждены отдельно.

### 5.1 Источник истины для слота оружия

- **A. Слот — источник истины, `WeaponManager` читает из него.** Плюс:
  один UI-слот. Минус: гардероб уже вызывает `WeaponManager` напрямую,
  пришлось бы переписывать существующую связь.
- **B. `WeaponManager.CurrentWeapon` — источник истины, слот — представление.
  Выбрано.** Плюс: гардероб, сохранение и инвентарь сходятся в одну точку;
  слот подписывается на `OnWeaponEquipped`. Минус: при внешней смене оружия
  старый предмет из слота надо вернуть в сетку.

### 5.2 Кулдауны при смене оружия

- A. Сбрасывать все.
- B. Хранить по индексу слота.
- **C. Хранить по `abilityId` в `Dictionary<string, float>`. Выбрано.**
  Плюс: способность сохраняет кулдаун при смене оружия и при переносе
  магии в другой слот. Минус: нужен стабильный `abilityId` у каждого ассета.

### 5.3 UX инвентаря

- A. Только drag-and-drop.
- B. Только клик.
- **C. Drag-and-drop + клик-эквип + тултип. Выбрано.** Плюс: старый
  drag не ломается, появляется быстрый путь и подсказки. Минус: больше
  кода в `InventorySlotUI`.

### 5.4 Три тестовых меча

- A. Три отдельные 3D-модели/prefab.
- **B. Один prefab + три цветовых материала. Выбрано.** Плюс: не нужна
  новая арт-модель, мечи визуально различаются в руке и в инвентаре.
  Минус: геометрия у всех одна.
- C. Один меч + только разные иконки. Отброшено: в мире не видно разницы.

### 5.5 Магия

- A. Магия привязана к оружию.
- **B. Глобальный список магии, назначается в свободные слоты 3–5. Выбрано.**
  Плюс: соответствует требованию «отдельные глобальные способности»;
  книга умений позволяет менять набор. Минус: нужен `SkillBookUI` и
  правила конфликтов с оружием.

### 5.6 Сцены

- A. Полностью собирать и `Game.unity`, и `SampleScene`.
- **B. `Game.unity` — играбельная сцена, `SampleScene` — песочница. Выбрано.**
  Плюс: не дублируем боевые системы в песочнице, не ломаем её.
  Минус: тестовые мечи в `SampleScene` добавляются кнопками, а не через
  полноценный бой.

### 5.7 Block

- A. Hold-to-block (удержание).
- **B. Тайм-абилка ~1.5 с. Выбрано.** Плюс: не нужен отдельный ввод и
  состояние удержания; у игрока нет здоровья, поэтому Block — это
  анимация/позиционирование, а не защита от урона. Минус: нет «щита»,
  который держится, пока кнопка зажата.

### 5.8 Аниматор

- A. Только готовые override-ассеты.
- B. Только runtime-сборка.
- **C. Hybrid: asset-first, runtime fallback. Выбрано на предыдущем этапе.**
  Плюс: удобно в инспекторе, но система не ломается без ассета. Минус:
  временный override для кастов надо аккуратно возвращать.

### 5.9 Меню персонажа

- A. Расширять `UiManager`/`PauseMenu`.
- **B. Отдельный `CharacterMenuController` с клавишами `I`/`K`, паузой и
  временным отключением `UiManager`. Выбрано.** Плюс: не трогаем
  существующее меню паузы и его кнопки. Минус: нужно следить за
  восстановлением `Time.timeScale` и компонентов.

### 5.10 Инициализация UI

- A. Держать панели активными с нулевой альфой.
- **B. Панели неактивны; `InventoryUI`/`WeaponSlotUI`/`SkillBookUI`
  инициализируются лениво при открытии. Выбрано.** Плюс: нет лишних
  `Start`/`Update`, нет случайных raycast. Минус: все публичные методы
  должны вызывать идемпотентный `EnsureInitialized()`.

## 6. Архитектура и файлы

### 6.1 Новые runtime-скрипты

| Файл | Назначение |
|---|---|
| `Assets/Inventary/Script/CharacterMenuController.cs` | `I`/`K`/Escape, пауза, переключение панелей инвентаря и книги умений, `static AnyOpen`. |
| `Assets/Inventary/Script/ItemTooltipUI.cs` | Всплывающий тултип: иконка, название, описание, тип; показ/скрытие, позиционирование у слота. |
| `Assets/Scripts/UI/SkillBookUI.cs` | Книга умений: 5 слотов, список глобальной магии, назначение в слоты 3–5, описание. |
| `Assets/Editor/CombatFullSetup.cs` | Меню `Tools/Combat/0. Full Setup`: ассеты + сцены + новая UI. |

### 6.2 Изменяемые runtime-скрипты

| Файл | Что меняется |
|---|---|
| `Assets/Inventary/Script/SO/ItemData.cs` | `ItemType.Armor`/`Magic`; алиасы `displayName`, `maxStack`, `itemType`; `IsWeapon`. |
| `Assets/Inventary/Script/Inventory.cs` | `EquipWeaponFromSlot`, `UnequipWeapon`, `FindFirstEmptySlot`, `TryReturnItemToInventory`, ссылки на `WeaponManager`/`WeaponSlotUI`, null-check `DescriptText`, убрать мутацию `itemData.description`. |
| `Assets/Inventary/Script/InventoryUI.cs` | Публичная идемпотентная `EnsureInitialized()`, `OnEnable`, `descriptionText`, `tooltip`, подписка/отписка. |
| `Assets/Inventary/Script/InventorySlotUI.cs` | Клик-эквип, тултип, null-check `SoundManager`, безопасный `DescriptText`, защита drag. |
| `Assets/Scripts/UI/WeaponSlotUI.cs` | `EnsureInitialized`, `TryAcceptItem`, `DisplayItem`, `OwnedQuantity`, `ClearOwnedItem`, синхронизация с гардеробом, тултип. |
| `Assets/Scripts/Combat/WeaponData.cs` | `weaponMaterialOverride`; без переименований. |
| `Assets/Scripts/Combat/WeaponManager.cs` | `DefaultWeapon`, material override, fallback `R_wrist_joint`, `PlayCastAnimation(clip, autoResume)`, `StopCastAnimation`. |
| `Assets/Scripts/Combat/AbilityData.cs` | `AbilityType.Block`, `blockDuration`. |
| `Assets/Scripts/Combat/AbilitySystem.cs` | Кулдауны по `abilityId`, глобальный loadout 3–5, Block, `AssignGlobalAbility`/`ClearGlobalSlot`, `defaultGlobalSlots`. |
| `Assets/Scripts/UI/AbilityBarUI.cs` | Подписи 1–5 по индексу слота, обновление при смене loadout. |
| `Assets/Scripts/UI/AbilitySlotUI.cs` | Всегда номер слота, цвет Block, без зависимости от `hotkey`. |
| `Assets/Inventary/Script/Json/InventorySaveSystem.cs` | `weaponSlot.EnsureInitialized()` при восстановлении; функциональный слот вместо display-only. |
| `Assets/Editor/WeaponAssetSetup.cs` | Новые клипы, 3 меча/материала, 3+3+2 способности, иконки/VFX, items. |
| `Assets/Editor/WeaponSceneSetup.cs` | Все новые items/weapons в списки; функциональный слот; новые тестовые кнопки. |
| `Assets/Scenes/Game.unity` | Инвентарь, функциональный слот оружия, книга умений, `CharacterMenuController`, тестовые кнопки. |
| `Assets/Inventary/Scenes/SampleScene.unity` | Кнопки для трёх мечей; остальное без изменений. |
| `Assets/Player.prefab` | `AbilitySystem.globalAbilities`, `defaultGlobalSlots`; ссылки уже есть. |

### 6.3 Файлы, которые не меняются

- `Assets/Scripts/Player/PlayerController.cs` — используем существующие
  публичные методы; новых полей/методов не добавляем.
- `Assets/Scripts/DestructibleObject.cs` — не трогаем.
- `Assets/Garderob.cs` — событие и `EquipWeapon` уже есть; не трогаем.
- `Assets/UiManager.cs` — не трогаем; `CharacterMenuController` временно
  отключает компонент, пока открыто меню персонажа.
- `Assets/Scripts/Player/Weapon.cs`, `AnimationEventRelay.cs`,
  `AttackHitbox.cs`, `WeaponEquipButton.cs` — не трогаем.
- `Assets/Scripts/Player/PlayerController.controller` — не трогаем.

## 7. Данные

### 7.1 ItemData

Существующие поля не переименовываются. Добавляются:

```csharp
public enum ItemType
{
    Weapon,
    Potion,
    Quest,
    Armor,   // задел, в этом этапе не используется
    Magic    // задел, в этом этапе не используется
}

public class ItemData : ScriptableObject
{
    // ... существующие поля ...

    public string displayName => itemName;
    public int maxStack => maxStackSize;
    public ItemType itemType => type;
    public bool IsWeapon => type == ItemType.Weapon && weaponData != null;
}
```

Алиасы — только свойства, в инспекторе дублей нет.

### 7.2 WeaponData

Добавляется одно поле:

```csharp
[Header("Visual")]
public Material weaponMaterialOverride;
```

`null` — использовать материалы prefab-модели. Для трёх мечей один и тот же
`weaponModelPrefab`, но разные материалы. `weaponId` у вариантов уникальный:

- `sword` — обычный;
- `sword_crimson` — багровый;
- `sword_azure` — лазурный.

### 7.3 AbilityData

```csharp
public enum AbilityType
{
    Melee,
    Magic,
    Block
}

public class AbilityData : ScriptableObject
{
    // ... существующие поля ...

    [Header("Block")]
    public float blockDuration = 1.5f;
}
```

`hotkey` остаётся как подсказка для первичного назначения магии; активация
всегда по номеру слота 1–5. `requiredWeapon` работает как раньше.

### 7.4 InventorySlot

Добавляется только удобное свойство:

```csharp
public bool IsEmpty => itemData == null || quantity <= 0;
```

### 7.5 Inventory

Публичные методы:

```csharp
public bool EquipWeaponFromSlot(int slotIndex);
public bool UnequipWeapon();
public int FindFirstEmptySlot(int exceptIndex = -1);
public bool TryReturnItemToInventory(ItemData item, int quantity, int preferredIndex = -1);

public WeaponSlotUI WeaponSlot { get; }
public WeaponManager WeaponManagerRef { get; }
```

Ссылки `weaponManager`/`weaponSlot` — `[SerializeField]`, с fallback
`FindFirstObjectByType` (включая неактивные объекты).

`EquipWeaponFromSlot`:

1. Проверяет индекс, `itemData`, `type == Weapon`, `weaponData != null`.
2. Находит функциональный `WeaponSlotUI` (не display-only).
3. Вызывает `weaponSlot.TryAcceptItem(slotIndex, this)`.
4. Возвращает `true`, если предмет перемещён/экипирован.

`UnequipWeapon`:

1. Если у слота есть `OwnedItem` — ищет свободный слот
   (`FindFirstEmptySlot`); если места нет — возвращает `false` и пишет
   предупреждение; иначе забирает предмет из слота и кладёт в свободный.
2. Если `OwnedItem` нет (оружие пришло из гардероба) — предмет не
   перемещается: гардероб работает без предмета.
3. Экипирует `weaponManager.DefaultWeapon` (кулаки).
4. Сохраняет инвентарь.

`UnequipWeapon` доступен из кнопки «Снять» в панели слота оружия.

`TryReturnItemToInventory` используется для возврата старого предмета при
внешней смене оружия: сначала пробует `preferredIndex`, потом первый
свободный; если места нет — `false`.

### 7.6 Сохранение

`InventorySaveSystem` продолжает хранить `equippedWeaponId` и
`weaponSlotItemId`. Новые item/weapon id добавляются в `allItems`/
`allWeapons` setup-скриптом. Назначения магии между запусками **не
сохраняются** — это не-цель. Кулдауны живут в сессии и сохраняются по
`abilityId` при смене оружия.

## 8. Инвентарь и экипировка

### 8.1 Inventory

- `Awake` инициализирует `width * height` слотов. В `Game.unity` — 5×4
  (20), в `SampleScene` остаётся 5×7; `Inventory` не хардкодит 20.
- `InternalAddItem`: убрать строку
  `slots[i].itemData.description = item.description;` — она мутирует
  общий `ItemData`-ассет.
- При переполнении — null-check `DescriptText` перед записью.
- `OnInventoryChanged` вызывается после `AddItem`/`RemoveItem`/`MoveItem`/
  `SetSlot`/`TakeSlot`.
- `EquipWeaponFromSlot` не делает перемещение само — делегирует
  `WeaponSlotUI.TryAcceptItem`, чтобы drag и клик шли одним путём.

### 8.2 InventoryUI

- Поля: `slotPrefab` (`Item.prefab`), `slotParent` (Grid), `inventory`,
  `descriptionText`, `tooltip`.
- `EnsureInitialized()`:
  - идемпотентна;
  - находит `Inventory`, если не назначен;
  - создаёт слоты из prefab;
  - передаёт каждому `InventorySlotUI` ссылку на тултип и `descriptionText`;
  - подписывается на `OnInventoryChanged`;
  - вызывает `UpdateUI`.
- Вызывается из `OnEnable`, `Awake` (если панель активна) и
  `CharacterMenuController.OpenInventory`.
- `OnDestroy` отписывается.
- Старый `Start`-путь сохраняется как fallback, но не дублирует слоты.

### 8.3 InventorySlotUI

- `OnPointerClick`:
  - левый клик по оружию → `inventory.EquipWeaponFromSlot(slotIndex)`;
  - двойной клик по зелью/квесту → существующий `UseItem()`;
  - двойной клик по оружию не делает ничего второго, потому что слот уже
    опустел.
- `OnPointerEnter`:
  - обновляет `Descript`;
  - пишет в `DescriptText`, только если он найден (без исключения);
  - вызывает `SoundManager.Instance?.PlaySound("Move", 0.3f)`;
  - показывает `ItemTooltipUI` для предмета.
- `OnPointerExit` скрывает тултип и очищает текст.
- `OnBeginDrag`/`OnEndDrag`:
  - скрывают тултип;
  - проверяют `EventSystem.current` и `dragIcon`;
  - сохраняют текущую логику перемещения/trash-area;
  - при провале возвращают слот на место.
- `UseItem` для `ItemType.Weapon` вызывает `EquipWeaponFromSlot`, а не
  пишет в лог.
- Добавляется `SetTooltip(ItemTooltipUI)` и `SetDescriptionText(TMP_Text)`.
- Все обращения к тегам (`DescriptObject`, `TrashArea`) защищены: если тег
  не объявлен в `TagManager`, `CompareTag`/`FindGameObjectWithTag` бросают
  `UnityException`. Setup добавляет теги, а код дополнительно оборачивает
  поиск в `try/catch (UnityException)` и считает, что объекта нет. Это
  сохраняет работоспособность `SampleScene`, даже если теги ещё не созданы.

### 8.4 ItemTooltipUI

- Компонент на панели тултипа (ребёнок `InventoryPanel`).
- Поля: `Image icon`, `TMP_Text nameText`, `TMP_Text descriptionText`,
  `TMP_Text typeText`, `RectTransform root`.
- `Show(ItemData item, RectTransform anchor)`:
  - заполняет иконку/название/описание/тип;
  - активирует root;
  - ставит справа от слота; если не помещается — слева;
  - `root.SetAsLastSibling()`.
- `Show(string title, string description, Sprite icon, RectTransform anchor)` —
  для слота оружия, если `ItemData` не найден.
- `Hide()` — деактивирует root.
- Все графики `raycastTarget = false`.

### 8.5 WeaponSlotUI

Существующие поля и `TryAccept(InventorySlotUI)` сохраняются. Добавляется:

```csharp
public ItemData DisplayItem { get; }
public int OwnedQuantity { get; }
public bool HasOwnedItem { get; }
public bool IsDisplayOnly { get; }
public void EnsureInitialized();
public bool TryAcceptItem(int sourceIndex, Inventory sourceInventory);
public bool ReturnOwnedItemToInventory(Inventory target, int preferredIndex = -1);
public void ClearOwnedItem();
```

`EnsureInitialized()`:

- идемпотентна;
- находит `inventory`/`weaponManager`/`saveSystem`;
- подписывается на `WeaponManager.OnWeaponEquipped` (если менеджер есть);
- подписывается на `Inventory.OnInventoryChanged` для
  `TryFlushPendingReturns()`;
- применяет `CurrentWeapon` (если менеджер есть);
- вызывается из `Awake`, `OnEnable`, `Start` и публичных методов.

`TryAcceptItem(sourceIndex, sourceInventory)`:

1. Проверяет `!displayOnly`, предмет, `weaponData`.
2. Если `ownedItem` уже равен этому предмету — `false`. Если текущее
   оружие совпадает, но `ownedItem == null` (оружие пришло из
   гардероба), предмет можно «забрать» в слот — это не дубль.
3. `TakeSlot(sourceIndex)`.
4. Запоминает старый `ownedItem`/`ownedQuantity`.
5. Кладёт новый предмет в `ownedItem`.
6. Старый предмет возвращает в `sourceIndex` (он только что освободился);
   если не получилось — в первый свободный слот; если места нет —
   добавляет в `pendingReturns` (см. ниже).
7. Вызывает `weaponManager.EquipWeapon(incoming.weaponData)`, если
   `weaponManager != null` (в `SampleScene` его нет — предмет просто
   перемещается и отображается).
8. `ApplyEquippedWeapon(weaponManager != null ? weaponManager.CurrentWeapon : incoming.weaponData)`.
9. Сохраняет инвентарь.

`ApplyEquippedWeapon(weapon)`:

- `displayItem` = `ownedItem`, если `ownedItem.weaponData == weapon`;
  иначе ищет `ItemData` в `weaponItems` по `weaponData`;
- если `ownedItem` есть, но относится к другому оружию (смена через
  гардероб), переносит его в `pendingReturns` и очищает `ownedItem`;
  слот продолжает показывать `weapon`;
- `isEquipped` = `weapon != null`;
- `currentLabel` = имя предмета или `weapon.displayName`;
- `RefreshVisual`.

`pendingReturns` — отдельная очередь `List<PendingReturn>` (item +
quantity), чтобы не путать «предмет в слоте» с «предметом, который надо
вернуть». `OwnedItem` всегда означает только предмет, который сейчас
относится к экипированному оружию. `TryFlushPendingReturns()` вызывается
из `ApplyEquippedWeapon`, `OnEnable`, `TryAcceptItem` и по
`Inventory.OnInventoryChanged`; каждый предмет возвращается в первый
свободный слот, пока есть место. Если места нет — остаётся в очереди;
второй свап не перезаписывает первый. Это гарантирует анти-цель «не
теряет предмет из инвентаря».

`ClearOwnedItem()` очищает `ownedItem`/`ownedQuantity`; вызывается из
`UnequipWeapon`. Если `weaponManager == null`, слот работает как
хранилище предмета и не пытается экипировать.

`UnequipWeapon` доступен из кнопки «Снять»: она вызывает
`Inventory.UnequipWeapon`; кнопка неактивна, если текущее оружие уже
кулаки/`DefaultWeapon`.

Тултип: `WeaponSlotUI` реализует `IPointerEnterHandler`/`IPointerExitHandler`
и показывает `displayItem` (или `weapon.displayName`, если предмета нет).

### 8.6 Потоки

**Клик по оружию в сетке:**

`InventorySlotUI.OnPointerClick` → `Inventory.EquipWeaponFromSlot` →
`WeaponSlotUI.TryAcceptItem` → `TakeSlot` → `WeaponManager.EquipWeapon` →
`AbilitySystem.SetAbilities` → `OnWeaponEquipped` → слот/гардероб/хотбар.

**Drag-and-drop:**

`InventorySlotUI.OnEndDrag` → `WeaponSlotUI.TryAccept(this)` →
тот же `TryAcceptItem`.

**Гардероб:**

`Garderob.EquipWeapon` → `WeaponManager.EquipWeapon` → `OnWeaponEquipped` →
`WeaponSlotUI.ApplyEquippedWeapon` → старый `ownedItem` возвращается в
сетку (если есть место) → слот показывает новое оружие.

**Снятие оружия:**

`Inventory.UnequipWeapon` → `WeaponSlotUI.ClearOwnedItem` → предмет в
свободный слот → `WeaponManager.EquipWeapon(DefaultWeapon)` → кулаки.

## 9. Меню персонажа и книга умений

### 9.1 CharacterMenuController

Компонент на `Canvas` (или на отдельном `CharacterMenu` под Canvas).

Поля:

- `GameObject inventoryPanel`;
- `GameObject skillBookPanel`;
- `PlayerController playerController`;
- `PlayerInteraction playerInteraction`;
- `UiManager uiManager`.

Публичные члены:

```csharp
public static bool AnyOpen { get; private set; }
public void OpenInventory();
public void OpenSkillBook();
public void CloseAll();
```

`Update`:

- `Keyboard.current == null` → выход;
- если `uiManager != null && uiManager.IsOpen` → выход (пауза имеет приоритет);
- `Escape` и `AnyOpen` → `CloseAll()`;
- `I` → `ToggleInventory()`;
- `K` → `ToggleSkillBook()`.

`OpenInventory`:

1. Закрывает книгу умений, если открыта.
2. Активирует `inventoryPanel`.
3. Вызывает `InventoryUI.EnsureInitialized()`.
4. `Time.timeScale = 0`.
5. Отключает `PlayerController`/`PlayerInteraction`.
6. Запоминает и отключает `UiManager` (чтобы Escape не открыл паузу).
7. `AnyOpen = true`.

`OpenSkillBook` — аналогично, вызывает `SkillBookUI.EnsureInitialized()`.

`CloseAll`:

1. Деактивирует панели.
2. `Time.timeScale = 1`.
3. Включает `PlayerController`/`PlayerInteraction`.
4. Включает `UiManager`.
5. `AnyOpen = false`.

`OnDisable`/`OnDestroy` вызывают восстановление, чтобы не оставить
`Time.timeScale = 0`.

Кнопка «Инвентарь» в существующем `PauseMenu` не чинится: инвентарь
открывается клавишей `I`. Это осознанно, чтобы не менять существующее
меню паузы. Если пользователь захочет — добавим отдельный
`OpenInventoryFromPause` позже.

### 9.2 SkillBookUI

Компонент на `SkillBookPanel` (создаётся setup-скриптом, стартует
неактивным).

Поля:

- `AbilitySystem abilitySystem`;
- `CharacterMenuController menuController`;
- `RectTransform magicListParent`;
- `Button[] slotButtons = new Button[5]`;
- `Image[] slotIcons = new Image[5]`;
- `TMP_Text[] slotLabels = new TMP_Text[5]`;
- `TMP_Text descriptionText`;
- `TMP_Text hintText`.

`EnsureInitialized()`:

- идемпотентна;
- находит `AbilitySystem`;
- подписывается на `OnAbilitiesChanged`;
- строит 5 кнопок слотов, если они не назначены;
- строит список магии из `abilitySystem.GlobalAbilities`;
- вызывает `Refresh`.

`Refresh`:

- для каждого слота 0–4:
  - `ability = abilitySystem.GetAbility(i)`;
  - иконка/название;
  - подпись `1`–`5`;
  - цвет: Melee — красноватый, Magic — синий, Block — серый,
    пусто — тёмный;
  - слоты 0–2, занятые способностями оружия, — read-only;
- список магии: имя, иконка, кулдаун, `requiredWeapon`;
- выбранная магия подсвечивается.

Поведение:

- клик по магии → выбрать;
- клик по слоту 3–5:
  - если выбрана магия — `AssignGlobalAbility(i, selected)`;
  - если магия не выбрана и слот занят магией — `ClearGlobalSlot(i)`;
  - если слот занят способностью оружия — показать hint
    «Слот занят способностью оружия»;
- клик по слоту 1–3 с оружейной способностью — только показать описание.

`AssignGlobalAbility`:

- разрешён только для индексов 2–4;
- нельзя занять слот, который сейчас занят способностью оружия;
- одна и та же магия не может стоять в двух слотах;
- при успехе — `RebuildSlots()` и `OnAbilitiesChanged`.

`ClearGlobalSlot` — только для магии в слотах 3–5.

Визуально книга умений — отдельная панель по `K`: заголовок «Книга
умений», слева список магии, справа 5 слотов, снизу описание и подсказка.
Панель строится в рантайме, если дочерние элементы не назначены; setup
создаёт только корень и назначает ссылки.

## 10. AbilitySystem

### 10.1 Слоты

- 5 слотов, индексы 0–4.
- Способности оружия занимают первые `min(3, weaponAbilities.Count)`
  слотов (1–3).
- Глобальная магия назначается только в свободные слоты 3–5
  (индексы 2–4).
- Если у оружия меньше 3 способностей, освободившийся слот 3 тоже можно
  занять магией.
- Если при смене оружия магия оказывается в занятом слоте, она
  переносится в первый свободный слот 3–5; если свободных нет —
  снимается с активных слотов (остаётся в списке известной магии, чтобы
  игрок назначил её заново).

Поля:

```csharp
[SerializeField] private List<AbilityData> globalAbilities = new List<AbilityData>();
[SerializeField] private AbilityData[] defaultGlobalSlots = new AbilityData[5];

private readonly AbilityData[] abilities = new AbilityData[SlotCount];
private readonly Dictionary<string, float> cooldownEndById = new Dictionary<string, float>();
private readonly Dictionary<int, AbilityData> globalLoadout = new Dictionary<int, AbilityData>();
private readonly List<AbilityData> weaponAbilities = new List<AbilityData>();
```

Публичные методы:

```csharp
public IReadOnlyList<AbilityData> GlobalAbilities { get; }
public AbilityData GetGlobalAbility(int slot);
public bool AssignGlobalAbility(int slot, AbilityData ability);
public void ClearGlobalSlot(int slot);
public bool IsWeaponSlot(int slot);
```

`Awake`: если `globalLoadout` пуст, заполняет его из
`defaultGlobalSlots[2..4]`, затем `RebuildSlots()`.

### 10.2 Кулдауны по abilityId

- Ключ: `abilityId`, если он не пуст; иначе `ability.name`.
- `GetCooldownRemaining(index)` берёт способность слота и смотрит
  `cooldownEndById`.
- `GetCooldownFraction(index)` делит остаток на `ability.cooldown`.
- `TryActivate` записывает `cooldownEndById[key] = Time.time + ability.cooldown`.
- При неудачном старте melee (`PlayAttackAnimation` вернул `false`)
  кулдаун откатывается (`Remove(key)`).
- `SetAbilities`/`RebuildSlots` **не сбрасывают** словарь.
- Раз в секунду можно чистить просроченные записи (не обязательно).

### 10.3 Block

- `ExecuteBlock(index, ability)`:
  1. `weaponManager.SuppressHitbox = true`.
  2. `playerController.StopMovement()`.
  3. Если есть `castAnimation` — `weaponManager.PlayCastAnimation(clip, autoResume: false)`.
  4. Ждёт `max(ability.blockDuration, clip.length / 1.25f)`.
  5. `weaponManager.SuppressHitbox = false`.
  6. `playerController.ResumeMovement()`.
- Если `PlayAttackAnimation` не стартовал — откатить кулдаун.
- Block не наносит урон и не защищает от урона: у игрока нет здоровья.
  Это анимация + остановка движения + подавление хитбокса.
- `CancelCurrentAbility` останавливает корутин, вызывает
  `weaponManager.StopCastAnimation()`, снимает `SuppressHitbox`,
  возвращает движение.

### 10.4 Magic

- `ExecuteMagic` остаётся в целом как сейчас:
  - `SuppressHitbox = true`;
  - `StopMovement()`;
  - `PlayCastAnimation` (autoResume по умолчанию);
  - `WaitForSeconds(vfxDelay)`;
  - `SpawnVfx`;
  - `CreateMagicHitbox`;
  - ожидание конца каста;
  - `SuppressHitbox = false`, `ResumeMovement()`.
- `CreateMagicHitbox` создаёт временный `SphereCollider` trigger +
  kinematic `Rigidbody` + тег `DammageObject`; `OverlapSphere` — только
  диагностика. Уничтожение делает `DestructibleObject`.
- Глобальные магии: `MagicBurst` и `FrostNova` (см. раздел 15).

### 10.5 Ввод

- `Update` проверяет `Time.timeScale <= 0`, `UiManager.IsOpen`,
  `CharacterMenuController.AnyOpen`.
- Активация цифрами 1–5.
- `AbilitySlotUI.OnPointerClick` вызывает `ActivateAbility(index)`;
  при открытом меню времени не идёт, поэтому клик ничего не сделает.

## 11. WeaponManager

### 11.1 DefaultWeapon

Добавляется:

```csharp
[SerializeField] private WeaponData defaultWeapon;
public WeaponData DefaultWeapon => defaultWeapon != null ? defaultWeapon : startingWeapon;
```

Setup назначает `Fists.asset` и в `startingWeapon`, и в `defaultWeapon`.
`UnequipWeapon` использует `DefaultWeapon`.

### 11.2 Material override

В `CreateSword` после `Instantiate`:

```csharp
if (weapon.weaponMaterialOverride != null)
{
    foreach (Renderer renderer in weaponModelInstance.GetComponentsInChildren<Renderer>(true))
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
            materials[i] = weapon.weaponMaterialOverride;
        renderer.sharedMaterials = materials;
    }
}
```

Если у renderer нет материалов — пропустить. Это даёт три визуально разных
меча без трёх prefab-ов.

### 11.3 Fallback правой руки

В `Awake`:

1. `animator.GetBoneTransform(HumanBodyBones.RightHand)`.
2. Если null — рекурсивно найти дочерний `R_wrist_joint`.
3. Если null — оставить как есть и залогировать warning при создании меча.

### 11.4 PlayCastAnimation / StopCastAnimation

```csharp
public bool PlayCastAnimation(AnimationClip clip, bool autoResume = true);
public void StopCastAnimation();
```

- `PlayCastAnimation` сохраняет `autoResume` и передаёт его в
  `RestoreAfterCast`.
- `RestoreAfterCast` восстанавливает контроллер оружия всегда; вызывает
  `ResumeMovement()` только если `autoResume == true`.
- `StopCastAnimation`:
  - останавливает корутин;
  - `RestoreWeaponController()`;
  - `DestroyCastOverride()`;
  - `ApplyHitbox(false)`;
  - `ResetHitboxState()`;
  - `SuppressHitbox = false`;
  - `ResumeMovement()`.
- `EquipWeapon` при смене оружия вызывает `abilitySystem.CancelCurrentAbility()`
  (он уже останавливает каст) и дополнительно страхуется
  `StopCastAnimation()`.
- Block передаёт `autoResume: false` и сам возвращает движение через
  `blockDuration`.

## 12. UI хотбара

- `AbilityBarUI` уже строит 5 слотов в рантайме под Canvas; не меняем
  архитектуру.
- `AbilitySlotUI.SetAbility` всегда показывает номер слота
  `slotIndex + 1`; `hotkey` больше не влияет на подпись.
- Цвет плейсхолдера:
  - Melee — красноватый;
  - Magic — синий;
  - Block — серый;
  - пусто — тёмно-серый.
- Радиальный кулдаун уже есть (`Image.Type.Filled`, `Radial360`).
- `AbilityBarUI.RefreshAbilities` вызывается по `OnAbilitiesChanged`.
- Хотбар не блокирует raycast фоном.

## 13. Интеграция с гардеробом

- `Garderob.cs` не меняется.
- `WeaponManager` уже подписан на все `Garderob.OnWeaponChanged`.
- `WeaponSlotUI` подписывается на `WeaponManager.OnWeaponEquipped` и
  синхронизирует `displayItem`/`currentLabel`.
- Гардероб не забирает и не требует предмет. Он меняет только
  `WeaponManager.CurrentWeapon`. Если в сетке лежит `ItemData` нового
  оружия, он остаётся в сетке; слот показывает оружие через `weaponItems`,
  а `ownedItem` остаётся `null`. Поэтому `UnequipWeapon` и сохранение
  поддерживают состояние «экипировано, но предмет не принадлежит слоту».
- При смене через гардероб:
  1. `WeaponManager.EquipWeapon` меняет `CurrentWeapon`;
  2. `AbilitySystem.SetAbilities` обновляет слоты 1–3;
  3. `OnWeaponEquipped` обновляет слот оружия и хотбар;
  4. старый `ownedItem` переносится в `pendingReturns` и возвращается в
     сетку, если есть место;
  5. если места нет — остаётся в `pendingReturns` и возвращается при
     следующем изменении инвентаря.
- Существующие кнопки гардероба не трогаются. Новые кнопки оружия
  (если есть) используют существующий `WeaponEquipButton`.

## 14. Урон и физика

- **Кулаки:** существующий `Cube` с тегом `DammageObject`, trigger
  `BoxCollider`, `Weapon`-компонент. `WeaponManager` включает/выключает
  collider; `Rigidbody` добавляется в рантайме, если его нет.
- **Меч:** дочерний `Hitbox` на модели меча, `BoxCollider` trigger,
  kinematic `Rigidbody`, тег `DammageObject`, `AttackHitbox`.
  Модель крепится к `R_wrist_joint`/RightHand.
- **Магия:** временный `SphereCollider` trigger + kinematic `Rigidbody` +
  тег `DammageObject`, живёт `magicHitboxLifetime`.
- **Источник хитбокса:** `WeaponData.useAnimationEvents`.
  - `true` — `AnimationEventRelay`; `PlayerController` не вызывает
    `SetHitboxActive`.
  - `false` — `PlayerController` по `normalizedTime` 0.15–0.55;
    `AnimationEventRelay` игнорирует события.
  - Одновременно два пути не работают (проверки уже есть в
    `PlayerController` и `AnimationEventRelay`).
- `DestructibleObject` не меняется; урон только по объектам с тегом
  `DammageObject`.

## 15. Анимации, способности и ассеты

### 15.1 Клипы

| Клип | Использование | Loop | AnimationEvent |
|---|---|---|---|
| `FIGHTING SWAY` | Fists idle | да | нет |
| `JAB` | Fists attack / FistsJab | нет | да |
| `LOW_KICK_RIGHT` | FistsLowKick | нет | да |
| `KICK_BLOCK` | FistsBlock | нет | нет |
| `idle` (Sword) | Sword idle | да | нет |
| `run02_front` | Sword run | да | нет |
| `attack_01` | Sword attack / SwordSlash | нет | да |
| `attack_02` | SwordHeavy | нет | да |
| `defense_01` | SwordBlock | нет | нет |
| `CROSS 01` | MagicBurst/FrostNova cast | нет | нет |
| `attack_03` | резерв (не обязателен) | нет | нет |

AnimationEvent `EnableHitbox`/`DisableHitbox` ставится на 15%/55% длины
клипа. Block и магия событий не получают.

### 15.2 Оружие и способности

**Fists** (`Fists.asset`):

- `FistsJab` — Melee, `JAB`, cooldown 0.8, icon `T_10_hand_fire_.png`;
- `FistsLowKick` — Melee, `LOW_KICK_RIGHT`, cooldown 1.5,
  icon `T_10_hammer_.png`;
- `FistsBlock` — Block, `KICK_BLOCK`, cooldown 4, `blockDuration = 1.5`,
  icon `T_1_shield_.png`.

**Sword** (`Sword.asset`, `SwordCrimson.asset`, `SwordAzure.asset`):

- `SwordSlash` — Melee, `attack_01`, cooldown 1.2,
  icon `T_5_sword_.png`;
- `SwordHeavy` — Melee, `attack_02`, cooldown 2.0,
  icon `T_2_sword45_.png`;
- `SwordBlock` — Block, `defense_01`, cooldown 4,
  `blockDuration = 1.5`, icon `T_4_shield_bezel_.png`.

Все три меча используют один `Sword.overrideController` (idle/run/attack),
различаются только `weaponMaterialOverride` и `weaponId`.

**Глобальная магия:**

- `MagicBurst` — Magic, `CROSS 01`, cooldown 3, `requiredWeapon = Any`,
  VFX `Magic Explosive Spell.prefab`, icon `T_3_magic_fire_.png`,
  radius 2, spawn 1.5, lifetime 2, hitbox lifetime 0.2;
- `FrostNova` — Magic, `CROSS 01`, cooldown 6, `requiredWeapon = Any`,
  VFX `Blue Impact.prefab`, icon `T_4_frost_.png`,
  radius 3, spawn 1.2, lifetime 2, hitbox lifetime 0.25.

`CROSS 01` используется для обеих магий как плейсхолдер: это единственная
подходящая не-оружейная анимация. VFX и иконки различают способности.

### 15.3 Предметы

- `FistsItem` — Weapon, `weaponData = Fists`, icon null, stackable false;
- `SwordItem` — Weapon, `sword`, icon `T_5_sword_.png`;
- `SwordCrimsonItem` — Weapon, `sword_crimson`,
  icon `T_2_sword45_.png`;
- `SwordAzureItem` — Weapon, `sword_azure`,
  icon `T_4_sword_saw_.png`.

Все мечи `stackable = false`, `maxStackSize = 1`, `consumable = false`.

### 15.4 Материалы

`CombatFullSetup` создаёт/обновляет:

- `Assets/Combat/Materials/SwordCrimson.mat` — копия
  `PolygonDarkFantasy_Mat_01_A.mat`, `_BaseColor`/`_Color` ≈
  `(0.55, 0.06, 0.06, 1)`;
- `Assets/Combat/Materials/SwordAzure.mat` — копия,
  `_BaseColor`/`_Color` ≈ `(0.08, 0.25, 0.65, 1)`.

Если свойства `_BaseColor`/`_Color` есть — менять; если нет — использовать
первое доступное цветовое свойство. Оригинальный `Sword.asset` оставляет
`weaponMaterialOverride = null`.

## 16. Setup-скрипт

### 16.1 `Assets/Editor/CombatFullSetup.cs`

Меню: `Tools/Combat/0. Full Setup`.

Шаги:

1. Проверить, что не Play mode.
2. Добавить в `ProjectSettings/TagManager.asset` недостающие теги
   `TrashArea` и `DescriptObject` (идемпотентно; существующий
   `DammageObject` не трогать). Без этого `CompareTag`/`FindGameObjectWithTag`
   в `SampleScene` бросают `UnityException`.
3. Вызвать `WeaponAssetSetup.CreateAssets()` (расширенный).
4. Вызвать `WeaponSceneSetup.WireAll()` (расширенный).
5. Открыть `Game.unity` и собрать UI персонажа:
   - `InventorySystem` (активный объект) с `Inventory` и
     `InventorySaveSystem`;
   - `InventoryPanel` (неактивный) под Canvas: фон, заголовок, сетка
     `GridLayoutGroup` 5×4, `Item.prefab` как `slotPrefab`,
     `InventoryUI`, `DragIcon` (из `DragIcon.prefab`, переименовать в
     `DragIcon`), описание, `ItemTooltipUI`, функциональный
     `InventoryWeaponSlotPanel` с `WeaponSlotUI` (не display-only) и
     кнопкой «Снять» (вызывает `Inventory.UnequipWeapon`; неактивна на
     кулаках/`DefaultWeapon`), тестовые кнопки трёх мечей, кнопка
     закрытия;
   - `TrashArea` (объект с тегом `TrashArea`), чтобы существующий
     drag-to-trash не падал;
   - `SkillBookPanel` (неактивный) под Canvas с `SkillBookUI`;
   - `CharacterMenuController` на Canvas; ссылки на панели, игрока,
     `PlayerInteraction`, `UiManager`;
   - `InventorySaveSystem`: `inventory`, функциональный `weaponSlot`,
     `weaponManager`, `allItems` (FistsItem + 3 SwordItem + существующие
     предметы), `allWeapons` (Fists + 3 SwordData), `loadOnStart = true`;
   - `AbilitySystem`: `globalAbilities` = MagicBurst/FrostNova,
     `defaultGlobalSlots[3] = MagicBurst`, `[4] = FrostNova`.
6. Открыть `SampleScene` и добавить кнопки для трёх мечей; сохранить;
   вернуться в `Game.unity`.
7. `AssetDatabase.SaveAssets()`, `AssetDatabase.Refresh()`.

Скрипт идемпотентен: существующие панели/объекты ищутся по имени и
пересоздаются/обновляются, а не дублируются. Если `InventorySaveSystem`
уже есть (например, `WeaponSaveSystem`), он переиспользуется.

### 16.2 Изменения `WeaponAssetSetup`

- Добавить пути клипов `LOW_KICK_RIGHT`, `KICK_BLOCK`, `CROSS 01`,
  `attack_02`, `attack_03`, `defense_01`.
- `ConfigureClip` для melee-клипов с событиями; block/magic — без.
- Создать/обновить 8 `AbilityData`.
- Создать/обновить 3 `WeaponData` мечей и `Fists`.
- Создать/обновить 4 `ItemData`.
- Создать/обновить 2 материала.
- Назначить иконки/VFX.
- Сохранить `Fists.abilities`/`Sword*.abilities` в порядке 1–3.
- `useAnimationEvents = true` для Fists/Sword*.

### 16.3 Изменения `WeaponSceneSetup`

- Все новые `WeaponData`/`ItemData` — в `allWeapons`/`allItems`.
- `weaponItems` display-only HUD и функционального слота — все 4 предмета.
- `InventorySaveSystem.weaponSlot` — функциональный слот, не display-only.
- В `SampleScene` — кнопки для трёх мечей (имена
  `SwordItemButton`, `SwordCrimsonItemButton`, `SwordAzureItemButton`).
- Не удалять существующие кнопки/панели гардероба.

## 17. Сцены и объекты

### 17.1 `Game.unity`

Добавляется:

- `InventorySystem` (active): `Inventory` (5×4), `InventorySaveSystem`.
- `InventoryPanel` (inactive, full-screen overlay):
  - фон/заголовок «Инвентарь»;
  - `Grid` с `GridLayoutGroup` 5×4;
  - `InventoryUI` (`slotPrefab = Item.prefab`);
  - `DragIcon`;
  - `DescriptionText`;
  - `ItemTooltipUI`;
  - `InventoryWeaponSlotPanel` с функциональным `WeaponSlotUI` и кнопкой
    «Снять» (`Inventory.UnequipWeapon`; неактивна на
    кулаках/`DefaultWeapon`);
  - `TestItemsPanel` с кнопками «Добавить: Меч / Багровый / Лазурный»;
  - кнопка «Закрыть».
- `TrashArea` (объект с тегом `TrashArea`) для существующего drag-to-trash.
- `SkillBookPanel` (inactive): `SkillBookUI`.
- `CharacterMenuController` на `Canvas`.

Существующие `PauseMenu`, `GarderobCanvas`, display-only
`WeaponSlotPanel`, `AbilityBarUI` сохраняются.

### 17.2 `SampleScene`

- Остаётся песочницей.
- Добавляются кнопки для трёх мечей.
- `Inventory`/`InventoryUI`/`WeaponSlotUI` не ломаются.
- `WeaponManager`/`AbilitySystem` не добавляются.

## 18. Проверка

### 18.1 Автоматическая

- Компиляция всех `.cs` через Roslyn (как в предыдущих задачах):
  `CSC_EXIT=0`, `ERROR_COUNT=0`.
- Проверка YAML `Game.unity`/`SampleScene.unity`/`Player.prefab` после
  setup: нет битых ссылок, нет дубликатов компонентов.
- Поиск запрещённых изменений:
  - `DestructibleObject.cs` не изменён;
  - `PlayerController.cs` не изменён;
  - `Garderob.cs` не изменён;
  - `UiManager.cs` не изменён.

### 18.2 Ручная в Unity

1. `Tools/Combat/0. Full Setup`, затем Play в `Game.unity`.
2. `I` открывает инвентарь, `K` — книгу умений, Escape закрывает;
   движение и способности на паузе не работают.
3. Кнопки «Добавить меч» кладут мечи в сетку; тултип показывает
   название/иконку/описание.
4. Клик по мечу экипирует; drag в слот оружия тоже работает.
5. Гардероб меняет оружие; слот оружия показывает новое; старый предмет
   вернулся в сетку; хотбар обновился.
6. Гардероб не забирает предмет: если `SwordItem` лежит в сетке, после
   клика по мечу в гардеробе он остаётся в сетке, а слот показывает меч;
   кнопка «Снять» возвращает кулаки и не пытается вернуть несуществующий
   `ownedItem`.
7. Заполнить инвентарь до отказа, сменить оружие через гардероб: старый
   предмет не теряется, лежит в `pendingReturns` и возвращается в сетку
   после освобождения места.
8. Кулдаун способности не сбрасывается при смене оружия и возврате.
9. Кулаки: JAB, LOW_KICK, Block; меч: attack_01, attack_02, Block.
   Block останавливает движение на ~1.5 с и не включает хитбокс.
10. Магия: назначить MagicBurst/FrostNova в слоты 4–5; каст с VFX;
    временный trigger уничтожает `DestructibleObject`.
11. `useAnimationEvents = true/false` — проверить, что работает только
    один путь.
12. `SampleScene`: инвентарь и три меча добавляются, drag/клик не падают;
    hover по предмету не бросает `UnityException`, даже если теги
    `DescriptObject`/`TrashArea` отсутствовали до setup.

## 19. Решения, оставленные строителю

Это можно решить без пользователя, не меняя суть:

- точные локальные позиции/масштаб меча и размер хитбокса;
- точные цвета материалов и UI (в пределах «багровый/лазурный»);
- точная ширина/высота панелей, отступы, шрифты;
- иконки для LOW_KICK/HEAVY (плейсхолдеры из существующего набора);
- положение `TestItemsPanel` (внутри инвентаря, внизу);
- добавлять ли `attack_03` в ассеты (можно оставить резервом);
- чинить ли кнопку «Инвентарь» в `PauseMenu` (не обязательно);
- сохранять ли назначения магии между запусками (не обязательно).

## 20. Ответы на builder check

Перед утверждением дизайн прошёл проверку «сможет ли строитель спланировать
работу без догадок». Шесть вопросов закрыты прямо в документе:

1. **Гардероб забирает предмет или работает без него?**
   Гардероб item-free: меняет только `WeaponManager.CurrentWeapon`, предмет
   в сетке не трогает. Слот показывает оружие через `weaponItems`,
   `ownedItem` остаётся `null`; `UnequipWeapon` и сохранение поддерживают
   состояние «экипировано, но предмет не принадлежит слоту». См. §8.5, §13.
2. **Куда девать старый предмет, если инвентарь полон?**
   В отдельную очередь `pendingReturns`, а не в `ownedItem`. `OwnedItem`
   всегда означает только предмет текущего оружия. Очередь разгружается
   при каждом изменении инвентаря; второй свап не перезаписывает первый.
   См. §8.5.
3. **Что делать с тегами `TrashArea`/`DescriptObject`, которых нет в
   `TagManager`?**
   Setup добавляет оба тега и объект `TrashArea` в `Game.unity`; код
   дополнительно оборачивает `CompareTag`/`FindGameObjectWithTag` в
   `try/catch (UnityException)`. См. §8.3, §16.1, §17.1.
4. **Как `WeaponSlotUI` ведёт себя в `SampleScene` без `WeaponManager`?**
   Null-safe: `EnsureInitialized` не падает, `TryAcceptItem` просто
   перемещает и показывает предмет, экипировка пропускается. См. §8.5.
5. **Как снять оружие, если в слоте нет предмета (гардероб)?**
   Кнопка «Снять» в панели слота вызывает `Inventory.UnequipWeapon`;
   если `OwnedItem == null`, предмет не перемещается, просто экипируются
   кулаки/`DefaultWeapon`. Кнопка неактивна на кулаках. См. §7.5, §8.5,
   §16.1.
6. **Сколько слотов инициализирует `Inventory`?**
   `width * height`, без хардкода 20: в `Game.unity` — 5×4, в
   `SampleScene` остаётся 5×7. См. §8.1.

## 21. Открытые вопросы

На момент написания документа открытых вопросов, меняющих архитектуру,
нет. Вопросы, которые могут появиться после утверждения, будут
добавлены сюда до начала реализации.
