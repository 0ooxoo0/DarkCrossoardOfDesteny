# Аудит префаба врага `Assets/Enemies/Prefabs/Enemy_Base.prefab`

Дата: 2026-11-27. Метод: разбор YAML префабов/сцены и чтение рантайм-скриптов. Play-тест не запускался.

---

## 0. Главный вывод

`Enemy_Base.prefab` — **мёртвая ветка**. Он нигде не используется:

* нет ни одной ссылки на его guid `c64292f4cef711e498daf24bec8aa099` ни в одной сцене и ни в одном префабе;
* в игре (`Assets/Scenes/Game.unity`) работает другой префаб — `Assets/Prefabs/Enemies/EnemyCombat.prefab` (guid `f2ca7f8bf6c179b4a83f8d009fa91c42`), на него смотрит `EnemySpawner` (`Game.unity:59736`);
* из `EnemyCombatAI.attackHitbox` в `Enemy_Base.prefab` стоит `{fileID: 0}` (сцена 234) — ссылки на хитбокс нет вообще;
* `EnemyController` (скрипт, который собирает модель и применяет сложность) тоже стоит **только** на `Enemy_Base.prefab` и больше нигде.

Значит:

| Претензия | `Enemy_Base` (мёртвый) | `EnemyCombat` (живой, в игре) | Кто реально виноват |
|---|---|---|---|
| Нет анимаций | да, на 100% | нет, аниматор есть | `Enemy_Base` |
| Не бьёт | не может в принципе | бьёт, но с двумя багами | `Enemy_Base` + 2 бага в `CombatHitbox` |
| Нет сложности | заготовка есть (`DifficultyConfig`) | сложность есть, но хардкодом `intelligence` | обе ветки, по-разному |

Дальше — по каждому пункту с доказательствами.

---

## 1. «У врага нет анимаций»

### Причина (цепочка)

```
EnemyController.SetVisual()                       Assets/Enemies/Scripts/EnemyController.cs:88
  currentAnimator = currentVisual.GetComponentInChildren<Animator>();   // Animator ЕСТЬ
  if (currentAnimator != null && data.animatorController != null)       // ← здесь null
      currentAnimator.runtimeAnimatorController = data.animatorController;

EnemyVisual_*.asset  (все 8 штук)  ->  animatorController: {fileID: 0}  // ПУСТО
Модели SM_Chr_*.prefab             ->  m_Controller: {fileID: 0}        // ПУСТО
```

Все 8 ассетов `Assets/Enemies/Data/EnemyVisual_*.asset` имеют `animatorController: {fileID: 0}`:
Skeleton (id 0), HeavyArmor (1), Ranger (2), Gargoyle (3), DarkLord (4), Demon (5), LightArmor (6), Flesh (7).

Все модели `Assets/Assets/DarkFantasy/PolygonDarkFantasy/Prefabs/Characters/SM_Chr_*.prefab` имеют `Animator` с `m_Controller: {fileID: 0}` (аватар выставлен: `Characters.fbx` / `BigCharacters.fbx`, тип Humanoid).

**Итог: контроллер аниматора не назначается ниоткуда.** Единственный существующий вражий контроллер — `Assets/Animations/Combat/EnemyCombat.controller` (guid `5c4e66875be35b540b60bcf4e79a74d9`) — подключается **только** в `EnemyCombat.prefab` как override `m_Controller` на вложенной модели (это единственная ссылка на его guid во всём проекте).

Даже если бы контроллер был назначен: `SM_Chr_*.prefab` имеют `m_ApplyRootMotion: 1`, а `EnemyCombatAI` двигает врага через `NavMeshAgent` и `transform.position` (`MoveTo`). При Humanoid-аватаре с включённым root motion это даёт рывки/борьбу за позицию. В `EnemyCombat.prefab` это уже лечат override'ом `m_ApplyRootMotion: 0` — в `Enemy_Base` такой настройки нет.

### Что уже есть в проекте (не нужно создавать)

`EnemyCombat.controller` полностью покрывает все параметры, которые дёргает `EnemyCombatAI`:

