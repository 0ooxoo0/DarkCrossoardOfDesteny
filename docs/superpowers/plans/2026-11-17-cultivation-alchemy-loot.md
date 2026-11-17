# Культивация, медитация, добыча и алхимия — план реализации

> **Для агента-исполнителя:** обязательный скилл — `superpowers:executing-plans`
> (native) или `superpowers:subagent-driven-development`. Шаги отмечаются
> чекбоксами. План реализует утверждённый дизайн
> `docs/superpowers/specs/2026-11-17-cultivation-alchemy-loot-design.md`.

**Goal:** Замкнуть цикл «убил зверя → выпал ресурс → сварил пилюлю → медитировал →
прокачал Дао → открыл скилл» и дать медитации цену: безопасные зоны, три стадии
срыва, дебафы, смерть с потерей опыта.

**Architecture:** Здоровье, угроза и добыча живут в новых компонентах
(`PlayerHealth`, `EnemyAI`, `AnimalAI`, `LootTable`) и подключаются к уже
существующим `HarvestableObject` и `WorldLootService` через события.
Культивация расширяется сбоку: `MeditationController` получает сессию, проверку
зоны и угрозы, а последствия считает отдельный `CultivationDebuffController`.
Алхимия — отдельная data-driven подсистема (`AlchemyRecipe` + `AlchemyService`),
качество пилюли хранится отдельными `ItemData`-вариантами. UI собирается
процедурно под существующим `Canvas`, сцена — идемпотентным Editor-скриптом.

**Tech Stack:** Unity 6000.2.10f1, Assembly-CSharp без asmdef, URP 17.6, uGUI,
TextMeshPro, Input System, `NavMeshAgent`, `JsonUtility`, `PlayerPrefs`.

**Spec:** `docs/superpowers/specs/2026-11-17-cultivation-alchemy-loot-design.md`

## Global Constraints

- **Не менять:** `Assets/Scripts/Player/PlayerController.cs`,
  `Assets/Scripts/DestructibleObject.cs`, `Assets/Scripts/Player/WeaponManager.cs`,
  `Assets/Scripts/Combat/WeaponData.cs`, `Assets/Scripts/Cultivation/Runtime/VFX/DaoAuraController.cs`.
- **`AbilitySystem.cs`** меняется только одним множителем урона (Task 14).
- **Не добавлять** теги и слои в `ProjectSettings/TagManager.asset`.
- **Не менять** модель персонажа, аватар и `Animator`; второго скелета не создавать.
- **Инвентарь не знает** о культивации, алхимии и сцене: связь только через события
  и `CultivationItemBridge`.
- **Один класс — один файл**, PascalCase, `[SerializeField] private`, без магических
  чисел и строк; XML-комментарии на русском над публичными классами и методами.
- **Проверка:** проект без рабочей тестовой сборки (asmdef нет, `Assembly-CSharp`
  не подключить к Test Framework). Цикл задачи = сборка
  `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo` (0 ошибок) + ручная
  проверка в Play mode/Editor. TDD-шаги заменены шагами проверки.
- **Сцену руками не править:** только `Tools → Cultivation → 4. Setup World Content (Game)`.
- **Идемпотентность:** повторный запуск сетапа не плодит объекты и не теряет ссылки.
- **Сохранение:** старый ключ `Cultivation.Save.v1` читается и мигрирует в v2.
- **Порядок задач обязателен:** каждая следующая опирается на интерфейсы предыдущих.

## Review Focus

1. **Смерть во время медитации или прорыва.** Опыт не теряется дважды, корутины
   останавливаются, дебаф не накладывается поверх смерти, респавн не оставляет
   `timeScale = 0` и выключенного игрока.
2. **Esc и стек панелей.** Esc закрывает верхнюю панель, затем меню; `timeScale`
   возвращается в 1, игрок включается, курсор остаётся рабочим. Ни одна панель не
   остаётся висеть поверх игры.
3. **Добыча без потерь и дублей.** Если контейнер не создался, предмет не исчезает;
   при массовом убийстве рядом нет двух контейнеров на одной точке; пустой
   контейнер исчезает.
4. **Дебафы и множители.** Несколько прерываний не дают отрицательный реген Ци,
   максимальное HP не падает ниже текущего, после перезагрузки истёкшие дебафы не
   восстанавливаются, а активные — восстанавливаются с остатком времени.
5. **Алхимия и ингредиенты.** Провал не уводит количество в минус и не списывает
   больше, чем есть; двойной клик по «Варить» не создаёт две пилюли и не списывает
   дважды; при полном инвентаре результат не теряется молча.

---

### Task 1: Здоровье игрока и потеря опыта

**Files:**
- Create: `Assets/Scripts/Combat/DamageInfo.cs`
- Create: `Assets/Scripts/Combat/PlayerHealth.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationSystem.cs` (новый метод и событие)

**Interfaces:**
- Consumes: `CultivationSystem.ExperienceQi`, `SetExperienceQi(int)`.
- Produces:
  - `struct DamageInfo { float Amount; GameObject Source; Vector3 HitPoint; bool IsCritical; }`
  - `PlayerHealth.Current`, `.Max`, `.Normalized`, `.IsDead`
  - `PlayerHealth.TakeDamage(DamageInfo)`, `.Heal(int)`, `.RestorePercent(float)`,
    `.SetInvulnerable(float)`, `.RecalculateMaxHealth()`, `.NotifyRespawned()`
  - `PlayerHealth.IncomingDamageMultiplier`, `.MaxHealthMultiplier` (settable)
  - события `OnHealthChanged(int current, int max)`, `OnDamaged(DamageInfo)`,
    `OnDied`, `OnRespawned`
  - `CultivationSystem.LoseExperiencePercent(float) -> int`,
    `event Action<int> OnExperienceLost`

- [ ] **Step 1: Создать `DamageInfo`**

Поля: `Amount`, `Source`, `HitPoint`, `IsCritical`. Конструктор
`DamageInfo(float amount, GameObject source, Vector3 hitPoint, bool isCritical = false)`.

- [ ] **Step 2: Создать `PlayerHealth`**

Сигнатуры из блока Interfaces. Поля инспектора:
`baseMaxHealth = 100f`, `regenPerSecond = 0.5f`, `regenDelayAfterDamage = 5f`,
ссылка `cultivation` (опционально). Длительность неуязвимости задаётся вызывающей
стороной через `SetInvulnerable`, своего поля в `PlayerHealth` нет.

`Max = baseMaxHealth + cultivation.TotalHpBonus`, затем умножается на
`MaxHealthMultiplier` и округляется вниз, минимум 1. При изменении максимума
`Current` клампится сверху, снизу — 0.

`TakeDamage`: выход, если `IsDead` или идёт неуязвимость; `Amount` умножается на
`IncomingDamageMultiplier`; `regenDelay` сбрасывается; событие `OnDamaged`; при
`Current <= 0` — `Die()` (один раз, флаг `IsDead`).

`Heal` и `RestorePercent` не воскрешают мёртвого.

- [ ] **Step 3: Добавить `LoseExperiencePercent` в `CultivationSystem`**

Возвращает фактически потерянное количество: `Mathf.RoundToInt(ExperienceQi * percent)`,
клампится снизу нулём, применяется через `SetExperienceQi`. Событие
`OnExperienceLost(int lost)` вызывается только при `lost > 0`.

