# Культивация, медитация, добыча и алхимия

Дата: 2026-11-17
Статус: на согласовании

## 1. Зачем

Сейчас культивация — это изолированная песочница: медитация даёт опыт и очки Дао,
прорыв бросает кубик, техники открываются по уровню. Но у неё нет цены: прерывание
ничего не стоит, медитировать можно в центре боя, ресурсов в мире нет, крафта нет,
а скиллы живут только в одной панели на `Tab`.

Нужен полный цикл, как в «Пике боевых искусств»:

> убил зверя → выпал ресурс → сварил пилюлю → медитировал → прокачал Дао → открыл скилл

И нужна цена ошибки: медитация вне безопасного места, прерванная ударом или
подошедшим врагом, должна калечить культивацию — от лёгкого срыва до обрыва
с потерей прогресса.

## 2. Цели

- У игрока появляется здоровье, смерть и respawn с потерей опыта культивации.
- Враги умеют агриться и бить; животные мирные и убегают; и те, и другие дают лут.
- Медитация:
  - безопасная зона (`MeditationZone`, алтарь) — без риска и с бонусом ×2;
  - вне зоны — риск: удар или подошедший агрессивный враг прерывает;
  - три стадии последствий: **Срыв Ци → Отклонение Ци → Обрыв культивации**;
  - ручное прерывание в безопасном месте бесплатно, вне его — первый stage;
  - дебафы видны, длятся, истекают и сохраняются между сессиями.
- Из врагов, животных, деревьев, камней и растений выпадают предметы-ресурсы
  в лут-контейнер (`WorldLootContainer`), а не абстрактные префабы.
- Алхимия:
  - у печи — полный набор рецептов и бонус к качеству;
  - в поле — только простые рецепты со штрафом к успеху;
  - провал существует и съедает часть ингредиентов;
  - грейды алхимика от Common до Open Heaven с подрангами;
  - качество пилюль: Normal / Pill Veins / Pill Cloud.
- Пассивные скиллы каждой школы (меч, кулак, тело, огонь, гром) открываются
  по мере прокачки Дао и реалма и реально меняют цифры персонажа.
- UI:
  - слева полоса Дао; клик открывает экран медитации;
  - Esc-меню — единая точка входа во все системы;
  - отдельный список скиллов (техники + пассивки);
  - экран медитации с прогрессом, риском и дебафами.
- VFX из `Assets/Magic`: аура, прорыв, обрыв культивации, отклонение Ци.

## 3. Не-цели

- Полный боевой ИИ по манхве (Utility AI, враги с техниками, формации) — отдельный этап.
- Торговля, секты, очки вклада, аукционы.
- Формации, артефакторика, звёздные шаттлы.
- Сюжет и квесты.
- Мультиплеер.
- Переделка инвентаря, сетки, drag&drop, экипировки.
- Замена модели персонажа, рига и анимаций.

## 4. Ограничения

- `PlayerController.cs` не трогаем. Урон, respawn и подходы — через отдельные
  компоненты и события.
- `DestructibleObject.cs`, `WeaponManager.cs`, `WeaponData.cs` не трогаем.
- Теги и слои в `TagManager` не добавляем: всё через ссылки в инспекторе и
  `LayerMask`-поля.
- Инвентарь не знает о культивации, алхимии и сцене. Связь — через события и
  отдельный мост (`CultivationItemBridge` уже существует).
- Один класс — один файл, папки по системам, PascalCase, `_camelCase` не вводим,
  если в файле уже другой стиль — держим стиль файла.
- Никаких магических чисел и строк: всё в `[SerializeField]` или ScriptableObject.
- XML-комментарии на русском над публичными классами и методами, inline-комментарии
  «почему так».
- Плейсхолдеры помечаются `[PLACEHOLDER]`.
- Правки существующих файлов — только с diff и объяснением.

## 5. Что есть в проекте сейчас

### 5.1 Культивация

- `CultivationBootstrap` (`Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs`)
  на `AfterSceneLoad` создаёт `[Cultivation]`, вешает компоненты на игрока, строит UI
  под существующим `Canvas`, восстанавливает прогресс из `PlayerPrefs`.
- `CultivationConfig` — `Assets/Resources/Cultivation/CultivationConfig.asset`.
  4 реалма × 3 ступени, 5 путей Дао, 15 техник, медитация, прорыв.
- `CultivationSystem`: `ExperienceQi`, `Comprehension`, `AddExperienceQi`,
  `SetExperienceQi`, `TryBreakthrough`, `TotalHpBonus`, `TotalQiBonus`,
  `TotalRegenBonus`, `HasBreakthroughMaterials` (Func).
- `DaoSystem`: уровни/очки/активный путь, `OnTechniqueLearned`, `HasLearnedTechnique`,
  `GetAvailableTechniques`, `IsTechniqueAvailable`.
- `MeditationController`: тик опыта/Дао/Ци/Прозрения, зоны (`MeditationZone`) с
  множителем, `IMeditationInterrupter` → `Interrupt()`. Прерывание сейчас = просто
  выход, без последствий.
- `BreakthroughSequence` + `BreakthroughUI`: 3 с прогресса, шанс, VFX-хук.
- `CultivationUIRoot`: `QiBarUI`, `CultivationUI` (Tab/C), `TechniqueListUI`,
  `MeditationUI`, `BreakthroughUI`, `CultivationQuickBarUI` (1–5).
- `CultivationItemBridge`: уже применяет пилюли (`qiRestore`, `experienceQiGain`,
  `comprehensionGain`), свитки и семена Дао.