| Параметр | Тип | Где ставится |
|---|---|---|
| `Speed` | float | `EnemyCombatAI.Update` (:212) |
| `Attack` | trigger | `StartAttack` (:367) |
| `Combo` | trigger | `AdvanceCombo` (:381) |
| `AttackIndex` | int | 0 / 1 / 2 |
| `IsBlocking` | bool | `EnterBlock` / `SetBlocking` |
| `BlockHit` | trigger | `OnBlocked` (:484) |
| `Hit` | trigger | `NotifyDamaged` (:498) |
| `IsDead` | bool | `NotifyDeath` (:515) |

Состояния: `Locomotion` (blend-tree: Idle / Combat Walk Fwd / Run, пороги 0 / 0.45 / 1), `Attack 1 (Jab)`, `Attack 2 (Cross)`, `Attack 3 (Hook)`, `Hit Reaction`, `Block`, `Block Hit`, `Knock Down (Death)`, `Lying Down`. На атаках висит `CombatAnimationWindow` (`hitboxStart 0.18 / hitboxEnd 0.42 / comboWindowStart 0.45 / comboWindowEnd 0.8`).

Клипы — Humanoid из Fantacode (`Left Jab.fbx`, `Right Cross.fbx`, `Left Hook.fbx`, `Block.fbx`, `Block Hit.fbx`, `Left Hit.fbx`, `Knock Down Back.fbx`, `Lying Down.fbx`, `Combat Idle/Walk`, `Run`). Совместимы с аватаром PolySplit (обе пачки `animationType: 3` = Humanoid).

---

## 2. «Враг не бьёт»

В живом `EnemyCombat.prefab` цепочка удара **собрана корректно**:

```
EnemyCombatAI.attackHitbox -> 4368626716039861873  (CombatHitbox, Assembly-CSharp::CombatHitbox)
CombatHitbox.ownerRoot     -> 670779377495465326   (корень префаба)  ✔
CombatHitbox.team          -> 2                    (Enemy)            ✔
Animator.m_Controller      -> EnemyCombat.controller                  ✔
EnemyCombatAI.telegraph    -> 1859528030230638988  (EnemyAttackTelegraph) ✔
EnemyCombatAI.aimator/agent/health -> заполнены                       ✔
```

То есть «не бьёт» в игре объясняется не сборкой, а двумя дефектами в коде:

### 2a. Хитбокс не бьёт игрока, пока у него нет `IDamageable` (`Assets/Scripts/CombatSystem/CombatHitbox.cs:115`)

```csharp
IDamageable target = other.GetComponentInParent<IDamageable>();
if (target == null || target.IsDead) return;
```

`CombatHitbox.FixedUpdate` делает `Physics.OverlapBoxNonAlloc(... QueryTriggerInteraction.Collide)` по `targetLayers: ~0`. Первым в буфер почти наверняка попадает `CapsuleCollider` **самого врага** (`EnemyCombat.prefab`, `m_IsTrigger: 0`, layer 0, экземпляр `8878601340758536882`) — своя цель отсеивается проверкой `targetRoot == myRoot`, но буфер на 32 элемента может быть забит другим. Главное: **если у игрока нет `IDamageable` в родителях коллайдера — урон не пройдёт вообще**, и в лог ничего не напишется (тихий `return`).

Проверить обязательно: корень `Player.prefab` (guid `515408867bd7d844691cdde1e3d9b603`) содержит `DarkCrossroad.Runtime::PlayerHealth` (компонент `3106896249760669837`) и тег `Player`. Хитбокс врага дотянется до него **только** если у игрока включён хотя бы один коллайдер ниже/на корне.

⚠️ **Здесь красный флаг.** Единственный физический коллайдер, объявленный прямо в `Player.prefab`, — это `BoxCollider` (`3217953594279070232`) на ребёнке `Cube` (GO `5854201483630845358`) с `isTrigger: 1` и `m_IsActive: 0`. В сцене `Game.unity` этот `Cube` **принудительно выключен**:

```
- target: {fileID: 5854201485358..., guid: 515408867bd7d844691cdde1e3d9b603}
  propertyPath: m_IsActive      value: 0
- target: ... propertyPath: m_TagString  value: DammageObject
- target: ... propertyPath: m_Layer      value: 0
```