- [ ] **Step 4: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Ручная проверка**

В Play mode через временный вызов из консоли/инспектора: `TakeDamage` уменьшает HP,
`Heal` возвращает, при 0 HP `OnDied` срабатывает один раз, `LoseExperiencePercent(0.25)`
уменьшает опыт ступени ровно на четверть.

- [ ] **Step 6: Коммит**

```bash
git add Assets/Scripts/Combat/DamageInfo.cs Assets/Scripts/Combat/PlayerHealth.cs Assets/Scripts/Cultivation/Runtime/CultivationSystem.cs
git commit -m "feat: add player health and cultivation experience loss"
```

---

### Task 2: Смерть, респавн и единый лут-пайплайн

**Files:**
- Create: `Assets/Scripts/Loot/LootRoll.cs`
- Create: `Assets/Scripts/Loot/LootTable.cs`
- Create: `Assets/Scripts/Combat/PlayerRespawnService.cs`
- Modify: `Assets/Scripts/Drop/HarvestableObject.cs` (событие смерти и ролл лута)
- Modify: `Assets/Inventary/Script/World/WorldLootService.cs` (статический `Instance`)

**Interfaces:**
- Consumes: `PlayerHealth.OnDied`, `PlayerHealth.RestorePercent`, `SetInvulnerable`,
  `CultivationSystem.LoseExperiencePercent`, `WorldLootService.TryDrop`,
  `WorldLootContainer.Add`.
- Produces:
  - `struct LootDrop { ItemData Item; int Quantity; }`
  - `LootTable.Rolls` (`IReadOnlyList<LootRoll>`), `LootTable.Roll(List<LootDrop>)`
  - `HarvestableObject.LootTable` (свойство), `event Action<HarvestableObject, Vector3> OnDied`
  - `WorldLootService.Instance`
  - `PlayerRespawnService.SetRespawnPoint(Transform)`, `Respawn()`

- [ ] **Step 1: Создать `LootRoll` и `LootTable`**

`LootRoll` — `[Serializable]` класс: `ItemData item`, `int min = 1`,
`int max = 1`, `float chance = 1f`, `bool guaranteed`.

`LootTable : ScriptableObject` — `[CreateAssetMenu(menuName = "Loot/Loot Table")]`,
поле `List<LootRoll> rolls`, свойство `Rolls`, метод `Roll(List<LootDrop> output)`.
Правило: гарантированные строки всегда дают `Random.Range(min, max+1)`; остальные —
с шансом `chance`; пустой `item` пропускается.

- [ ] **Step 2: Добавить `Instance` в `WorldLootService`**

`public static WorldLootService Instance { get; private set; }`; присваивается в
`Awake`, обнуляется в `OnDestroy` при совпадении.

- [ ] **Step 3: Расширить `HarvestableObject`**

Добавить `[SerializeField] private LootTable lootTable;` и
`public event Action<HarvestableObject, Vector3> OnDied;`.
В `Die()`: сначала `OnDied?.Invoke(this, transform.position)`, затем:
если `lootTable != null` и `WorldLootService.Instance != null` — временный
`List<LootDrop>`, `lootTable.Roll(list)`, для каждого `TryDrop(item, qty, dropSpawnPoint ?? transform)`;
иначе — прежний `DropManager`-путь. `Destroy(gameObject)` остаётся последним.

- [ ] **Step 4: Создать `PlayerRespawnService`**

Поля: `Transform respawnPoint`, `float experienceLossPercent = 0.25f`,
`float hpRestorePercent = 0.5f`, `float invulnerabilityAfterRespawn = 3f`,
ссылки `PlayerHealth health`, `CultivationSystem cultivation`, `Transform playerRoot`.

На `PlayerHealth.OnDied`:
0. если `BreakthroughSequence.IsRunning` — `CancelBreakthrough()` (иначе смерть
   во время прорыва оставит корутину и двойной штраф);
1. `cultivation.LoseExperiencePercent(experienceLossPercent)`;
2. отключить `CharacterController` и `NavMeshAgent`, переместить к точке,
   включить обратно (если компоненты есть);
3. `health.RestorePercent(hpRestorePercent)`, `health.SetInvulnerable(...)`;
4. `health.NotifyRespawned()` — поднимает `OnRespawned` для UI и VFX.

`Respawn()` публичный для ручного вызова.

- [ ] **Step 5: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 6: Ручная проверка**

На сцене: временно повесить `PlayerRespawnService`, задать точку, позвать
`TakeDamage` с летальным уроном. Проверить: игрок на точке, HP = 50%, опыт уменьшен
один раз, в консоли нет ошибок. На `Tree` повесить `LootTable` с одним ресурсом —
после смерти появляется `WorldLootContainer`, а не прежний префаб.

- [ ] **Step 7: Коммит**

```bash
git add Assets/Scripts/Loot Assets/Scripts/Combat/PlayerRespawnService.cs Assets/Scripts/Drop/HarvestableObject.cs Assets/Inventary/Script/World/WorldLootService.cs
git commit -m "feat: add loot tables, death event and player respawn"
```

---

### Task 3: Враг, который агрится и бьёт

**Files:**
- Create: `Assets/Scripts/Enemy/EnemyAI.cs`

**Interfaces:**
- Consumes: `PlayerHealth.TakeDamage(DamageInfo)`, `HarvestableObject.OnDied`,
  `PlayerHealth` на игроке.
- Produces: `EnemyAI.IsAggroed` (bool), `EnemyAI.SetTarget(Transform)`.

- [ ] **Step 1: Создать `EnemyAI`**

Поля инспектора: `aggroRadius = 10f`, `attackRange = 2f`,
`attackDamage = 8f`, `attackCooldown = 1.6f`, `moveSpeed = 3.2f`,
`leashRadius = 18f`, `loseTargetSeconds = 6f`, ссылки
`NavMeshAgent agent`, `HarvestableObject health`, `Animator animator`,
`string attackTriggerName` (пусто = без анимации).

Состояния: `Idle`, `Chase`, `Attack`, `Return`, `Dead`. Цель — объект с
`PlayerHealth` (ищется по `FindFirstObjectByType` один раз в `Awake`).

Поведение: в радиусе агра и при живой цели — `Chase`; в `attackRange` и кулдаун
готов — `Attack` (урон `PlayerHealth.TakeDamage`); дальше `leashRadius` —
`Return` к стартовой точке; цель потеряна дольше `loseTargetSeconds` — `Idle`.
`OnDied` переводит в `Dead` и останавливает агента.

- [ ] **Step 2: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 3: Ручная проверка**

Повесить `EnemyAI` на `Enemy` в сцене, добавить `PlayerHealth` игроку. Враг
подходит, бьёт с интервалом, HP падает; после отхода за `leashRadius` возвращается.
Убийство врага не оставляет ошибок в консоли.

- [ ] **Step 4: Коммит**

```bash
git add Assets/Scripts/Enemy/EnemyAI.cs
git commit -m "feat: add enemy chase and melee attack"
```

---

### Task 4: Мирные животные

**Files:**
- Create: `Assets/Scripts/Enemy/AnimalAI.cs`

**Interfaces:**
- Consumes: `HarvestableObject.Damaged`, `HarvestableObject.OnDied`.
- Produces: `AnimalAI.IsFleeing` (bool).