- `CultivationStateCache`: снимок в `PlayerPrefs` (реалм, ступень, опыт, Ци,
  активное Дао, уровни/очки, выученные техники).
- Реалмы требуют теги материалов: `herb.mortal`, `herb.spirit`, `ore.jade`,
  `core.beast`, `essence.golden`. `Inventory.HasAllItemsWithTags` и
  `RemoveItemsByTags` уже используются при прорыве.

### 5.2 Добыча и враги

- `HarvestableObject` (`Assets/Scripts/Drop/HarvestableObject.cs`): HP, `TakeDamage`,
  `Damaged`, при смерти — `destroyEffectPrefab`, `DropManager.GetDropItem(...)`,
  `Destroy(gameObject)`.
- `DropManager` (`Assets/Scripts/Drop/DropManager.cs`): таблицы шансов, но спавнит
  **префабы**, а не `ItemData`. С инвентарём не связан.
- `Enemy` в `Game.unity` — сфера с `HarvestableObject` + `Enemy1` (свой HP и
  `HealthBar`). Не двигается, не атакует.
- `EnemyLogovo` — логово с детьми `up`, без скриптов поведения.
- `Tree`, `Rock`, `Plant` — `HarvestableObject`.
- Животные: `Assets/Prefabs/Animals` — `Horse Variant`, `Pig Variant`,
  `Sheep Variant`. В сцене их нет.
- У игрока **нет здоровья** и нет ни одного источника урона по нему.
- `PlayerInteraction` бьёт по `IInteractable` в радиусе, `blockAttackLayers` уже
  учитывает UI и лут.

### 5.3 Инвентарь и лут

- `ItemData` (`Assets/Inventary/Script/SO/ItemData.cs`): `ItemType`, `ItemUseKind`
  (`Potion`, `Pill`, `Scroll`, `DaoSeed`, `BreakthroughMaterial`), `equipSlot`,
  `equipVisualId`, `breakthroughTags`, `qiRestore`, `experienceQiGain`,
  `comprehensionGain`.
- `Inventory` (`Assets/Inventary/Script/Inventory.cs`): `AddItem`, `RemoveItem`,
  `GetFreeSpace`, `HasItemsWithTags`, `HasAllItemsWithTags`,
  `RemoveItemsByTags`, события добавления/удаления/экипировки.
- `WorldLootService` (`Assets/Inventary/Script/World/WorldLootService.cs`):
  `TryDrop(ItemData, int, Transform)`, слияние контейнеров в радиусе, снап к земле
  и NavMesh.
- `WorldLootContainer`: `Add`, `TryTake`, `TryTakeAll`, `OnChanged`, `OnEmptied`.
- `LootPointerController`, `LootPickupController`, `LootPanelUI` — наведение,
  подход, «Забрать»/«Забрать всё».
- `Assets/Editor/InventorySetup.cs` — единственный способ собрать сцену: тестовые
  предметы, слоты, префабы, панель, контур, лут-сервис.

### 5.4 Меню и ввод

- В сцене `Canvas` содержит `UiManager`, `AbilityBarUI`, `InventoryPanel`,
  `LootPanel`, `PauseMenu` (выключен).
- `UiManager` по Esc открывает `uiPanel` = `PauseMenu`, ставит `timeScale = 0`,
  отключает `PlayerController` и `PlayerInteraction`.
- `Pause_menu.cs` **не висит** на объекте сцены: его логика сейчас мертва.
- Внутри `PauseMenu` уже есть кнопки: `Инвентарь`, `Сохранение`, `Навыки`,
  `Карта`, `Настройки`, `Выход`, а также панели «Улучшение навыков»,
  «Настройки», «Настройки звука», «Настройки графики», «Карта», «Гардероб».
- `CultivationUIRoot` перехватывает `Tab`/`C`/`M` и `1–5` через `CultivationInput`
  и блокирует остальной ввод, пока открыта его панель.
- `Altar` (`Assets/Scripts/Cultivation/Runtime/Altar.cs`) — подход + `E`, открывает
  панель культивации, добавляет `MeditationZone`.

### 5.5 VFX

`Assets/Magic` содержит готовые префабы:

- `PersonAura/Holy Aura`, `PersonAura/Evil Aura`, `PersonAura/Dark Magic Orb`;
- `otherAura/Red Area`, `Green Area`, `Purple Area`, `Blue Area + Distort Sphere`;
- `2Dammage/Explosion Galaxy`, `Explosion Bomb`, `Lightning Strike`;
- `Thick Smoke`, `Portal/Green Portal`, `Shield/*`.

Отдельных префабов «прорыв» и «обрыв культивации» нет — собираем из этих.

## 6. Принятые решения

| Вопрос | Решение |
|---|---|
| Где медитировать | Везде; в `MeditationZone` ×2 и нулевой риск, вне зоны риск |
| Цена прерывания | Три стадии: Срыв Ци → Отклонение Ци → Обрыв культивации, стадии копятся |
| Как падает добыча | Контейнером на землю, подбор как сейчас |
| Где крафт | И печь в мире (полный набор + бонус), и походный крафт со штрафом |
| Провал и качество | Провал есть; полная градация алхимика и качество Normal / Pill Veins / Pill Cloud |
| Что такое скиллы | Существующие 15 техник + пассивные скиллы каждой школы |
| Esc-меню | Все системы + заглушки Карты и Журнала |
| Подход к угрозе | «Живой мир»: `PlayerHealth` + `EnemyAI` |
| Смерть игрока | Респавн с потерей опыта |
| Животные | Мирные, убегают, дают мясо/шкуру |