Плюс в `m_RemovedGameObjects` инстанса игрока лежит удалённый объект `{fileID: 1181507340331625043}`. Если это был hurtbox — врагу физически **нечего** бить. Это надо смотреть глазами в сцене (Play → выделить игрока → проверить, есть ли активный нетриггерный коллайдер на слое игрока), потому что часть вложений приходит из вложенного FBX-префаба.

Настоящий хитбокс игрока, судя по коду, — временный триггер от `Assets/Scripts/Combat/AbilitySystem.cs` + `AttackHitbox.cs` (абилки/оружие по тегу `DammageObject`), а не постоянный коллайдер. Урон врага на такой хитбокс не рассчитан.

### 2b. Тайминги окон завязаны на нормализованное время с двумя проходами (`Assets/Scripts/CombatSystem/CombatAnimationWindow.cs:38-49`)

```csharp
float t = Mathf.Repeat(stateInfo.normalizedTime, 1f);
if (!hitboxOpen && t >= hitboxStart && t <= hitboxEnd) { hitboxOpen = true; receiver.OnAttackWindowOpen(); }
else if (hitboxOpen && t > hitboxEnd) { hitboxOpen = false; receiver.OnAttackWindowClose(); }
```

`OnStateExit` закрывает окно, поэтому повторное открытие на втором проходе не сломает состояние. Но: `CombatHitbox.SetActive(false)` **отключает коллайдер**, а `FixedUpdate`-фолбэк работает только пока `active == true`. При `hitboxStart 0.18 … hitboxEnd 0.42` окно открыто ~0.24 нормализованного времени клипа; при быстром джебе это меньше одного физического тика на 50 Гц. Дополнительно `CombatHitbox` живёт на **корне префаба у ног** (`AttackHitbox` локально `y = 1, z = 1.1`) и бьёт по `OverlapBox` фиксированного размера — попадание зависит от дистанции сильнее, чем от анимации.

### 2c. `Enemy_Base` не бьёт по другой причине — хитбокс не привязан

В `Enemy_Base.prefab`:

```
EnemyCombatAI.attackHitbox: {fileID: 0}     ← ссылки нет
CombatHitbox.team: 2, damage: 12            ← сам хитбокс настроен
EnemyCombatAI.animator: {fileID: 0}         ← animator тоже не привязан
```

`StartAttack()` при `attackHitbox == null` молча ничего не делает по урону (`:357`), а окна `OnAttackWindowOpen` некому открывать — контроллера-то нет. Полный ноль урона.

---

## 3. «Сложность врага 1..4»

### Что уже есть в коде

`EnemyCombatAI` умеет ровно то, что просят, но **всё захардкожено в `switch` по `intelligence`** (`:81-154`):

| intelligence | Damage × | Speed × | Cooldown × | ReactionDelay | ComboChance | BlockChance | Особое |
|---|---|---|---|---|---|---|---|
| 1 | 0.70 | 0.80 | 1.35 | 0.80 | 0.00 | 0.00 | не блокирует (`intelligence >= 2` в `:251`) |
| 2 | 1.00 | 1.00 | 1.00 | 0.50 | 0.25 | 0.25 | блокирует |
| 3 | 1.20 | 1.15 | 0.80 | 0.30 | 0.55 | 0.55 | блокирует + комбо |
| 4 | 1.45 | 1.30 | 0.60 | 0.15 | 0.80 | 0.80 | **контратака после блока** (`OnBlocked`, `:487`) |
| 5 | 1.90 | 1.55 | 0.35 | 0.02 | 1.00 | 1.00 | «no chance» |

То есть запрошенная лестница (1 редко бьёт → 2 защищается → 3 часто бьёт и защищается → 4 ещё и парирует) **уже реализована** в `EnemyCombatAI`. Парирование = блок + мгновенная контратака в `OnBlocked`:

```csharp
public void OnBlocked(DamageInfo info)          // EnemyCombatAI.cs:482
{
    if (animator != null) animator.SetTrigger(blockHitTrigger);
    blockUntil = Time.time + 0.4f;
    if (intelligence >= 4) { nextAttackTime = 0f; StartAttack(); }   // ← парирование
}
```