- [ ] **Step 1: Создать `AnimalAI`**

Поля: `wanderRadius = 8f`, `wanderSpeed = 1.2f`, `fleeSpeed = 4f`,
`fleeSeconds = 4f`, `idleSeconds = 2f`, ссылки `NavMeshAgent agent`,
`HarvestableObject health`.

Состояния: `Wander`, `Idle`, `Flee`, `Dead`. Точка блуждания выбирается в
`wanderRadius` вокруг стартовой позиции; на `Damaged` — `Flee` от игрока
(или от последнего источника урона) на `fleeSeconds`, затем возврат к блужданию.
`OnDied` → `Dead`.

- [ ] **Step 2: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 3: Ручная проверка**

Повесить `AnimalAI` на префаб животного, поставить на плоскость с NavMesh: животное
бродит; после удара убегает и через `fleeSeconds` успокаивается.

- [ ] **Step 4: Коммит**

```bash
git add Assets/Scripts/Enemy/AnimalAI.cs
git commit -m "feat: add peaceful fleeing animals"
```

---

### Task 5: HUD здоровья и Ци

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/UI/PlayerVitalsUI.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs` (создание панели)

**Interfaces:**
- Consumes: `PlayerHealth.OnHealthChanged`, `QiComponent.OnQiChanged`,
  `CultivationSystem.OnRealmChanged`/`OnStageChanged`, `QiBarUI.Build`.
- Produces: `PlayerVitalsUI.Build(PlayerHealth, QiComponent, CultivationSystem, DaoSystem, Transform)`.

- [ ] **Step 1: Создать `PlayerVitalsUI`**

Процедурная панель в левом верхнем углу: HP-бар, под ним `QiBarUI` (передаётся
его `RectTransform` как `hpBar`), строка «Реалм · ступень». HP-бар красный,
Ци — цвет активного Дао. Подписи `HP 100/100` и `Ци 50/100`. События
отписываются в `OnDestroy`.

- [ ] **Step 2: Подключить в `CultivationUIRoot.Create`**

Создать объект `PlayerVitals` под `Canvas`, вызвать `Build`, сохранить ссылку в
свойстве `Vitals`.

- [ ] **Step 3: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 4: Ручная проверка**

Play mode: в левом верхнем углу два бара и подпись реалма; урон по игроку двигает
HP, каст техники тратит Ци и двигает второй бар.

- [ ] **Step 5: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/UI/PlayerVitalsUI.cs Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs
git commit -m "feat: add player vitals HUD"
```

---

### Task 6: Данные риска и контроллер дебафов

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/CultivationVfxKind.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/CultivationFailureStage.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/MeditationRiskConfig.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/MeditationSession.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/ActiveCultivationDebuff.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/CultivationDebuffController.cs`

**Interfaces:**
- Produces:
  - `enum CultivationVfxKind { None, QiSurge, QiDeviation, Severing }`
  - `[Serializable] class CultivationFailureStage { string DisplayName; float DurationSeconds;
    float QiRegenMultiplier; float DamageDealtMultiplier; float MaxHealthMultiplier;
    float SessionExperienceLossPercent; float StageExperienceLossPercent;
    float MeditationBlockSeconds; CultivationVfxKind Vfx; }`
  - `MeditationRiskConfig.ThreatRadius`, `.ThreatScanInterval`, `.EscalationDecaySeconds`,
    `.EscalationResetSeconds`, `.Stages`, `.GetStage(int severity)`,
    `.ManualPenaltyStageIndex`, `.ManualPenaltyOnlyUnsafe`
  - `MeditationSession.Start()`, `.Reset()`, `.AddExperience(int)`, `.AddDaoPoints(int)`,
    `.AddComprehension(int)`, `.AccumulatedExperience`, `.AccumulatedDaoPoints`,
    `.AccumulatedComprehension`, `.WasSafe`
  - `CultivationDebuffController.Severity`, `.IsMeditationBlocked`,
    `.QiRegenMultiplier`, `.DamageDealtMultiplier`, `.IncomingDamageMultiplier`,
    `.MaxHealthMultiplier`, `.Active`, `.Apply(CultivationFailureStage, int severity)`,
    `.Tick(float)`, `.ClearAll()`, `.NotifyCleanPeriod(float)`; события
    `OnDebuffApplied`, `OnDebuffExpired`, `OnSeverityChanged`

- [ ] **Step 1: Создать перечисление и стадию**

`CultivationVfxKind` — как в Interfaces. `CultivationFailureStage` — `[Serializable]`
класс с полями из Interfaces и методом `ApplyDefaults(...)` не нужен.

- [ ] **Step 2: Создать `MeditationRiskConfig`**

`ScriptableObject`, `[CreateAssetMenu(menuName = "Cultivation/Meditation Risk")]`.
Поля из Interfaces. Значения по умолчанию — из таблицы 7.3 спеки:
`threatRadius = 8f`, `threatScanInterval = 0.25f`, `escalationDecaySeconds = 180f`,
`escalationResetSeconds = 600f`, три стадии с числами спеки.
`GetStage(int severity)` возвращает стадию по индексу `severity - 1`, кламп.

Дополнительно добавить в `CultivationConfig` поле
`[SerializeField] private MeditationRiskConfig meditationRisk;` и свойство
`public MeditationRiskConfig MeditationRisk => meditationRisk;` — чтобы bootstrap
не искал ассет по строковому пути.

- [ ] **Step 3: Создать `MeditationSession`**

Обычный класс (не `MonoBehaviour`). `Start()` запоминает `Time.time`, обнуляет
накопления. `WasSafe` выставляется вызывающей стороной. Суммы — целые.

- [ ] **Step 4: Создать `ActiveCultivationDebuff`**

Класс: `CultivationFailureStage Stage`, `float Remaining`, `int Severity`,
`bool IsExpired`, `void Tick(float dt)`, `CultivationVfxKind Vfx`.

- [ ] **Step 5: Создать `CultivationDebuffController`**

Поля: ссылка `MeditationRiskConfig config`, `CultivationSystem cultivation`,
`QiComponent qi`. Активные дебафы — `List<ActiveCultivationDebuff>`.

Множители:
- `QiRegenMultiplier` = произведение `QiRegenMultiplier` активных, клампится в `[0, 1]`.
- `DamageDealtMultiplier` и `IncomingDamageMultiplier` — произведение, кламп `[0.1, 1]`.
- `MaxHealthMultiplier` — произведение, кламп `[0.1, 1]`.

`Apply(stage, severity)`: удаляет активный дебаф той же стадии (обновление), добавляет
новый, поднимает `Severity`, сбрасывает таймер затухания.

`Tick(dt)`: тикает дебафы, по истечении убирает и вызывает `OnDebuffExpired`;
считает время без прерываний и снижает/сбрасывает `Severity`.

`IsMeditationBlocked` = есть дебаф с `Stage.MeditationBlockSeconds > 0` и
`Remaining > 0`, либо `QiRegenMultiplier <= 0`.

- [ ] **Step 6: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 7: Ручная проверка**

В Play mode вызвать `Apply` вручную: множители меняются, `Tick` уменьшает время,
по истечении дебаф исчезает, множители возвращаются к 1.

- [ ] **Step 8: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/Meditation
git commit -m "feat: add cultivation risk config and debuff controller"
```

---