## 7. Архитектура

### 7.1 Здоровье игрока, смерть и респавн

Новые файлы:

- `Assets/Scripts/Combat/DamageInfo.cs` — структура `Amount`, `Source`,
  `HitPoint`, `IsCritical`.
- `Assets/Scripts/Combat/PlayerHealth.cs` — здоровье игрока.
- `Assets/Scripts/Combat/PlayerRespawnService.cs` — смерть и возврат.

`PlayerHealth`:

- `maxHealthBase`, `regenPerSecond`, `regenDelayAfterDamage`,
  `invulnerabilityAfterRespawn` — поля инспектора.
- `MaxHealth = maxHealthBase + Cultivation.TotalHpBonus + PassiveBonus + DebuffMultiplier`.
- `event Action<int, int> OnHealthChanged`, `event Action<DamageInfo> OnDamaged`,
  `event Action OnDied`, `event Action OnRespawned`.
- `TakeDamage(DamageInfo)`: игнорирует неуязвимость и мёртвого; умножает на
  `DebuffController.DamageTakenMultiplier`; при 0 HP — `Die()`.
- `Heal(int)`, `HealPercent(float)`.
- Реген включается через `regenDelayAfterDamage` после последнего урона.
- Не трогает `PlayerController`: только событие смерти и телепорт.

`PlayerRespawnService`:

- `respawnPoint` — `Transform` (алтарь или стартовая точка, назначается сетапом).
- `experienceLossPercent` — по умолчанию 0.25.
- `hpRestorePercent` — по умолчанию 0.5.
- На `OnDied`:
  1. `Cultivation.LoseExperiencePercent(experienceLossPercent)`;
  2. отключить `CharacterController`/`NavMeshAgent`, переместить к точке,
     включить обратно;
  3. `PlayerHealth.RestorePercent(hpRestorePercent)`;
  4. неуязвимость на `invulnerabilityAfterRespawn`;
  5. событие `OnRespawned` для UI и VFX.

`CultivationSystem` получает `LoseExperiencePercent(float)` и событие
`OnExperienceLost(int)`.

### 7.2 Угроза: враги и животные

Новые файлы:

- `Assets/Scripts/Enemy/EnemyAI.cs`
- `Assets/Scripts/Enemy/AnimalAI.cs`

`EnemyAI` (навешивается на врага рядом с `HarvestableObject`):

- Состояния: `Idle`, `Chase`, `Attack`, `Return`, `Dead`.
- `aggroRadius`, `attackRange`, `attackDamage`, `attackCooldown`, `moveSpeed`,
  `leashRadius`, `loseTargetSeconds` — поля инспектора.
- Работает через `NavMeshAgent`, если он есть; иначе двигает `transform`.
- `public bool IsAggroed { get; }` — используется проверкой медитации.
- Урон наносит `PlayerHealth.TakeDamage`.
- Не меняет `PlayerController`, не вызывает его методы.
- Смерть определяет по `HarvestableObject.OnDied`, после чего останавливается.

`AnimalAI`:

- Мирное поведение: блуждание в `wanderRadius` вокруг дома.
- На `HarvestableObject.Damaged` — бежит от источника урона `fleeSpeed` в течение
  `fleeSeconds`, потом возвращается.
- Не атакует.

`HarvestableObject` (правка):

- `[SerializeField] private LootTable lootTable;`
- `public event Action<HarvestableObject, Vector3> OnDied;`
- При смерти:
  - если `lootTable != null` — ролл и `WorldLootService.Instance.TryDrop(...)`
    для каждой строки;
  - иначе — старое поведение через `DropManager` (обратная совместимость);
  - `OnDied` до `Destroy`, чтобы `EnemyAI`/UI успели отреагировать.
- `WorldLootService` получает `public static WorldLootService Instance`.

Спавн животных и врагов — редакторский сетап, не рантайм-спавнер.

### 7.3 Медитация, безопасность и три стадии срыва

Новые файлы в `Assets/Scripts/Cultivation/Runtime/Meditation/`:

- `MeditationRiskConfig.cs` — ScriptableObject с числами (см. 8.1).
- `MeditationSession.cs` — снимок сессии: `StartedAt`, `AccumulatedExperience`,
  `AccumulatedDaoPoints`, `AccumulatedComprehension`, `WasSafe`.
- `CultivationFailureStage.cs` — описание стадии: имя, длительность, штрафы,
  потеря опыта, блок медитации, id VFX.
- `CultivationDebuffController.cs` — активные дебафы, таймеры, множители.
- `MeditationConsequenceService.cs` — применение последствий.

`MeditationController` (правка):

- Хранит `MeditationSession` и накапливает в нём опыт/очки/Прозрение.
- `public bool IsInSafeZone => zones.Count > 0;`
- Каждые `threatScanInterval` (по умолчанию 0.25 с) проверяет сферу
  `threatRadius` вокруг игрока: есть ли `EnemyAI` с `IsAggroed` и линией видимости.
- `StartMeditation()`:
  - нельзя, если `DebuffController.IsMeditationBlocked`;
  - нельзя, если игрок движется (проверка ввода WASD);
  - запоминает `WasSafe`, обнуляет счётчик сессии.
- `Interrupt(MeditationInterruptionReason reason)`:
  - `Manual` — в безопасной зоне бесплатно; вне зоны stage 1;
  - `Damage` — эскалация;
  - `Threat` — эскалация;
  - `Death` — без отдельной стадии, смерть сама по себе наказание.