Но `blockChance` считается **один раз в `Awake`** (`blockChance = GetBlockChance();`, `:165`) — смена `intelligence` в рантайме не меняет шанс блока. Это баг для «сложности на лету».

### Чего не хватает под требование

1. **Нет внешнего API сложности.** `intelligence` — `[SerializeField] private`, публичного сеттера нет. Снаружи его меняют только рефлексией: `EnemyController.SetDifficulty` (`Assets/Enemies/Scripts/EnemyController.cs:113-116`) через `SetField(ai, "intelligence", config.intelligence)`. Сам `EnemyController` висит только на мёртвом `Enemy_Base`.
2. **Порядок в каталоге сложностей перепутан.** В `Enemy_Base.prefab` (сцена 264-268) список идёт Easy, Normal, Hard, Nightmare, а `currentDifficultyIndex: 1` = **Normal**. Само по себе ок, но `DifficultyConfig.intelligence` = 1/2/3/4 = Easy/Normal/Hard/Nightmare, и это единственная связь с лестницей 1..4. Автор префаба, судя по баг-репорту, ожидал «1 = Easy».
3. **`DifficultyConfig` не несёт боевых параметров сложности**: только `healthMultiplier`, `damageMultiplier`, `speedMultiplier`, `attackCooldownMultiplier`, `intelligence`. Нет полей `reactionDelay`, `comboChance`, `blockChance`, `parryChance`, `maxCombo`, `blockDuration`. Всё это живёт в `switch` и не тюнится из инспектора.
4. **Ноль документации в инспекторе**: `[Header("Intelligence (1 = weak, 4 = strong, 5 = no chance)")]` — тултипа с расшифровкой «1 редко бьёт / 2 блокирует / …» нет.
5. **`EnemyCombat.prefab` в сцене игроку подсовывает сложность override'ами**: `intelligence: 4`, `attackDamage: 12.4`, `maxHealth: 200` (`Game.unity:11884-11899`, объект `EnemyCombat_Strong_2`). То есть «сложность» в игре сейчас = ручные override'ы на расставленных вручную экземплярах, а не система.

---

## 4. Прочие дефекты, найденные попутно

1. **`Enemy_Base.prefab` не имеет `EnemyAttackTelegraph`, `EnemyHitReaction`, `EnemyHealthBar`.** В живом `EnemyCombat.prefab` они есть. То есть у мёртвого префаба нет даже телеграфа замаха, без которого блок игрока — угадайка (прямая цитата из `EnemyAttackTelegraph.cs:6`).
2. **Позиция корня `Enemy_Base` — `y = -0.71037`** (сцена 109) — «утоплен» в землю, тогда как `EnemyCombat` стоит на `y = 0`.
3. **`ReactionDelay` не влияет на блок.** В `:251-259` `nextBlockDecision = Time.time + ReactionDelay;` — то есть задержка задаёт лишь частоту перепроверки, а не задержку реакции на замах. Игрок успевает ударить до того, как враг «подумает».
4. **`maxCombo` — `[Range(1, 3)]`, а `AttackIndex` в контроллере размечен на 0/1/2** — согласовано, но 3-й удар (Hook) доступен только с `maxCombo: 3`, а в обоих префабах стоит `2`. Третья анимация атаки в контроллере простаивает.
5. **`intelligence >= 4` = парирование наступает в `OnBlocked` мгновенно и безусловно**, без шанса и без кулдауна: игрок бьёт в блок — всегда получает контрудар. На 5 (`default`) это превращается в бесконечную петлю «удар-блок-контратака».
6. **`EnemyController` использует рефлексию** для записи `private` полей (`SetField`, `:211`) — помечено `ponytail:`-комментарием в коде. Работает, но хрупко: переименование поля в `EnemyHealth`/`EnemyCombatAI` тихо ломает сложность без ошибки компиляции.
7. **`EnemyVisualData` id 0..7 без пропусков, но порядок в `availableVisuals` префаба (сцена 256-263) — по guid'ам, и id 1 = HeavyArmor** — при добавлении новых ассетов порядок массива не совпадает с id, `FindVisual` ищет по id корректно, но человек в инспекторе видит неотсортированный список.
8. **Legacy-ветка**: `Assets/Scripts/Enemy/EnemyAI.cs` помечен `[System.Obsolete]`, файл в кодировке, где русские комментарии читаются как `РџСЂРѕСЃС‚РѕР№` (кодировка сломана). На игру не влияет (класс не используется), но при правке даст кашу.