### Task 7: Медитация с безопасностью, угрозой и последствиями

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/MeditationInterruptionReason.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Meditation/MeditationConsequenceService.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/Meditation/MeditationController.cs`

**Interfaces:**
- Consumes: `MeditationSession`, `CultivationDebuffController`, `PlayerHealth.OnDamaged`,
  `EnemyAI.IsAggroed`, `CultivationSystem.LoseExperiencePercent`.
- Produces:
  - `enum MeditationInterruptionReason { Manual, Damage, Threat, Death }`
  - `MeditationController.IsInSafeZone`, `.Session`, `.Interrupt(MeditationInterruptionReason)`,
    `.StartMeditation()`, `.StopMeditationForDeath()`
  - `MeditationConsequenceService.Apply(MeditationInterruptionReason, MeditationSession, bool wasSafe)`

- [ ] **Step 1: Создать перечисление и сервис**

`MeditationConsequenceService` — `MonoBehaviour` с ссылками
`CultivationDebuffController debuffs`, `CultivationSystem cultivation`,
`MeditationRiskConfig config`.

`Apply(reason, session, wasSafe)`:
- `Manual` и `wasSafe` → выходят без последствий;
- `Death` → выходят без дебафа;
- `Manual` вне зоны → severity 1;
- `Damage`/`Threat` → severity `min(debuffs.Severity + 1, config.Stages.Count)`;
- взять стадию через `config.GetStage(severity)`;
- потерять `session.AccumulatedExperience * stage.SessionExperienceLossPercent` через
  `cultivation.LoseExperiencePercent` от текущего опыта (проценты складываются,
  но клампятся в 1);
- `debuffs.Apply(stage, severity)`;
- логировать причину и потерю.

- [ ] **Step 2: Расширить `MeditationController`**

Добавить поля: `MeditationRiskConfig riskConfig`, `CultivationDebuffController debuffs`,
`MeditationConsequenceService consequences`, `PlayerHealth playerHealth`,
`float threatRadiusFallback`.

`IsInSafeZone` = `zones.Count > 0`. `Session` — новый объект сессии.

`EnterMeditation`: отказ, если `debuffs.IsMeditationBlocked` или `playerHealth.IsDead`;
запуск сессии с `WasSafe = IsInSafeZone`.

`Update`: раз в `riskConfig.ThreatScanInterval` — `Physics.OverlapSphere` по
`threatRadius`; если найден `EnemyAI` с `IsAggroed` и линией видимости — `Interrupt(Threat)`.

`OnEnable`: подписка на `playerHealth.OnDamaged` → `Interrupt(Damage)` (вместо
старого `HandleInterrupted`); старый `IMeditationInterrupter` оставить как
дополнительный источник, но с причиной `Damage`.

`Interrupt(reason)`: если не медитирует — выход; `ExitMeditation()`;
`consequences.Apply(reason, Session, Session.WasSafe)`; событие
`OnMeditationInterrupted`.

`StopMeditationForDeath()`: `ExitMeditation()` без последствий.

Накопление в сессии — в `TickRoutine` рядом с начислением опыта/Дао/Прозрения.

- [ ] **Step 3: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 4: Ручная проверка**

В зоне алтаря: медитация идёт, ручной выход бесплатен. Перед проверкой создать
ассет `Create → Cultivation → Meditation Risk` и назначить его в `CultivationConfig`.
Вне зоны: удар врага
прерывает и вешает «Срыв Ци»; повторное прерывание даёт «Отклонение Ци»;
третье — «Обрыв культивации». В консоли видно причину и потерю опыта.
Отдельно: умереть во время медитации — медитация завершается без дебафа, опыт
теряется только от смерти, ошибок и двойного списания нет.

- [ ] **Step 5: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/Meditation
git commit -m "feat: add meditation threat detection and staged failure consequences"
```

---

### Task 8: Панель дебафов и сохранение v2

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/UI/DebuffPanelUI.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationStateCache.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs` (регистрация)

**Interfaces:**
- Consumes: `CultivationDebuffController.Active`, `.OnDebuffApplied`,
  `.OnDebuffExpired`, `PlayerHealth`.
- Produces:
  - `DebuffPanelUI.Build(CultivationDebuffController, Transform)`
  - `CultivationSnapshot` v2 с полями `debuffKind`, `debuffRemaining`,
    `debuffSeverity`, `severityDecayAt`, `playerHealth`, `playerMaxHealth`

- [ ] **Step 1: Создать `DebuffPanelUI`**

Компактный список под виталами: имя стадии, остаток времени, строка эффектов
(«Ци-реген −60%», «Урон −25%», «HP −20%»). Обновление раз в 0.2 с, без
пересоздания объектов каждый кадр.

- [ ] **Step 2: Расширить снимок и миграцию**

`CultivationSnapshot`: новые поля из Interfaces. Ключ v2
`Cultivation.Save.v2`; `LoadFromPrefs` при отсутствии v2 читает v1 и переносит
общие поля. `Capture` получает дополнительные параметры (дебафы, здоровье,
алхимия). `TryRestore` восстанавливает только неистёкшие дебафы.

- [ ] **Step 3: Подключить в `CultivationBootstrap`**

Создать `CultivationDebuffController` на игроке (если нет), передать конфиг,
добавить `DebuffPanelUI` в `CultivationUIRoot`.

- [ ] **Step 4: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Ручная проверка**

Дебаф виден с таймером; после выхода из Play mode и повторного входа активный
дебаф восстановлен с остатком времени, истёкший — нет. Старый сейв v1 читается
без ошибок. Отдельно: получить три прерывания подряд, выйти и войти — счётчик
стадии и остаток времени совпадают, реген Ци не уходит в минус.

- [ ] **Step 6: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/UI/DebuffPanelUI.cs Assets/Scripts/Cultivation/Runtime/CultivationStateCache.cs Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs
git commit -m "feat: add debuff UI and v2 cultivation save"
```

---

### Task 9: Пилюли, лечение и контент ресурсов

**Files:**
- Modify: `Assets/Inventary/Script/SO/ItemData.cs` (`healAmount`)
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationItemBridge.cs` (лечение)
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs` (передать `PlayerHealth` в мост)
- Create: `Assets/Editor/CultivationWorldSetup.cs` (часть: контент предметов)

**Interfaces:**
- Consumes: `PlayerHealth.Heal(int)`, `CultivationItemBridge.ApplyPill`.
- Produces: `ItemData.healAmount` (int); ассеты в
  `Assets/Inventary/Data/Content/` и `Assets/Inventary/Data/Content/Pills/`.

- [ ] **Step 1: Добавить `healAmount`**

`[Header("Pill")] [Min(0)] public int healAmount;` рядом с существующими полями пилюли.

- [ ] **Step 2: Применять лечение в `CultivationItemBridge`**

Ссылка `PlayerHealth playerHealth` и параметр в `Initialize(...)`. В `ApplyPill`:
если `healAmount > 0` и `playerHealth != null` — `Heal`, `applied = true`.
Условие «пилюля не имеет эффекта» учитывает `healAmount`.
Обновить вызов `itemBridge.Initialize(...)` в `CultivationBootstrap.WireInventory`,
передав найденный `PlayerHealth` (если его нет — `null`).

- [ ] **Step 3: Создать редакторский сетап контента**