- `ExitMeditation()` остаётся для «мягкого» выхода без последствий (используется
  смертью и загрузкой).

`CultivationDebuffController`:

- `IReadOnlyList<ActiveCultivationDebuff> Active`.
- `float QiRegenMultiplier`, `DamageDealtMultiplier`, `DamageTakenMultiplier`,
  `MaxHealthMultiplier`.
- `bool IsMeditationBlocked`.
- `int Severity` (0–3) и таймер эскалации.
- События `OnDebuffApplied`, `OnDebuffExpired`, `OnSeverityChanged`.
- Читает `MeditationRiskConfig`, не хранит числа в коде.
- Сохраняется в `PlayerPrefs` через `CultivationStateCache`.

Эскалация:

- `Severity` увеличивается на 1 при `Damage`/`Threat`, максимум 3.
- Ручное прерывание вне зоны поднимает только до 1.
- Если `escalationDecaySeconds` (180 с) не было прерываний — `Severity`
  уменьшается на 1; после `escalationResetSeconds` (600 с) — сбрасывается.

Последствия по стадиям (значения по умолчанию, всё в конфиге):

| Стадия | Длительность | Ци-реген | Урон | Макс. HP | Потеря опыта сессии | Потеря опыта ступени | Блок медитации |
|---|---|---|---|---|---|---|---|
| Срыв Ци | 30 с | −25% | −10% | −10% | 15% | 0% | 5 с |
| Отклонение Ци | 90 с | −60% | −25% | −20% | 35% | 10% | 20 с |
| Обрыв культивации | 240 с | −100% | −40% | −35% | 60% | 25% | 60 с |

Потеря опыта сессии — процент от того, что накоплено за текущую медитацию.
Потеря опыта ступени — процент от `CultivationSystem.ExperienceQi`.

### 7.4 Добыча и таблицы лута

Новые файлы:

- `Assets/Scripts/Loot/LootTable.cs` — ScriptableObject.
- `Assets/Scripts/Loot/LootRoll.cs` — сериализуемая строка таблицы.

`LootRoll`: `ItemData item`, `int min`, `int max`, `float chance`,
`bool guaranteed`.

`LootTable`:

- `List<LootRoll> rolls`, `int guaranteedRolls`, `int bonusRolls`.
- `RollInto(List<LootDrop> output)` — без `Instantiate`, без магии.
- Пустой/null item пропускается с предупреждением.

Дроп с существующих объектов:

| Объект | Ресурсы |
|---|---|
| `Tree` | `Wood_Spirit` 1–3 |
| `Rock` | `Ore_Iron` 1–2; `Ore_Jade` 1 с шансом 10% |
| `Plant` | `Herb_Mortal` 1–2; `Herb_Spirit` 1 с шансом 20% |
| `Enemy` (разбойник) | `Ore_Iron` 1–2 (60%), `Herb_Mortal` 1–2 (40%), `Essence_Golden` 1 (5%) |
| `EnemyLogovo` | пока без дропа, только визуал |
| `Horse/Pig/Sheep` | `Meat_Raw` 1–2; `Pelt` 1 (60%) |

Каждый труп оставляет **один** контейнер: `WorldLootService` уже сливает
контейнеры в радиусе `mergeRadius`.

### 7.5 Алхимия

Новые файлы в `Assets/Scripts/Cultivation/Runtime/Alchemy/`:

- `AlchemyGrade.cs` — грейд и подранг.
- `PillQuality.cs` — `Normal`, `PillVeins`, `PillCloud`.
- `AlchemyRecipe.cs` — ScriptableObject рецепта.
- `AlchemySkill.cs` — грейд и мастерство игрока.
- `AlchemyResult.cs` — результат крафта.
- `AlchemyService.cs` — проверка и сам крафт.
- `AlchemyStation.cs` — печь в мире.
- `AlchemyConfig.cs` — ScriptableObject с числами.

Грейды (`AlchemyGrade`):

- 12 грейдов: Common, Earth, Heaven, Mysterious, Spirit, Saint, Saint King,
  Origin, Origin King, Dao Source, Emperor, Open Heaven.
- Подранги Low/Middle/High у грейдов 1–10; Emperor — Low/Middle/High/Great;
  Open Heaven — один.
- Сравнение по `RankIndex`, а не по двум полям.

`AlchemySkill`:

- `Grade`, `Mastery`, `MasteryForNextRank`.
- `AddMastery(int)`; при переполнении — подранг вверх, затем грейд.
- Событие `OnGradeChanged`, `OnMasteryChanged`.

`AlchemyRecipe`:

- `recipeId`, `displayName`, `description`;
- `output` (`ItemData`) и `outputCount`;
- `ingredients` (`ItemData` + количество);
- `requiredGrade`, `requiredDaoLevel`, `requiredRealmIndex`;
- `baseSuccessChance`, `portableAllowed`, `craftSeconds`;
- `masteryReward`.

`AlchemyService.Craft(AlchemyRecipe, bool atStation, Inventory, out AlchemyResult)`:

1. Проверки: рецепт, ингредиенты, грейд, уровень Дао, реалм, место.
2. Шанс успеха:
   `baseSuccessChance + (playerRank - requiredRank) * chancePerRank
    + comprehension * chancePerComprehension + stationBonus - portablePenalty`.
   Всё клампится в `[minChance, maxChance]` из конфига.
3. Бросок.
4. Успех: списать ингредиенты, выдать `output` в количестве `outputCount`,
   начислить мастерство, определить качество.