---

## 5. Сводка «что именно сломано, где и как чинить»

| № | Симптом | Файл:строка | Причина | Правка |
|---|---|---|---|---|
| 1 | Нет анимаций | `Assets/Enemies/Scripts/EnemyController.cs:88-90` + все `Assets/Enemies/Data/EnemyVisual_*.asset` | `animatorController: {fileID: 0}` и `m_Controller: {fileID: 0}` у моделей | назначить `Assets/Animations/Combat/EnemyCombat.controller` в каждый `EnemyVisual_*`; либо как fallback в коде |
| 2 | Нет root motion контроля | `SM_Chr_*.prefab` (`m_ApplyRootMotion: 1`) | Humanoid-клипы тянут корень, а двигает `NavMeshAgent` | снять `Apply Root Motion` (в префабе-инстансе или кодом `animator.applyRootMotion = false`) |
| 3 | Враг не бьёт (мёртвый префаб) | `Assets/Enemies/Prefabs/Enemy_Base.prefab:234` | `attackHitbox: {fileID: 0}`, `animator: {fileID: 0}` | привязать, либо (правильнее) удалить префаб и работать с `EnemyCombat.prefab` |
| 4 | Враг не бьёт (живая сцена) | `Assets/Scripts/CombatSystem/CombatHitbox.cs:115` | тихий `return`, если у игрока нет `IDamageable`; у `Player.prefab` единственный бокс-коллайдер выключен (`Cube`, `m_IsActive: 0`) | гарантировать hurtbox игрока (активный коллайдер на корне с `PlayerHealth`) + `Debug.LogWarning` при промахе по причине «нет IDamageable» |
| 5 | Сложность не переключается в рантайме | `EnemyCombatAI.cs:165` | `blockChance` считается один раз в `Awake` | сделать свойством: `private float BlockChance => GetBlockChance();` |
| 6 | Сложность не тюнится | `EnemyCombatAI.cs:81-154`, `Assets/Enemies/Scripts/DifficultyConfig.cs:12-19` | множители захардкожены в `switch`, ассет несёт только 5 полей | вынести в `DifficultyConfig` поля `blockChance`, `comboChance`, `reactionDelay`, `parryChance`, `maxCombo`; `EnemyCombatAI.ApplyDifficulty(DifficultyConfig)` |
| 7 | Нет API сложности | `EnemyCombatAI` (поле `intelligence`) | `private`, сеттера нет | `public void SetIntelligence(int level)` + `public int IntelligenceLevel` |
| 8 | Парирование = бесконечная петля | `EnemyCombatAI.cs:487-491` | контратака без шанса и кулдауна | `parryChance` + `parryCooldown` (см. 6) |
| 9 | Реакция на замах не работает | `EnemyCombatAI.cs:251-259` | `ReactionDelay` влияет только на частоту опроса | запоминать время начала атаки игрока, входить в блок только если `Time.time - attackStart >= ReactionDelay` |

---

## 6. ПРОМТ ДЛЯ ИСПРАВЛЕНИЙ (копировать целиком)