`CultivationWorldSetup` с `[MenuItem("Tools/Cultivation/4. Setup World Content (Game)", priority = 4)]`
и приватным `EnsureItems()`. Идемпотентно создаёт `ItemData` по таблицам 8.2 спеки:
10 ресурсов, 5 пилюль × 3 качества (`_Normal`, `_Veins`, `_Cloud`).
Иконки — существующие спрайты из
`Assets/Ideals/Menu/MenuSaves/Brackeys/2D Mega Pack/Items & Icons/`
(помечены `[PLACEHOLDER]` в константах). `breakthroughTags` — из спеки.

- [ ] **Step 4: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Ручная проверка**

Запустить меню. В `Assets/Inventary/Data/Content/` появились ассеты; повторный
запуск не создаёт дубли. Пилюля с `healAmount` лечит игрока, пилюля без эффекта
пишет предупреждение и не тратится.

- [ ] **Step 6: Коммит**

```bash
git add Assets/Inventary/Script/SO/ItemData.cs Assets/Scripts/Cultivation/Runtime/CultivationItemBridge.cs Assets/Editor/CultivationWorldSetup.cs Assets/Inventary/Data
git commit -m "feat: add healing pills and resource item content"
```

---

### Task 10: Таблицы лута для мира

**Files:**
- Modify: `Assets/Editor/CultivationWorldSetup.cs` (часть: `EnsureLootTables`)

**Interfaces:**
- Consumes: `LootTable`, `LootRoll`, ассеты предметов из Task 9.
- Produces: ассеты `Assets/Inventary/Data/Loot/*.asset`, привязка к объектам сцены.

- [ ] **Step 1: Добавить `EnsureLootTables`**

Создаёт/обновляет таблицы: `Loot_Tree`, `Loot_Rock`, `Loot_Plant`, `Loot_Enemy`,
`Loot_Animal`. Содержимое — таблица из раздела 7.4 спеки.

- [ ] **Step 2: Привязать к объектам**

Найти в открытой сцене объекты `Tree`, `Rock`, `Plant`, `Enemy` и выставить
`HarvestableObject.lootTable` через `SerializedObject` (поле приватное).
Отсутствие объекта — предупреждение, не ошибка.

- [ ] **Step 3: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 4: Ручная проверка**

Убить дерево, камень, растение и врага: выпадают ресурсы из таблиц, рядом нет
второго контейнера, пустой контейнер исчезает после «Забрать всё».
Дополнительно: временно убрать `WorldLootService` из сцены — при смерти объекта
предмет не должен исчезать молча, в консоли должно быть предупреждение.

- [ ] **Step 5: Коммит**

```bash
git add Assets/Editor/CultivationWorldSetup.cs Assets/Inventary/Data/Loot
git commit -m "feat: add loot tables for world objects"
```

---

### Task 11: Данные алхимии

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyGrade.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyConfig.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyIngredient.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyRecipe.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemySkill.cs`

**Interfaces:**
- Produces:
  - `enum AlchemyGradeName` (12 значений), `enum AlchemySubRank { Low, Middle, High, Great, None }`
  - `struct AlchemyGrade` с `Name`, `SubRank`, `RankIndex`, `DisplayName`,
    `static AlchemyGrade CommonLow`, `CompareTo`, `==`, `!=`, `<`, `>`
  - `AlchemyConfig` (`ChancePerRank`, `ChancePerComprehension`, `StationBonus`,
    `PortablePenalty`, `MinChance`, `MaxChance`, `IngredientLossOnFail`,
    `MasteryOnFail`, `VeinsMargin`, `CloudMargin`, `VeinsQualityBonus`,
    `CloudQualityBonus`, `MasteryPerSubRank`, `PortableQualityPenalty`)
  - `AlchemyIngredient { ItemData Item; int Count; }`
  - `AlchemyRecipe` (`RecipeId`, `DisplayName`, `Description`, `Output`, `OutputCount`,
    `Ingredients`, `RequiredGrade`, `RequiredDaoLevel`, `RequiredRealmIndex`,
    `BaseSuccessChance`, `PortableAllowed`, `MasteryReward`)
  - `AlchemySkill` (`Grade`, `Mastery`, `MasteryForNextRank`, `AddMastery(int)`,
    `Restore(AlchemyGrade, int)`, `event Action OnChanged`), ссылка на `AlchemyConfig`

- [ ] **Step 1: Создать `AlchemyGrade`**

`RankIndex = (int)Name * 3 + (int)SubRank`, где `Great` и `None` дают +2.
`DisplayName` — «Земной · Средний», «Императорский · Великий», «Открывающий Небеса».
Реализовать `IComparable<AlchemyGrade>`.

- [ ] **Step 2: Создать `AlchemyConfig`**

`ScriptableObject`, меню `Cultivation/Alchemy Config`. Значения по умолчанию — из
раздела 8.1 спеки.

- [ ] **Step 3: Создать `AlchemyIngredient` и `AlchemyRecipe`**

`AlchemyIngredient` — `[Serializable]`. `AlchemyRecipe` — `ScriptableObject`,
меню `Cultivation/Alchemy Recipe`, поля из Interfaces, `IReadOnlyList<AlchemyIngredient> Ingredients`.

- [ ] **Step 4: Создать `AlchemySkill`**

`MonoBehaviour`. `AddMastery(int)` повышает подранг, затем грейд; на максимуме
мастерство не растёт. `MasteryForNextRank` = `config.MasteryPerSubRank`.
Событие `OnChanged` после любого изменения.
Дополнительно расширить `CultivationSnapshot`, `Capture` и `TryRestore` полями
`alchemyGrade` (int), `alchemySubRank` (int), `alchemyMastery` (int).

- [ ] **Step 5: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 6: Ручная проверка**

В инспекторе создать `AlchemyRecipe`, выставить грейд; проверить, что
`RankIndex` растёт монотонно и `DisplayName` читаемый. `AddMastery` переводит
Common Low → Common Middle по достижении порога.

- [ ] **Step 7: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/Alchemy
git commit -m "feat: add alchemy grade, recipe and skill data"
```

---