5. Провал: списать `ingredientLossOnFail` (доля каждого ингредиента, округление
   вверх, минимум 1), начислить `masteryOnFail`, вернуть причину.
6. Качество: по «запасу» броска выше порога успеха; пороги `veinsMargin` и
   `cloudMargin` в конфиге, плюс бонус за разницу грейдов и место.
7. Качество реализуется **отдельным `ItemData`-вариантом** (`Pill_Qi_Veins` и
   т.п.), чтобы не менять `Inventory` и хранить качество в существующих слотах.

Походный крафт:

- доступен из Esc-меню → «Крафт»;
- видны только рецепты с `portableAllowed`;
- `portablePenalty` к шансу и `portableQualityPenalty` к качеству;
- печь (`AlchemyStation`) даёт `stationBonus` и полный список.

`AlchemyStation`:

- триггер-радиус, подсветка/подсказка «E — алхимия»;
- `E` открывает `AlchemyPanelUI` в режиме печи;
- ссылка на `AlchemyConfig` и список рецептов (или общий каталог рецептов).

### 7.6 Пассивные скиллы школ

Новые файлы:

- `Assets/Scripts/Cultivation/Runtime/Passives/PassiveEffectKind.cs`
- `Assets/Scripts/Cultivation/Runtime/Passives/PassiveSkillService.cs`

`PassiveEffectKind`: `MaxHealthPercent`, `QiRegenPercent`, `MaxQiPercent`,
`SpellPowerPercent`, `MeleeDamagePercent`, `DamageReductionPercent`,
`ComprehensionGainPercent`, `AlchemySuccessPercent`.

`TechniqueData` (правка): `passiveEffect`, `passiveValue`.

Контент (в `CultivationDefaults`), по одному пассиву на школу + второй на
реалме 1:

| Школа | Пассив | Эффект |
|---|---|---|
| Меч | Острота клинка | +10% урона оружием |
| Кулак | Твёрдость кулака | +10% урона без оружия |
| Тело | Закалка тела | +15% макс. HP |
| Огонь | Внутреннее пламя | +10% силы техник |
| Гром | Громовое дыхание | +10% реген Ци |
| Тело (реалм 1) | Несокрушимость | −10% получаемого урона |
| Огонь (реалм 1) | Пепел и пламя | +10% силы техник |
| Меч (реалм 1) | Единство с клинком | +10% урона оружием |
| Кулак (реалм 1) | Железная ладонь | +10% урона без оружия |
| Гром (реалм 1) | Раскат грома | +10% регена Ци |

`PassiveSkillService`:

- Подписан на `DaoSystem.OnTechniqueLearned`, `OnDaoLevelChanged`,
  `OnRealmChanged`, `OnStageChanged`.
- Собирает активные пассивы (выученные + требования выполнены).
- Отдаёт `IReadOnlyList<PassiveModifier>` и событие `OnModifiersChanged`.
- `PlayerHealth`, `QiComponent`, `CultivationSystem`, `AlchemyService` читают
  множители через сервис.
- `AbilitySystem` получает свойство `DamageMultiplier` (одна точка правки,
  только умножение урона). Без этого «усиление заклинаний» не работает.

### 7.7 UI

Новые файлы в `Assets/Scripts/Cultivation/Runtime/UI/`:

- `PlayerVitalsUI.cs` — HP + Ци + реалм/ступень, верхний левый угол.
- `DaoHudBarUI.cs` — левая полоса Дао.
- `MeditationScreenUI.cs` — экран медитации.
- `SkillListUI.cs` — список скиллов.
- `AlchemyPanelUI.cs` — окно крафта.
- `DebuffPanelUI.cs` — активные дебафы (под виталами).

`GameMenuController` (`Assets/Scripts/UI/GameMenuController.cs`) — владелец Esc:

- заменяет Esc-логику `UiManager`, но оставляет `UiManager` для паузы и
  отключения игрока;
- стек панелей: если открыта системная панель — Esc закрывает её; иначе
  открывает/закрывает `PauseMenu`;
- `OpenPanel(GameObject)`, `CloseTopPanel()`, `IsAnyPanelOpen`;
- пауза (`timeScale = 0`) и свободный курсор для меню и модальных панелей;
- экран медитации — исключение: он не ставит паузу, потому что медитация идёт
  в реальном времени и риск должен быть настоящим.

`PauseMenu` (сцена, правки сетапом):

- существующая панель «Кнопки» получает три новые кнопки: **Культивация**,
  **Техники**, **Крафт**; **Карта** и **Журнал** — заглушки с текстом
  «В разработке».
- кнопки ведут в `GameMenuController`, а не в мёртвый `Pause_menu.cs`.
- `Pause_menu.cs` остаётся нетронутым и не навешивается.

`DaoHudBarUI`:

- вертикальная полоса у левого края: цвет активного Дао, название школы,
  уровень, заполнение до следующего уровня;
- под ней — 5 маленьких узлов Дао (открыт/закрыт);
- клик по полосе открывает `MeditationScreenUI`.

`MeditationScreenUI`:

- реалм, ступень, опыт до следующей, Прозрение;
- индикатор «Безопасное место / Опасная зона»;
- сессия: накоплено опыта/Дао/Прозрения, множитель;
- кнопка «Медитировать» / «Прервать»;
- список активных дебафов с таймерами и эффектами;
- подсказка о риске и о том, что прерывание вне зоны даёт дебаф;
- не ставит паузу.

`SkillListUI`:

- две вкладки: «Техники» (15 существующих) и «Пассивные» (10 новых);
- для каждой строки: иконка, имя, школа, статус «Открыто / Требуется уровень
  Дао X / Требуется реалм Y», описание, эффект;