> Ты работаешь в Unity-проекте `D:\.Programm\UnitySave\DarkCrossoardOfDesteny` (URP, Unity 6, сборка `DarkCrossroad.Runtime` в `Assets/Scripts/DarkCrossroad.Runtime.asmdef`, плюс код без asmdef в `Assembly-CSharp`).
>
> **Задача:** починить врага — анимации, нанесение урона и уровни сложности 1–4.
>
> **Контекст, который нельзя переоткрывать заново (проверено):**
> - Живой враг в игре — `Assets/Prefabs/Enemies/EnemyCombat.prefab` (guid `f2ca7f8bf6c179b4a83f8d009fa91c42`), на него ссылается `EnemySpawner` в `Assets/Scenes/Game.unity:59736`.
> - `Assets/Enemies/Prefabs/Enemy_Base.prefab` (guid `c64292f4cef711e498daf24bec8aa099`) **не используется нигде**: ни в сценах, ни в префабах. На нём висит скрипт `EnemyController`.
> - Готовый контроллер анимаций врага уже существует: `Assets/Animations/Combat/EnemyCombat.controller` (guid `5c4e66875be35b540b60bcf4e79a74d9`). Он уже подключён к `EnemyCombat.prefab`. Параметры: `Speed` (float), `Attack` (trigger), `Combo` (trigger), `AttackIndex` (int), `IsBlocking` (bool), `BlockHit` (trigger), `Hit` (trigger), `IsDead` (bool). Состояния: `Locomotion`, `Attack 1 (Jab)`, `Attack 2 (Cross)`, `Attack 3 (Hook)`, `Hit Reaction`, `Block`, `Block Hit`, `Knock Down (Death)`, `Lying Down`. Новые клипы и контроллеры **не создавать**.
> - Модели врагов: `Assets/Assets/DarkFantasy/PolygonDarkFantasy/Prefabs/Characters/SM_Chr_*.prefab`, Humanoid-аватар, `m_Controller: {fileID: 0}`.
> - Скрипты боя: `Assets/Scripts/CombatSystem/CombatHitbox.cs`, `EnemyCombatAI.cs`, `EnemyHealth.cs`, `CombatAnimationWindow.cs`, `EnemyAttackTelegraph.cs`.
>
> **Что сделать:**
>
> 1. **Анимации.**
>    1.1. Во всех 8 ассетах `Assets/Enemies/Data/EnemyVisual_*.asset` заполнить `animatorController` = `EnemyCombat.controller`. Сделай это редакторным скриптом (`AssetDatabase.LoadAssetAtPath` + `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`), а не руками.
>    1.2. В `EnemyController.SetVisual` (`Assets/Enemies/Scripts/EnemyController.cs:88-90`) добавь fallback: если `data.animatorController == null`, не оставлять `currentAnimator` пустым, а логировать `Debug.LogError` с именем ассета — тихий пропуск анимаций недопустим.
>    1.3. Гарантировать `currentAnimator.applyRootMotion = false` в `SetVisual` (движение делает `NavMeshAgent`, root motion даёт рывки).
>
> 2. **Урон.**
>    2.1. В `CombatHitbox.TryDamage` (`:115`) добавь ветку диагностики: если `target != null`, но `GetComponentInParent<IDamageable>() == null`, писать `Debug.LogWarning` не чаще раза в 2 секунды (статический таймстемп), с именем объекта. Сейчас промах абсолютно немой.
>    2.2. В `CombatHitbox.FixedUpdate` исключи из `OverlapBox` собственный корень: после `Configure(...)` запоминай слой владельца в `ownerLayers` автоматически (`ownerRoot.gameObject.layer`), чтобы буфер на 32 элемента не забивался своими же коллайдерами.
>    2.3. Проверить на сцене `Assets/Scenes/Game.unity`, что у игрока есть **включённый нетриггерный коллайдер** на корне `Player` (префаб `Assets/Player.prefab`, компонент `PlayerHealth` = `DarkCrossroad.Runtime::PlayerHealth`). Если нет — добавить на корень `Player` в `Player.prefab` `CapsuleCollider` (radius 0.4, height 2, center y=1, isTrigger=false, layer = слой игрока) и занести это в отчёт. Обоснуй изменение в комментарии.
>    2.4. В `Enemy_Base.prefab` привязать `EnemyCombatAI.attackHitbox` к дочернему `AttackHitbox` (сцена 234: `attackHitbox: {fileID: 0}`), `animator` — к аниматору модели, `health` — к `EnemyHealth`. Либо, если решаешь префаб удалить, скажи это прямо и объясни.
>
> 3. **Сложность 1–4 (`EnemyCombatAI`).** Приведи лестницу ровно к требованию заказчика:
>
>    | Ур. | Название | Поведение |
>    |---|---|---|
>    | 1 | Слабый | бьёт редко: `attackCooldown × 1.4`, `blockChance 0`, `comboChance 0`, `parryChance 0`, реакция 0.8 с |
>    | 2 | Обычный | защищается: `blockChance 0.4`, `comboChance 0.15`, `parryChance 0`, кулдаун × 1.0, реакция 0.5 с |
>    | 3 | Опытный | часто бьёт и защищается: `blockChance 0.6`, `comboChance 0.55`, `maxCombo 3`, кулдаун × 0.7, реакция 0.25 с, `parryChance 0` |
>    | 4 | Мастер | всё выше + парирует: `blockChance 0.8`, `comboChance 0.8`, `parryChance 0.5`, кулдаун × 0.55, реакция 0.12 с |
>
>    3.1. Вынеси эти числа в `Assets/Enemies/Scripts/DifficultyConfig.cs` полями: `blockChance`, `comboChance`, `parryChance`, `reactionDelay`, `maxCombo`, `blockDuration`, помимо существующих множителей. `switch`-и в `EnemyCombatAI` (`:81-154`) удали и замени чтением из конфига.
>    3.2. Добавь в `EnemyCombatAI`:
>       ```csharp
>       public void ApplyDifficulty(DifficultyConfig config);   // заполняет все боевые поля + вызывает agent.speed
>       public void SetIntelligence(int level);                 // 1..4, применяет пресет по уровню
>       public int IntelligenceLevel { get; }
>       ```
>       `SetIntelligence` должен работать в рантайме: пересчитывать `blockChance`, `comboChance`, `parryChance`, `attackCooldown`, `maxCombo`, `blockDuration` и `agent.speed` немедленно.
>    3.3. Убери ошибку `blockChance`-в-`Awake` (`:165`) — вычисляй как свойство, чтобы смена уровня влияла сразу.
>    3.4. **Парирование** сделай отдельным поведением, а не безусловной контратакой: в `OnBlocked` контратаковать только если `Random.value < parryChance` и `Time.time >= nextParryTime` (кулдаун = `attackCooldown`), иначе просто `blockHit` + продление блока. Сейчас `intelligence >= 4` контратакует всегда и без кулдауна — это бесконечная петля «удар-блок-контратака».
>    3.5. Добавь `[Tooltip]` с расшифровкой уровней на поле `intelligence`/`difficulty`.
>    3.6. В `EnemyCombat.prefab` выстави уровень по умолчанию **2** и добавь ссылку на пресет сложности в новый слот `DifficultyConfig difficulty`, чтобы уровень читался из ассета, а не из магического числа. Существующие override'ы в `Game.unity` (`intelligence: 4`, `attackDamage: 12.4`, `maxHealth: 200` на объекте `EnemyCombat_Strong_2`) не трогай — они остаются ручной настройкой конкретного экземпляра.
>
> 4. **Проверка.** После правок:
>    - `refresh_unity` + проверка отсутствия ошибок компиляции;
>    - один runnable-чек: маленький EditMode-тест или `Debug.LogAssertion`-самопроверка, что `ApplyDifficulty(config)` на пресете уровня 4 выставляет `parryChance > 0`, а на уровне 1 — `blockChance == 0`;
>    - отчёт: какие файлы изменены, какие ссылки заполнены в префабах, найден ли у игрока hurtbox.
>
> **Запрещено:** создавать новые анимационные клипы и контроллеры; переписывать `EnemyCombat.controller`; трогать `Assets/Unity Technologies/Fantacode Studios/**`; добавлять новые пакеты; удалять `Enemy_Base.prefab` без явного подтверждения.

---

## 7. Чек-лист приёмки

- [ ] В Play-режиме враг играет `Locomotion` при движении и `Attack 1/2/3` при ударе.
- [ ] `IsDead` переводит врага в `Knock Down (Death)` без ошибок в консоли.
- [ ] Удар врага снимает HP игрока (видно в `PlayerHealth`), попадание по стене урона не наносит (`requireLineOfSight`).
- [ ] Уровень 1: враг не блокирует ни разу за 60 секунд боя.
- [ ] Уровень 2: враг блокирует, но не контратакует.
- [ ] Уровень 3: интервал между ударами заметно короче, чем на 1; бывают серии из 3 ударов.
- [ ] Уровень 4: после блока враг иногда контратакует, но не каждый раз и не чаще раза в `attackCooldown`.
- [ ] `SetIntelligence(n)` из рантайма меняет поведение в течение одного кадра, без перезапуска сцены.