### Task 12: Сервис крафта

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/PillQuality.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyResult.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyService.cs`

**Interfaces:**
- Consumes: `AlchemyRecipe`, `AlchemySkill`, `AlchemyConfig`, `Inventory`,
  `CultivationSystem.Comprehension`, `Inventory.RemoveItem(ItemData, int)`.
- Produces:
  - `enum PillQuality { Normal, PillVeins, PillCloud }`
  - `struct AlchemyResult { bool Success; PillQuality Quality; int OutputCount;
    string Message; float Chance; }`
  - `AlchemyService.Random01` (`Func<float>`, подменяемый)
  - `AlchemyService.ComputeChance(AlchemyRecipe, bool atStation) -> float`
  - `AlchemyService.CanCraft(AlchemyRecipe, Inventory, bool atStation, out string reason) -> bool`
  - `AlchemyService.Craft(AlchemyRecipe, Inventory, bool atStation) -> AlchemyResult`
  - `AlchemyService.ResolveQuality(AlchemyRecipe, bool atStation, float roll) -> PillQuality`
  - `AlchemyService.OutputForQuality(AlchemyRecipe) -> ItemData`
    (контракт: `recipe.Output` указывает на вариант `_Normal`; метод заменяет
    суффикс `_Normal` на `_Veins`/`_Cloud` в имени ассета; если вариант не найден —
    возвращает `Output` и пишет предупреждение один раз)

- [ ] **Step 1: Создать `PillQuality` и `AlchemyResult`**

Как в Interfaces. `Message` — короткая русская строка для UI и лога.
`AlchemyService` — `MonoBehaviour` с полями `AlchemyConfig config`,
`AlchemySkill skill`, `CultivationSystem cultivation`.

- [ ] **Step 2: Реализовать проверки и шанс**

`ComputeChance` = `BaseSuccessChance + (playerRank - requiredRank) * ChancePerRank +
Comprehension * ChancePerComprehension + (atStation ? StationBonus : -PortablePenalty)`,
кламп `[MinChance, MaxChance]`. Для походного крафта рецепт должен иметь
`PortableAllowed`.

`CanCraft` проверяет: рецепт не null; грейд игрока ≥ требуемого; уровень Дао и
реалм; ингредиенты в нужном количестве; походный режим; инвентарь не null.
Причина — конкретная строка («Нужен грейд Земной · Низкий», «Не хватает: Духовная
трава 1»).

- [ ] **Step 3: Реализовать `Craft`**

Порядок:
1. `CanCraft`;
2. `OutputForQuality(...)` и проверка `Inventory.GetFreeSpace(output) >= count` —
   если места нет, вернуть провал с сообщением «В инвентаре нет места», **ничего
   не списывая**;
3. бросок `Random01()`;
4. успех: списать ингредиенты, `Inventory.AddItem(output, count)`,
   `AlchemySkill.AddMastery(recipe.MasteryReward)`;
5. провал: списать `Mathf.Max(1, Ceil(count * IngredientLossOnFail))` по каждому
   ингредиенту, но не больше имеющегося; `AddMastery(MasteryOnFail)`.

- [ ] **Step 4: Реализовать качество**

`ResolveQuality` считает запас `roll - chance`. Если запас ≥ `CloudMargin` —
`PillCloud`; иначе ≥ `VeinsMargin` — `PillVeins`; иначе `Normal`. Бонусы грейда и
станции добавляются к запасу. Походный крафт уменьшает запас на
`PortableQualityPenalty`.

- [ ] **Step 5: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 6: Ручная проверка**

Подменить `Random01` на константу 0.99: успех и `Normal`; 0.01: провал и списание
части ингредиентов. Проверить, что двойной вызов `Craft` с теми же ингредиентами
не уводит количество в минус и не выдаёт две пилюли при нехватке.

- [ ] **Step 7: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/Alchemy
git commit -m "feat: add alchemy crafting service with failure and quality"
```

---

### Task 13: Печь и окно алхимии

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyStation.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/UI/AlchemyPanelUI.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs` (создание панели)
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationConfig.cs` (ссылки на алхимию)
- Modify: `Assets/Editor/CultivationWorldSetup.cs` (ассеты алхимии)
- Create: `Assets/Resources/Cultivation/AlchemyConfig.asset` (Editor-сетап)

**Interfaces:**
- Consumes: `AlchemyService`, `AlchemyRecipe`, `Inventory`, `CultivationInput`.
- Produces:
  - `AlchemyStation.Recipes` (`IReadOnlyList<AlchemyRecipe>`), `AlchemyStation.Config`,
    `bool PlayerInside`
  - `AlchemyPanelUI.Build(AlchemyService, AlchemySkill, Inventory, CultivationSystem,
    DaoSystem, IReadOnlyList<AlchemyRecipe>, Transform)`,
    `.OpenForStation(AlchemyStation)`, `.OpenPortable()`, `.Close()`, `.IsOpen`

- [ ] **Step 1: Создать `AlchemyStation`**

Триггер-радиус, подсказка «E — алхимия» (procedural UI или world-space текст),
список рецептов, ссылка на `AlchemyConfig`. По `E` при `PlayerInside` открывает
`AlchemyPanelUI.OpenForStation(this)`. Не открывает, если панель уже открыта.

- [ ] **Step 2: Создать `AlchemyPanelUI`**

Левая колонка — список рецептов; правая — детали: ингредиенты «есть/нужно»,
требуемый грейд, шанс, шансы качеств, кнопка «Варить», журнал результата.
Режим печи показывает все рецепты, режим поля — только `PortableAllowed`.
Кнопка блокируется, если `CanCraft` вернул `false`, и показывает причину.
Двойной клик защищён флагом `craftingInProgress`.

- [ ] **Step 3: Подключить в bootstrap**

Создать ассеты `AlchemyConfig` и пять рецептов из раздела 8.3 спеки методом
`CultivationWorldSetup.EnsureAlchemy()` (вызывается из пункта меню) под
`Assets/Resources/Cultivation/`. Добавить в `CultivationConfig` поля
`[SerializeField] private AlchemyConfig alchemyConfig;`,
`[SerializeField] private List<AlchemyRecipe> alchemyRecipes;` и свойства
`AlchemyConfig`, `AlchemyRecipes`.
Создать панель под `Canvas`, сохранить в `CultivationUIRoot.Alchemy`.

- [ ] **Step 4: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Ручная проверка**

Поставить печь в сцену, подойти, нажать E: открывается полный список; успешный
крафт выдаёт пилюлю; провал съедает часть ингредиентов; двойной клик не дублирует.
Из походного режима видны только 2 рецепта и шанс ниже.

- [ ] **Step 6: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/Alchemy/AlchemyStation.cs Assets/Scripts/Cultivation/Runtime/UI/AlchemyPanelUI.cs Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs
git commit -m "feat: add alchemy station and crafting UI"
```

---

### Task 14: Пассивные скиллы школ

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/Passives/PassiveEffectKind.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Passives/PassiveModifier.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/Passives/PassiveSkillService.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/TechniqueData.cs` (поля пассива)
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationDefaults.cs` (10 пассивов)
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationStateCache.cs` (поля алхимии)
- Modify: `Assets/Scripts/Combat/PlayerHealth.cs` (множитель макс. HP)
- Modify: `Assets/Scripts/Cultivation/Runtime/QiComponent.cs` (множители Ци)
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationSystem.cs` (множитель Прозрения)
- Modify: `Assets/Scripts/Combat/AbilitySystem.cs` (`DamageMultiplier`)

**Interfaces:**
- Consumes: `DaoSystem.OnTechniqueLearned`, `OnDaoLevelChanged`,
  `CultivationSystem.OnRealmChanged`/`OnStageChanged`, `TechniqueData.Kind`.
- Produces:
  - `enum PassiveEffectKind { MaxHealthPercent, QiRegenPercent, MaxQiPercent,
    SpellPowerPercent, MeleeDamagePercent, DamageReductionPercent,
    ComprehensionGainPercent, AlchemySuccessPercent }`
  - `struct PassiveModifier { PassiveEffectKind Kind; float Value; TechniqueData Source; }`
  - `PassiveSkillService.GetMultiplier(PassiveEffectKind) -> float`,
    `.ActivePassives`, `.OnModifiersChanged`
  - `AbilitySystem.DamageMultiplier` (`float`, get/set)

- [ ] **Step 1: Создать типы**

`PassiveEffectKind`, `PassiveModifier` — как в Interfaces.

- [ ] **Step 2: Расширить `TechniqueData` и контент**

Поля `passiveEffect` и `passiveValue` (0 = нет эффекта). В `CultivationDefaults`
добавить 10 пассивов из таблицы 7.6 спеки с `Kind = Passive`, корректными
`RequiredDaoLevel`/`RequiredRealmIndex` и уникальными `Id`.

- [ ] **Step 3: Создать `PassiveSkillService`**

Собирает выученные пассивы, у которых требования выполнены. Возвращает
произведение `1 + value` для каждого вида эффекта. Пересчёт — по событиям, не в
`Update`.

- [ ] **Step 4: Подключить множители**

`PlayerHealth` применяет `MaxHealthPercent`; `QiComponent` — `MaxQiPercent`
через `SetMaxQi(value, keepPercent: true)` (не `Configure`, чтобы не сбрасывать
текущее Ци) и `QiRegenPercent` через `RegenPerSecond`; `CultivationSystem` — `ComprehensionGainPercent`
при начислении Прозрения; `AlchemyService` — `AlchemySuccessPercent` к шансу;
`AbilitySystem` — `DamageMultiplier` в расчёте урона.

- [ ] **Step 5: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 6: Ручная проверка**

Выучить «Закалку тела»: максимальное HP выросло. Выучить «Внутреннее пламя»:
урон техники вырос (проверить по логу/цифрам). Снять/потерять пассив — множители
вернулись.

- [ ] **Step 7: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/Passives Assets/Scripts/Cultivation/Runtime/TechniqueData.cs Assets/Scripts/Cultivation/Runtime/CultivationDefaults.cs Assets/Scripts/Combat/AbilitySystem.cs
git commit -m "feat: add school passive skills and stat modifiers"
```