- открытые техники показывают стоимость Ци и КД;
- кнопка «Назначить» для активных техник (пишет в `AbilitySystem` через
  существующий `CultivationAbilityBridge`).

`AlchemyPanelUI`:

- слева список рецептов (в поле — только походные);
- справа ингредиенты «есть/нужно», требуемый грейд, шанс успеха, шансы качеств;
- кнопка «Варить» с блокировкой и причиной;
- журнал результатов (успех/провал/качество);
- вкладка «Печь / Походный» видна только у станции.

`PlayerVitalsUI`:

- HP-бар и Ци-бар; `QiBarUI` переиспользуется и встаёт под HP, как уже умеет;
- подписи «HP» и «Ци», реалм и ступень;
- при смерти — короткая вспышка и сообщение «Вы потеряли часть прогресса».

### 7.8 VFX

Новые файлы:

- `Assets/Scripts/Cultivation/Runtime/VFX/CultivationVfxConfig.cs` — ссылки на
  префабы, без строковых id в коде.
- `Assets/Scripts/Cultivation/Runtime/VFX/CultivationVfxBridge.cs` — подписки на
  события и проигрывание.

Маппинг (всё из `Assets/Magic`):

| Событие | Префаб |
|---|---|
| Аура культивации | `PersonAura/Holy Aura` (успех), `PersonAura/Evil Aura` (дебаф) |
| Медитация (петля) | `otherAura/Green Area` |
| Прорыв: старт | `otherAura/Blue Area + Distort Sphere` |
| Прорыв: успех | `2Dammage/Explosion Galaxy` + `Holy Aura` |
| Прорыв: провал | `Thick Smoke` + `otherAura/Red Area` |
| Срыв Ци | `1Dammage/Blue Tornado` |
| Отклонение Ци | `PersonAura/Dark Magic Orb` + `Thick Smoke` |
| Обрыв культивации | `Dark Magic Orb` + `otherAura/Red Area` + `Thick Smoke` |
| Смерть игрока | `Thick Smoke` + виньетка UI |

`DaoAuraController.auraPrefab` заполняется сетапом из конфига. Экранная виньетка
при обрыве — `Image` в UI с плавным появлением/исчезновением.

### 7.9 Сохранение

`CultivationSnapshot` расширяется до версии 2:

- `debuffType`, `debuffRemaining`, `severity`, `severityDecayAt`;
- `alchemyGrade`, `alchemySubRank`, `alchemyMastery`;
- `playerHealth`, `playerMaxHealth`;
- выученные техники уже есть (`learnedTechniqueIds`).

`CultivationStateCache`:

- ключ `Cultivation.Save.v2`;
- при отсутствии v2 читает v1 и переносит общие поля;
- дебафы с истёкшим временем не восстанавливаются.

### 7.10 Сетап сцены

Новый редакторский файл `Assets/Editor/CultivationWorldSetup.cs`:

- пункт меню `Tools/Cultivation/4. Setup World Content (Game)`.
- Создаёт/обновляет:
  - предметы-ресурсы, пилюли и их варианты качества;
  - `LootTable` для дерева, камня, растения, врага, животных;
  - рецепты алхимии;
  - `AlchemyConfig`, `MeditationRiskConfig`, `CultivationVfxConfig`;
  - печь `SFWC2_Crafting_Table` в поселении (рядом с `Smithy`);
  - `EnemyAI` на `Enemy`, `AnimalAI` на префабах животных и расставляет
    несколько животных на плоскости;
  - `PlayerHealth`, `PlayerRespawnService`, `CultivationDebuffController`,
    `AlchemySkill`, `PassiveSkillService` на игрока;
  - `DaoAltar` как точку респавна (если его нет — создаёт);
  - UI: `PlayerVitalsUI`, `DaoHudBarUI`, `MeditationScreenUI`, `SkillListUI`,
    `AlchemyPanelUI`, `DebuffPanelUI`;
  - кнопки в `PauseMenu` и `GameMenuController` на `Canvas`.
- Не создаёт дубли: ищет по имени и компоненту, обновляет существующее.
- Сохраняет сцену и ассеты, пишет отчёт в консоль.

Сетап идемпотентен: повторный запуск не плодит объекты.

## 8. Данные и контент

### 8.1 Конфиги

`MeditationRiskConfig` (значения по умолчанию):

- `threatRadius = 8`, `threatScanInterval = 0.25`;
- `escalationDecaySeconds = 180`, `escalationResetSeconds = 600`;
- три `CultivationFailureStage` из таблицы 7.3;
- `manualPenaltyStageIndex = 0`, `manualPenaltyOnlyUnsafe = true`.

`AlchemyConfig`:

- `chancePerRank = 0.05`, `chancePerComprehension = 0.01`;
- `stationBonus = 0.15`, `portablePenalty = 0.25`;
- `minChance = 0.05`, `maxChance = 0.95`;
- `ingredientLossOnFail = 0.5`, `masteryOnFail = 1`;
- `veinsMargin = 0.15`, `cloudMargin = 0.30` (запас броска);
- `veinsQualityBonus = 0.05`, `cloudQualityBonus = 0.10`;
- пороги мастерства на подранг.

`CultivationVfxConfig` — ссылки на префабы из 7.8.

### 8.2 Предметы

Ресурсы (`ItemType.Misc`, стакаются, `breakthroughTags` там, где нужно):

| Id | Имя | Теги | Иконка |
|---|---|---|---|
| `res.herb.mortal` | Трава смертного | `herb.mortal` | [PLACEHOLDER] Flowers |
| `res.herb.spirit` | Духовная трава | `herb.spirit` | [PLACEHOLDER] MagicEssence |
| `res.herb.blood` | Кровавый цветок | `herb.blood` | [PLACEHOLDER] Flower |
| `res.ore.iron` | Железная руда | `ore.iron` | [PLACEHOLDER] Iron |
| `res.ore.jade` | Нефритовая руда | `ore.jade` | [PLACEHOLDER] Diamond |
| `res.core.beast` | Ядро зверя | `core.beast` | [PLACEHOLDER] Magic Egg |
| `res.essence.golden` | Золотая эссенция | `essence.golden` | [PLACEHOLDER] SoulFragment |
| `res.wood.spirit` | Духовная древесина | — | [PLACEHOLDER] Wood |
| `res.pelt` | Шкура | — | [PLACEHOLDER] Wool |
| `res.meat.raw` | Сырое мясо | — | [PLACEHOLDER] Meat |

Пилюли (`ItemType.Consumable`, `useKind = Pill`), три варианта качества:

| Рецепт | Эффект | Грейд |
|---|---|---|
| `Пилюля возврата Ци` | `qiRestore` 40 / 70 / 110 | Common Low |
| `Пилюля очищения костного мозга` | `experienceQiGain` 60 / 110 / 180 | Common Middle |
| `Пилюля озарения` | `comprehensionGain` 1 / 2 / 3 | Earth Low |
| `Пилюля лечения` | `healAmount` 40 / 70 / 110 | Common Low |
| `Пилюля прорыва` | `BreakthroughMaterial`, теги реалма | Earth Middle |

`Pill Cloud` даёт эффект выше базового и, по лору, не теряет силу — в коде это
просто ещё один вариант `ItemData` с усиленными числами.

### 8.3 Рецепты

| Пилюля | Ингредиенты | Требования |
|---|---|---|
| Возврат Ци | `Herb_Mortal` ×2, `Herb_Spirit` ×1 | Common Low, Дао 1 |
| Очищение костного мозга | `Herb_Mortal` ×2, `Core_Beast` ×1 | Common Middle, Дао 2 |
| Озарение | `Herb_Spirit` ×1, `Essence_Golden` ×1 | Earth Low, Дао 3, реалм 1 |
| Лечение | `Herb_Mortal` ×1, `Meat_Raw` ×1 | Common Low, Дао 1 |
| Прорыв | `Herb_Spirit` ×1, `Ore_Jade` ×1, `Core_Beast` ×1 | Earth Middle, Дао 4, реалм 1 |

Походными считаются только «Возврат Ци» и «Лечение».

## 9. Краевые случаи

- **Инвентарь полон.** `TryDrop` кладёт в контейнер всегда; при подборе
  `WorldLootContainer` уже возвращает `false` и панель показывает «нет места».
- **Ингредиентов больше, чем нужно.** Списывается ровно по рецепту; лишнее
  остаётся.
- **Провал крафта при нехватке ингредиентов.** Проверка до списания; при
  частичном списании остаток не уходит в минус.
- **Рецепт выше грейда.** Кнопка заблокирована с причиной.
- **Смерть во время медитации.** Медитация мягко завершается, дебаф не
  накладывается, применяется только потеря опыта от смерти.
- **Смерть во время прорыва.** `BreakthroughSequence` отменяется, опыт не
  теряется дважды.
- **Прерывание, когда опыт сессии = 0.** Потеря 0, дебаф всё равно накладывается.
- **Медитация при 0 HP.** Недоступна.
- **Дебаф с истёкшим временем в сохранении.** Не восстанавливается.
- **Массовое убийство рядом.** Контейнеры сливаются по `mergeRadius`; больше
  одного контейнера на точку не появляется.
- **Враг вне NavMesh.** `EnemyAI` переключается на движение по `transform` и не
  падает с ошибкой.
- **Животное застряло.** Через `fleeSeconds` возвращается к блужданию.
- **Игрок открыл Esc во время медитации.** Меню ставит паузу; медитация
  останавливается по таймеру паузы (время стоит), риск не начисляется.
- **Игрок открыл экран медитации и ушёл.** Экран не блокирует движение; при
  движении сессия прерывается как ручная.
- **Нет `PlayerHealth` на сцене.** `EnemyAI` пишет предупреждение и не бьёт.
- **Нет `WorldLootService`.** `HarvestableObject` пишет предупреждение и
  использует старый `DropManager`.

## 10. Что меняется в существующих файлах