---

### Task 15: Экран списка скиллов

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/UI/SkillListUI.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs`

**Interfaces:**
- Consumes: `DaoSystem.GetAllTechniques`, `HasLearnedTechnique`,
  `IsTechniqueAvailable`, `CultivationSystem.CurrentRealmIndex`,
  `PassiveSkillService.ActivePassives`, `CultivationAbilityBridge`.
- Produces: `SkillListUI.Build(DaoSystem, CultivationSystem, PassiveSkillService,
  CultivationAbilityBridge, Transform)`, `.Open()`, `.Close()`, `.IsOpen`,
  `.SelectTab(int)`.

- [ ] **Step 1: Создать `SkillListUI`**

Две вкладки: «Техники» и «Пассивные». Строка: иконка, имя, школа, статус
(«Открыто», «Нужен уровень Дао X», «Нужен реалм Y»), описание. Для активных
техник — Ци и КД, кнопка «Назначить» (через существующий мост). Для пассивов —
эффект («+10% урона оружием»). Прокрутка — `ScrollRect`, объекты переиспользуются.

- [ ] **Step 2: Подключить в `CultivationUIRoot`**

Свойство `Skills`, создание в `Create`, обновление при `OnModifiersChanged` и
`OnTechniqueLearned`, если панель открыта.

- [ ] **Step 3: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 4: Ручная проверка**

Открыть панель: обе вкладки заполнены, статусы соответствуют прогрессу,
назначение техники меняет слот, пассив показывает эффект и подсвечен как открытый.

- [ ] **Step 5: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/UI/SkillListUI.cs Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs
git commit -m "feat: add skill list UI"
```

---

### Task 16: Единое Esc-меню

**Files:**
- Create: `Assets/Scripts/UI/GameMenuController.cs`
- Modify: `Assets/UiManager.cs` (делегирование Esc)
- Modify: `Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs` (регистрация панелей)
- Modify: `Assets/Inventary/Script/UI/InventoryPanelUI.cs` (регистрация панели)
- Modify: `Assets/Editor/CultivationWorldSetup.cs` (кнопки в `PauseMenu`)

**Interfaces:**
- Consumes: `UiManager.OpenUI/CloseUI`, `InventoryPanelUI`, `CultivationUI`,
  `SkillListUI`, `AlchemyPanelUI`.
- Produces:
  - `GameMenuController.Instance`
  - `.IsAnyPanelOpen`, `.RegisterPanel(GameObject)`, `.OpenPanel(GameObject)`,
    `.CloseTopPanel()`, `.ToggleMenu()`

- [ ] **Step 1: Создать `GameMenuController`**

Стек открытых панелей. Esc: если стек не пуст — закрыть верхнюю; иначе
`ToggleMenu()` (открыть/закрыть `PauseMenu` через `UiManager`). При открытии меню —
`timeScale = 0`, курсор свободный; при закрытии — `timeScale = 1`, игрок включён.
Экран медитации в стек не входит и паузу не ставит.

- [ ] **Step 2: Делегировать Esc из `UiManager`**

`UiManager.Update` больше не реагирует на Esc напрямую; вызывает
`GameMenuController.ToggleMenu()` (или ничего, если контроллера нет — обратная
совместимость).

Панели вызывают `GameMenuController.Instance?.RegisterPanel(gameObject)` при
сборке. `MeditationScreenUI` в стек не входит: он сам обрабатывает Esc и паузу не
ставит.

- [ ] **Step 3: Добавить кнопки сетапом**

В панель `PauseMenu/Кнопки` добавить «Культивация», «Техники», «Крафт», «Журнал».
Кнопки ведут в `GameMenuController`. «Карта» уже есть — оставить как заглушку.
Повторный запуск сетапа не дублирует кнопки (поиск по имени).

- [ ] **Step 4: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Ручная проверка**

Esc открывает меню, все кнопки открывают свои панели; Esc закрывает верхнюю
панель, затем меню; после закрытия игра идёт, игрок двигается, курсор рабочий.
Открытие меню во время медитации ставит паузу и не начисляет дебаф.

- [ ] **Step 6: Коммит**

```bash
git add Assets/Scripts/UI/GameMenuController.cs Assets/UiManager.cs Assets/Editor/CultivationWorldSetup.cs
git commit -m "feat: add unified escape menu"
```

---

### Task 17: Левая полоса Дао и экран медитации

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/UI/DaoHudBarUI.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/UI/MeditationScreenUI.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs`

**Interfaces:**
- Consumes: `DaoSystem`, `CultivationSystem`, `MeditationController`,
  `CultivationDebuffController`, `MeditationRiskConfig`.
- Produces: `DaoHudBarUI.Build(DaoSystem, CultivationSystem, MeditationScreenUI, Transform)`,
  `MeditationScreenUI.Build(...)`, `.Open()`, `.Close()`, `.IsOpen`.

- [ ] **Step 1: Создать `DaoHudBarUI`**

Вертикальная полоса у левого края: цвет активного Дао, название школы, уровень,
заполнение очков до следующего уровня, 5 узлов (открыт/закрыт). Клик открывает
`MeditationScreenUI`.

- [ ] **Step 2: Создать `MeditationScreenUI`**

Показывает реалм/ступень/опыт/Прозрение, индикатор «Безопасное место»/«Опасная
зона», накопления сессии и множитель, активные дебафы, кнопку
«Медитировать»/«Прервать». Паузу не ставит. Блокирует кнопку при
`IsMeditationBlocked` и показывает причину. Esc закрывает экран, не открывая меню.

- [ ] **Step 3: Подключить в `CultivationUIRoot`**

Свойства `DaoBar`, `MeditationScreen`; создание в `Create`; `DaoBar` подписан на
смену Дао и уровня.

- [ ] **Step 4: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Ручная проверка**

Полоса видна слева, клик открывает экран, медитация запускается оттуда; в зоне
индикатор зелёный, вне зоны — красный; прерывание обновляет дебафы и опыт.

- [ ] **Step 6: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/UI/DaoHudBarUI.cs Assets/Scripts/Cultivation/Runtime/UI/MeditationScreenUI.cs Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs
git commit -m "feat: add dao hud bar and meditation screen"
```