| Файл | Изменение | Причина |
|---|---|---|
| `Assets/Scripts/Drop/HarvestableObject.cs` | `lootTable`, `OnDied`, приоритет нового дропа | Источник лута |
| `Assets/Scripts/Drop/PlayerInteraction.cs` | не требуется | — |
| `Assets/Scripts/Enemy/Enemy.cs` | подписка на `OnDied` (если нужно скрыть бар) | Смерть врага |
| `Assets/Scripts/Cultivation/Runtime/Meditation/MeditationController.cs` | сессия, зона, угроза, причины прерывания | Ядро механики |
| `Assets/Scripts/Cultivation/Runtime/CultivationSystem.cs` | `LoseExperiencePercent`, событие | Смерть и срыв |
| `Assets/Scripts/Cultivation/Runtime/CultivationStateCache.cs` | v2-снимок, миграция | Сохранение |
| `Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs` | новые компоненты и панели | Сборка |
| `Assets/Scripts/Cultivation/Runtime/CultivationConfig.cs` | ссылки на конфиги | Данные |
| `Assets/Scripts/Cultivation/Runtime/TechniqueData.cs` | пассивные поля | Пассивки |
| `Assets/Scripts/Cultivation/Runtime/CultivationDefaults.cs` | 10 пассивов | Контент |
| `Assets/Scripts/Cultivation/Runtime/CultivationItemBridge.cs` | лечение, баффы, качество | Пилюли |
| `Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs` | новые панели, HUD | UI |
| `Assets/Scripts/Combat/AbilitySystem.cs` | `DamageMultiplier` | Сила заклинаний |
| `Assets/Inventary/Script/SO/ItemData.cs` | `healAmount`, баффы | Пилюли |
| `Assets/Inventary/Script/World/WorldLootService.cs` | `Instance` | Доступ из врагов |
| `Assets/Inventary/README.md` | разделы про добычу | Документация |
| `Assets/Scripts/Cultivation/README.md` | разделы про медитацию, алхимию, скиллы | Документация |
| `Assets/UiManager.cs` | делегирование Esc в `GameMenuController` | Единое меню |

`PlayerController.cs`, `DestructibleObject.cs`, `DaoAuraController.cs`,
`WeaponManager.cs`, `WeaponData.cs` — без изменений.

## 11. Этапы работ (срезы)

Каждый срез заканчивается компиляцией, ручной проверкой и коммитом.

1. **Фундамент угрозы.** `PlayerHealth`, `PlayerRespawnService`, `EnemyAI`,
   `AnimalAI`, правки `HarvestableObject`, `WorldLootService.Instance`,
   `CultivationSystem.LoseExperiencePercent`, `PlayerVitalsUI`.
2. **Медитация и дебафы.** `MeditationRiskConfig`, сессия, три стадии,
   `CultivationDebuffController`, `MeditationConsequenceService`, правки
   `MeditationController`, `DebuffPanelUI`, сохранение.
3. **Добыча и алхимия.** `LootTable`, ресурсы, пилюли, `Alchemy*`, печь,
   `AlchemyPanelUI`, `CultivationItemBridge` (лечение/баффы).
4. **Скиллы, меню и VFX.** `PassiveSkillService`, 10 пассивов, `SkillListUI`,
   `GameMenuController`, кнопки в `PauseMenu`, `DaoHudBarUI`,
   `MeditationScreenUI`, `CultivationVfxBridge`, аура/прорыв/обрыв.
5. **Сетап и приёмка.** `CultivationWorldSetup`, README, чеклист.

## 12. Критерии приёмки

Проверяется в Play mode на `Game.unity` после
`Tools/Cultivation/4. Setup World Content (Game)`.

1. **Здоровье.** В левом верхнем углу HP и Ци; враг подходит и бьёт; HP падает.
2. **Смерть.** При 0 HP игрок возвращается к алтарю, HP восстановлено частично,
   опыт ступени уменьшился, в консоли нет ошибок.
3. **Мирные животные.** Животное убегает после удара, затем успокаивается.
4. **Добыча.** После смерти врага/животного/дерева/камня/растения появляется
   контейнер с ресурсами; «Забрать» и «Забрать всё» работают; лишнего контейнера
   рядом нет.
5. **Медитация в зоне.** У алтаря множитель ×2, риск отсутствует; ручное
   прерывание бесплатно.
6. **Медитация вне зоны.** Индикатор «Опасная зона»; удар или подошедший враг
   прерывает; накладывается «Срыв Ци»; повторные прерывания дают «Отклонение
   Ци», затем «Обрыв культивации» с потерей опыта и VFX.
7. **Блок медитации.** После тяжёлого дебафа кнопка «Медитировать» заблокирована
   и показывает причину и таймер.
8. **Дебафы видны.** Панель под виталами показывает название, эффект и остаток
   времени; после истечения исчезает.
9. **Алхимия у печи.** У печи виден полный список рецептов; успешный крафт
   создаёт пилюлю и повышает мастерство; провал съедает часть ингредиентов.
10. **Походный крафт.** Из Esc-меню видны только походные рецепты; шанс ниже,
    чем у печи.
11. **Качество.** При высоком запасе броска получается Pill Veins или Pill Cloud
    с усиленным эффектом.
12. **Пилюли работают.** Лечение возвращает HP, возврат Ци — Ци, очищение —
    опыт, озарение — Прозрение, пилюля прорыва учитывается в «Прорыве».
13. **Скиллы.** Вкладка «Техники» показывает 15 техник со статусами; вкладка
    «Пассивные» — 10 пассивов; открытие пассива меняет цифры (HP, урон, реген).
14. **Левая полоса Дао.** Видна, показывает активное Дао и уровень; клик
    открывает экран медитации.
15. **Esc-меню.** В меню есть Культивация, Техники, Крафт, Карта, Журнал,
    Инвентарь, Навыки, Сохранение, Настройки, Выход; все открываются; Esc
    закрывает верхнюю панель, затем меню.
16. **VFX.** Аура, прорыв (успех/провал) и обрыв культивации видны и не ломают
    сцену.
17. **Сохранение.** После перезапуска Play mode реалм, ступень, опыт, дебафы,
    грейд алхимии и здоровье восстанавливаются.
18. **Ничего не сломано.** Инвентарь, экипировка, лут-контейнеры, оружие,
    хотбар и панель культивации работают как раньше.

## 13. Открытые вопросы

Нет. Все спорные решения зафиксированы в разделе 6. Числа баланса — значения по
умолчанию в конфигах, их можно менять без кода.