---

### Task 18: VFX культивации

**Files:**
- Create: `Assets/Scripts/Cultivation/Runtime/VFX/CultivationVfxConfig.cs`
- Create: `Assets/Scripts/Cultivation/Runtime/VFX/CultivationVfxBridge.cs`
- Modify: `Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs` (подключение)

**Interfaces:**
- Consumes: `CultivationVfxKind`, `MeditationController.OnMeditationInterrupted`,
  `BreakthroughSequence.OnBreakthroughResult`, `CultivationDebuffController.OnDebuffApplied`.
- Produces: `CultivationVfxConfig` со ссылками на префабы;
  `CultivationVfxBridge.Play(CultivationVfxKind, Transform)` и
  `PlayBreakthrough(bool success, Transform)`.

- [ ] **Step 1: Создать `CultivationVfxConfig`**

Поля: `auraPrefab`, `meditationLoopPrefab`, `breakthroughStartPrefab`,
`breakthroughSuccessPrefab`, `breakthroughFailPrefab`, `qiSurgePrefab`,
`qiDeviationPrefab`, `severingPrefab`, `deathPrefab`, `effectLifetime = 4f`.
`ScriptableObject`, меню `Cultivation/Vfx Config`. Добавить в `CultivationConfig`
поле `[SerializeField] private CultivationVfxConfig vfxConfig;` и свойство
`public CultivationVfxConfig VfxConfig => vfxConfig;`.

- [ ] **Step 2: Создать `CultivationVfxBridge`**

Подписывается на события, инстансит префаб в позиции игрока, уничтожает через
`effectLifetime`. Отсутствующий префаб — предупреждение один раз на вид, не спам.
Экранная виньетка для `Severing` — `Image` под `Canvas` с плавным alpha.

- [ ] **Step 3: Подключить в bootstrap**

Создать мост на игроке, передать конфиг, назначить `DaoAuraController.auraPrefab`.

- [ ] **Step 4: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Ручная проверка**

Прорыв успешный и провальный дают разные эффекты; обрыв культивации — тёмный
эффект и виньетку; аура соответствует активному Дао; в консоли нет ошибок
отсутствующих материалов.

- [ ] **Step 6: Коммит**

```bash
git add Assets/Scripts/Cultivation/Runtime/VFX Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs
git commit -m "feat: add cultivation vfx bridge"
```

---

### Task 19: Сетап сцены, документация и приёмка

**Files:**
- Modify: `Assets/Editor/CultivationWorldSetup.cs` (сцена, компоненты, UI, станция)
- Modify: `Assets/Scripts/Cultivation/README.md`
- Modify: `Assets/Inventary/README.md`

**Interfaces:**
- Consumes: всё, что создано в Task 1–18.
- Produces: готовая сцена `Game.unity` после запуска меню.

- [ ] **Step 1: Дописать сетап сцены**

`EnsureWorldObjects()`:
- `EnemyAI` на `Enemy`, `AnimalAI` на префабах животных, 3–4 животных на плоскости;
- печь `SFWC2_Crafting_Table` рядом с `Smithy` + `AlchemyStation`;
- `DaoAltar` как точка респавна (создать, если нет);
- на игрока: `PlayerHealth`, `PlayerRespawnService`, `CultivationDebuffController`,
  `AlchemySkill`, `PassiveSkillService`, `EnemyAI`-независимые ссылки;
- UI: `PlayerVitalsUI`, `DaoHudBarUI`, `MeditationScreenUI`, `SkillListUI`,
  `AlchemyPanelUI`, `DebuffPanelUI`, `GameMenuController`;
- назначить в `CultivationConfig` уже созданные `MeditationRiskConfig`,
  `AlchemyConfig`, `CultivationVfxConfig` и рецепты;
- сохранение сцены и ассетов, отчёт в консоль.

Идемпотентность: поиск по имени и компоненту, обновление полей, без дублей.

- [ ] **Step 2: Сборка**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 3: Запустить сетап**

Run: `Tools → Cultivation → 4. Setup World Content (Game)` в Unity.
Expected: сцена сохранена, в консоли нет ошибок, повторный запуск не создаёт
дубликатов.

- [ ] **Step 4: Обновить README**

`Assets/Scripts/Cultivation/README.md`: разделы про здоровье/смерть, медитацию и
три стадии, добычу, алхимию, пассивки, Esc-меню, VFX, сохранение v2.
`Assets/Inventary/README.md`: раздел про `LootTable` и дроп с существующих объектов.

- [ ] **Step 5: Пройти чеклист приёмки**

Все 18 пунктов из раздела 12 спеки. Каждый пункт — либо «ок», либо баг с
описанием. Не оставлять незакрытых пунктов.

- [ ] **Step 6: Финальная сборка и коммит**

```bash
dotnet build Assembly-CSharp-Editor.csproj -v q --nologo
git add Assets/Editor/CultivationWorldSetup.cs Assets/Scripts/Cultivation/README.md Assets/Inventary/README.md Assets/Scenes/Game.unity
git commit -m "feat: wire cultivation, loot and alchemy into the game scene"
```

---

## Приложение: карта новых файлов

| Папка | Файлы |
|---|---|
| `Assets/Scripts/Combat` | `DamageInfo`, `PlayerHealth`, `PlayerRespawnService` |
| `Assets/Scripts/Enemy` | `EnemyAI`, `AnimalAI` |
| `Assets/Scripts/Loot` | `LootRoll`, `LootTable` |
| `Assets/Scripts/UI` | `GameMenuController` |
| `Assets/Scripts/Cultivation/Runtime/Meditation` | `CultivationVfxKind`, `CultivationFailureStage`, `MeditationRiskConfig`, `MeditationSession`, `ActiveCultivationDebuff`, `CultivationDebuffController`, `MeditationInterruptionReason`, `MeditationConsequenceService` |
| `Assets/Scripts/Cultivation/Runtime/Alchemy` | `AlchemyGrade`, `AlchemyConfig`, `AlchemyIngredient`, `AlchemyRecipe`, `AlchemySkill`, `PillQuality`, `AlchemyResult`, `AlchemyService`, `AlchemyStation` |
| `Assets/Scripts/Cultivation/Runtime/Passives` | `PassiveEffectKind`, `PassiveModifier`, `PassiveSkillService` |
| `Assets/Scripts/Cultivation/Runtime/UI` | `PlayerVitalsUI`, `DebuffPanelUI`, `DaoHudBarUI`, `MeditationScreenUI`, `SkillListUI`, `AlchemyPanelUI` |
| `Assets/Scripts/Cultivation/Runtime/VFX` | `CultivationVfxConfig`, `CultivationVfxBridge` |
| `Assets/Editor` | `CultivationWorldSetup` |
| `Assets/Inventary/Data/Content` | ресурсы и пилюли (Editor) |
| `Assets/Inventary/Data/Loot` | таблицы лута (Editor) |
