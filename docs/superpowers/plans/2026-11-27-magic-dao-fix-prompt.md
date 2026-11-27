# Промт: магия летит в центр экрана + префабы `Assets/Magic` становятся скиллами древа Дао

> Скопируй весь текст ниже в агента-исполнителя (Claude Code / Codex / OpenCode).
> Промт самодостаточен: контекст, инварианты, срезы, приёмка.

---

## Роль и контекст

Ты работаешь в Unity-проекте `DarkCrossoardOfDesteny` (Unity 6000.2.10f1, URP 17.6.0, Input System,
жанр — культивация/уся, ПК, мышь + клавиатура). Проект на `D:\.Programm\UnitySave\DarkCrossoardOfDesteny`.

Уже есть: `AbilitySystem` (5 слотов, клавиши 1–5), система культивации (`DaoSystem`, `TechniqueData`,
`CultivationAbilityBridge`), боевой урон через `CombatHitbox`/`IDamageable`, 57 VFX-префабов
в `Assets/Magic` (AllIn1 Vfx Toolkit).

Игрок жалуется на две вещи:

1. **Магия не летит туда, куда смотрит центр экрана.** Сейчас она появляется невидимой сферой в 1.5 м
   перед персонажем, в направлении `transform.forward`. Луча из центра экрана нет вообще.
2. **Магия не настраиваемая.** У 25 техник `vfxPrefab` пустой, визуала нет; из 25 техник назначаются
   только 5 (`Active`); древа изучения нет — плоский список с гейтом по уровню Дао.

Полный аудит с цитатами: `docs/superpowers/specs/2026-11-27-magic-dao-audit.md` — **прочитай первым**.

## Что читать перед правками (не менять на этом шаге)

```
Assets/Scripts/Combat/AbilitySystem.cs              (578 строк; ключевое: 345-374, 463-513, 515-549)
Assets/Scripts/Combat/AbilityData.cs                (80)
Assets/Scripts/Cultivation/Runtime/TechniqueData.cs (106)
Assets/Scripts/Cultivation/Runtime/CultivationAbilityBridge.cs (361; ключевое: 69-82, 297-340)
Assets/Scripts/Cultivation/Runtime/DaoSystem.cs     (456; ключевое: 233-256, 370-411)
Assets/Scripts/Cultivation/Runtime/CultivationCastController.cs (263; ключевое: 201-213)
Assets/Scripts/Player/PlayerController.cs           (991; ключевое: 432-436, 975-990)
Assets/Scripts/CombatSystem/CombatHitbox.cs         (215; ключевое: 74-86, 111-169, 175-194)
Assets/Scripts/Cultivation/Runtime/CultivationStateCache.cs (194)
Assets/Scripts/Cultivation/Editor/CultivationSetup.cs (370)
Assets/Tests/EditMode/SkillSystemTests.cs           (196)
```

## Инварианты (не нарушать)

1. Не ломать инвентарь, экипировку, лут, оружие и его способности 1–3, хитбоксы, `DestructibleObject`,
   `PlayerController`, `CombatFeedback`, `PlayerHealth`.
2. Слоты 1–3 `AbilitySystem` остаются за оружием: техники Дао живут в 3–4 (`CultivationAbilityBridge.cs:99`,
   `AbilitySystem.cs:107-110`). Не менять это без отдельной задачи.
3. Не удалять `CultivationCastController`/`CultivationQuickBarUI` — это fallback для сцен без `AbilitySystem`
   (`CultivationBootstrap.cs:242-255`). Правки в них — только те же, что в основной ветке.
4. Сохранение: формат `Cultivation.Save.v2` (ключ в `CultivationStateCache.cs:45`) читается без падений.
   Переиспользуй существующие поля; новый формат — только если иначе нельзя, с миграцией.
5. Не добавлять новых художественных ассетов: иконки — процедурные заглушки (в UI уже есть фолбэк-руна,
   `TechniqueListUI.cs:246-252`).
6. Никаких сетевых допущений: мультиплеера в проекте нет (`Mirror|Netcode|FishNet|Photon` — 0 совпадений).
   Если появится — синхронизировать `aimPoint`/хит, а не позицию снаряда.
7. Работай **срезами**: после каждого — компиляция без ошибок, проверка в Play mode, атомарный коммит.
   Не смешивай срезы в одном коммите.
8. Если доступен Unity MCP — `read_console` после каждой правки C#, `manage_editor` для Play mode.
   Если нет — в отчёте явно перечисли, что требует ручной проверки.
9. Публичные сигнатуры существующих API не менять без необходимости; новые методы аддитивные.
   Если сигнатуру всё же меняешь — обнови всех вызывающих и напиши об этом в отчёте.

## Известные ловушки (проверь их, иначе получишь регресс)

- **Слой игрока = 0** (`Player.prefab`), врагов тоже 0. Приём `~(1 << gameObject.layer)` при слое 0
  не исключает ничего (`PlayerInteraction.cs:88,104`). Свой корень отсекай сравнением
  `hit.collider.transform.root == casterRoot`, а не слоем.
- **Trigger-коллайдеры.** Зона магии — trigger (`CombatHitbox.Awake` принудительно `isTrigger = true`).
  Луч прицела обязан идти с `QueryTriggerInteraction.Ignore`, иначе попадёт в свои прошлые зоны и в лут.
- **`Camera.main` ненадёжен.** В `Game.unity` объект `Main Camera` выключен (`m_IsActive: 0`),
  рабочая камера — в `CameraContainer`. Бери `PlayerController.ResolveViewCamera()`
  (`PlayerController.cs:975-990`) — он отсеивает камеры с `targetTexture`.
- **Камера отстаёт на кадр**: `CameraOrbitMode.autoOrbit` в сцене выключен, вращение в `LateUpdate`.
  Точку считать в момент каста, не кэшировать между кастами.
- **Высота.** `origin.position` — корень у ступней. Без `+ Vector3.up * aimHeight` VFX уходит под землю
  (текущая демо-ветка подъём делает: `CultivationCastController.cs:203`).
- **EditMode-тесты.** `SkillSystemTests.cs:157` создаёт `AbilitySystem` на пустом GameObject без камеры
  и без `PlayerController`. Новый код обязан молча уходить в фолбэк, иначе NRE в тестах.
- **LOS.** `CombatHitbox.HasLineOfSight` (`:175-194`) при пустом `ownerLayers` считает препятствием всё:
  цель за укрытием урона не получит. Это ожидаемое поведение, не баг.
- **Спавн в точке прицела + мгновенная зона** дают двойной урон, если сделать и снаряд, и зону.
  Выбирай одно: либо снаряд с зоной по прибытии, либо мгновенная зона в точке.

---

## Срез 1. Прицел: точка из центра экрана (P0)

**Цель:** каст по клавише 4/5 бьёт туда, куда смотрит центр экрана; дальность берётся из ассета.

### 1.1 Статический резолвер точки прицела

Создай `Assets/Scripts/Combat/AimResolver.cs`:

```csharp
using UnityEngine;

/// Точка прицела из центра экрана. Общий код для AbilitySystem и демо-кастера.
public static class AimResolver
{
    /// hintFallback — направление, если камеры нет (тесты, сцены без камеры).
    public static Vector3 Resolve(Transform casterRoot, float range, LayerMask mask,
        float aimHeight, Vector3 hintFallback)
    {
        Camera cam = PlayerController.ResolveViewCamera();
        Vector3 origin = casterRoot.position + Vector3.up * aimHeight;

        if (cam == null) return origin + hintFallback * range;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, range, mask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        Vector3 point = ray.GetPoint(range);
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            // Свой коллайдер (модель персонажа) препятствием не считается.
            if (hits[i].collider.transform.root == casterRoot) continue;
            if (hits[i].distance >= best) continue;
            best = hits[i].distance;
            point = hits[i].point;
            found = true;
        }

        // Ничего не задели — точка на максимальной дальности вдоль луча.
        return found ? point : ray.GetPoint(range);
    }
}
```

Требования:
- `casterRoot` — корень игрока, `hintFallback` — `casterRoot.forward`.
- Никаких аллокаций в кадре: `RaycastAll` тут вызывается только на каст, это допустимо.
  Если хочешь без аллокаций — `Physics.RaycastNonAlloc` с буфером на 16.
- Маску по умолчанию — `~0`, но поле `aimMask` в `AbilitySystem` дать, чтобы можно было исключить слои.

### 1.2 `AbilitySystem`: считать точку до каста

Файл `Assets/Scripts/Combat/AbilitySystem.cs`.

1. Добавь поля рядом с существующими `[Header]`-секциями:

```csharp
[Header("Прицел")]
[Tooltip("Слои, по которым ищется точка прицела. Trigger-коллайдеры игнорируются всегда.")]
[SerializeField] private LayerMask aimMask = ~0;
[Tooltip("Высота точки вылета над корнем персонажа.")]
[SerializeField, Min(0f)] private float aimHeight = 1.2f;
```

2. `ExecuteMagic` (`:463-502`): посчитай точку **до** `WaitForSeconds(vfxDelay)` — за задержку 0.2 с
   игрок успевает довернуть камеру, и снаряд должен уйти уже по новой цели:

```csharp
Transform caster = playerController != null ? playerController.transform : transform;
float range = Mathf.Max(1f, ability.magicRange);
Vector3 aimPoint = AimResolver.Resolve(caster, range, aimMask, aimHeight, caster.forward);

// ... существующий код каста и анимации ...

yield return new WaitForSeconds(Mathf.Max(0f, ability.vfxDelay));

aimPoint = AimResolver.Resolve(caster, range, aimMask, aimHeight, caster.forward); // пересчёт после задержки
if (playerController != null) playerController.FaceTowards(aimPoint, 0.35f);

SpawnVfx(ability, aimPoint);
CreateMagicHitbox(ability, aimPoint);
```

`FaceTowards` — существующий public-метод `PlayerController.cs:432-436`, используй его, свой поворот не пиши.

3. `SpawnVfx` (`:504-513`) — принять `aimPoint`, поднять точку вылета и развернуть эффект по направлению:

```csharp
private void SpawnVfx(AbilityData ability, Vector3 aimPoint)
{
    if (ability.vfxPrefab == null) return;

    Transform caster = playerController != null ? playerController.transform : transform;
    Vector3 spawn = caster.position + Vector3.up * aimHeight + caster.forward * ability.magicSpawnDistance;
    Vector3 direction = aimPoint - spawn;
    Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : caster.rotation;

    GameObject vfx = Instantiate(ability.vfxPrefab, spawn, rotation);
    if (ability.vfxLifetime > 0f) Destroy(vfx, ability.vfxLifetime);
}
```

4. `CreateMagicHitbox` (`:515-549`) — ставить зону **в точку прицела**, а не в 1.5 м вперёд:

```csharp
private void CreateMagicHitbox(AbilityData ability, Vector3 aimPoint)
{
    Transform caster = playerController != null ? playerController.transform : transform;
    GameObject zone = new GameObject("MagicHitbox_Temp");
    zone.transform.position = aimPoint;
    // ... остальное без изменений (SphereCollider, kinematic Rigidbody, CombatHitbox, lifetime)
    hitbox.Configure(ability.power * Mathf.Max(0f, DamageMultiplier), CombatTeam.Player, caster);
}
```

5. `magicRange` (`AbilityData.cs:56`) перестаёт быть мёртвым полем — это максимальная дальность.
   В `Assets/Combat/Abilities/MagicBurst.asset` подними `magicRange` с `2.5` до `16`, иначе луч
   упрётся в 2.5 м и правка не будет заметна.

### 1.3 Демо-ветка

`Assets/Scripts/Cultivation/Runtime/CultivationCastController.cs:201-213` — та же логика:
точка через `AimResolver`, разворот `Quaternion.LookRotation`, спавн на `+ Vector3.up * 1f`
(подъём уже есть — сохрани). Урон в демо-ветке остаётся `OverlapSphere`, менять не нужно.

### Приёмка среза 1

- Наведение в центр экрана + `4` → эффект появляется **в точке под прицелом**, а не в 1.5 м вперёд.
- Навести на стену в 10 м и нажать `4` → урон получает стена/никто, враг за стеной — нет.
- Навести на врага → попадание; игрок доворачивается к цели.
- Каст без камеры в сцене не падает и работает по `forward`.
- `Assets/Tests/EditMode/SkillSystemTests.cs` проходит (без NRE).

---

## Срез 2. Тип доставки + полёт снаряда (P0/P1)

**Цель:** префаб из `Assets/Magic` может быть снарядом, лучом, аурой или ударом на месте — и это
настраивается в ассете, а не кодом. Префабы остаются визуальными: 57 файлов не правим.

### 2.1 Поля настройки

`Assets/Scripts/Cultivation/Runtime/TechniqueData.cs` — добавь:

```csharp
public enum DeliveryType { Impact, Projectile, Beam, Aura, Self }

[Header("Доставка")]
[SerializeField] private DeliveryType delivery = DeliveryType.Impact;
[Tooltip("Скорость снаряда/луча, м/с. 0 — мгновенно.")]
[SerializeField, Min(0f)] private float speed = 18f;
[Tooltip("Время жизни снаряда, с. 0 — до попадания.")]
[SerializeField, Min(0f)] private float lifetime = 3f;
[Tooltip("Радиус зоны урона в точке попадания.")]
[SerializeField, Min(0f)] private float radius = 2f;
[Tooltip("Префаб вспышки попадания. Пусто — только vfxPrefab.")]
[SerializeField] private GameObject impactPrefab;
[Tooltip("Префаб вспышки каста у руки. Пусто — без вспышки.")]
[SerializeField] private GameObject muzzlePrefab;
[SerializeField] private bool homing;
```

Плюс свойства `Delivery`, `Speed`, `Lifetime`, `Radius`, `ImpactPrefab`, `MuzzlePrefab`, `Homing`
и расширение `Setup(...)` (сигнатуру не ломай — добавь перегрузку).

### 2.2 Рантайм-компонент снаряда

Создай `Assets/Scripts/Combat/MagicProjectile.cs`. Он **навешивается в рантайме** на инстанс префаба,
поэтому коллайдеры/Rigidbody в 57 префабах добавлять не нужно:

```csharp
using UnityEngine;

/// Полёт снаряда магии к точке прицела. Навешивается в рантайме на инстанс VFX-префаба.
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class MagicProjectile : MonoBehaviour
{
    private Vector3 target;
    private Vector3 direction;
    private float speed;
    private float radius;
    private float lifetime;
    private float damage;
    private float hitboxLifetime;
    private Transform owner;
    private LayerMask targetLayers;
    private GameObject impactPrefab;
    private bool homing;
    private bool fired;

    public void Configure(Vector3 aimPoint, float projectileSpeed, float life, float damageRadius,
        float projectileDamage, float zoneLifetime, Transform ownerRoot, LayerMask layers,
        GameObject impact, bool useHoming)
    { /* сохранить поля; direction = (aimPoint - transform.position).normalized */ }

    private void Update()
    {
        // Полёт: MoveTowards к точке прицела, а не физика — попадание детерминировано.
        // По достижении target или по lifetime — Explode().
    }

    private void Explode()
    {
        // 1) спавн impactPrefab в точке
        // 2) временная trigger-зона с CombatHitbox (копия AbilitySystem.CreateMagicHitbox)
        // 3) Destroy(gameObject)
    }
}
```

Требования:
- Rigidbody — kinematic, `useGravity = false` (иначе `OnTriggerEnter` не сработает; тот же приём,
  что в `AbilitySystem.cs:538-540`).
- SphereCollider — `isTrigger = true`, радиус из `radius`.
- Урон — через существующий `CombatHitbox`: не писать свой `TakeDamage`, не изобретать `IDamageable`.
  Блокировка (`ICombatBlockable`) и дедупликация попаданий уже внутри.
- Никакой физики полёта: `Vector3.MoveTowards` к `target`, при достижении — `Explode()`.
- Если `speed <= 0` — считать мгновенным: сразу `Explode()` в точке прицела.
- `homing` = true → обновлять `target` на позицию текущей цели (если цель найдена по слоям), иначе — фиксированная точка.

### 2.3 Точка входа — одна

`AbilitySystem.SpawnVfx` — единственное место, где выбирается поведение:

```csharp
switch (delivery)
{
    case DeliveryType.Projectile: // Instantiate(prefab, spawn, rot) + MagicProjectile.Configure(aimPoint, ...)
    case DeliveryType.Impact:     // текущее поведение: Instantiate + CreateMagicHitbox в aimPoint
    case DeliveryType.Beam:       // Instantiate в spawn, LookAt(aimPoint), тик урона OverlapSphere каждые 0.2 с
    case DeliveryType.Aura:       // Instantiate в playerController.transform (НЕ в мировую точку), жить до конца действия
    case DeliveryType.Self:       // Instantiate на игроке, без урона
}
```

`CultivationAbilityBridge.ToAbilityData` (`:297-319`) прокидывает новые поля — проводка уже есть,
архитектуру не меняй.

### 2.4 Разрешить `Aura`/`Beam` в мосте

`CultivationAbilityBridge.cs:69-82` сейчас отвергает `Block` и `Aura` («механика в разработке»).
Вариант минимальной правки: маппить `Aura` → `AbilityType.Magic` с `DeliveryType.Aura` (эффект на игроке
+ периодический урон в радиусе), `Block` — оставить как есть до отдельной задачи. Не выдавай игроку
кнопку, которая ничего не делает: если механика не готова, кнопка должна быть неактивна и **с причиной**.

### Приёмка среза 2

- Техника с `Delivery = Projectile` и `vfxPrefab = Assets/Magic/1Dammage/Fire Bullet.prefab`
  → видимый снаряд летит в точку прицела и взрывается в ней.
- `Delivery = Beam`, `vfxPrefab = Assets/Magic/2Dammage/Beam Blue.prefab` → луч в цель.
- `Delivery = Aura`, `vfxPrefab = Assets/Magic/PersonAura/Holy Aura.prefab` → аура едет на игроке.
- Смена типа/скорости/радиуса делается в инспекторе ассета, без правки кода.
- Снаряд не наносит урон дважды за одно попадание.

---

## Срез 3. Древо изучения Дао из префабов (P1)

**Цель:** «из всех префабов магии сделать скиллы дерева Дао».

### 3.1 Данные узла

`TechniqueData.cs` — добавь:

```csharp
[Header("Древо")]
[Tooltip("Стоимость в очках Дао. 0 — узел стартовый.")]
[SerializeField, Min(0)] private int unlockCost = 1;
[Tooltip("ID техник-предпосылок в этом же Дао.")]
[SerializeField] private string[] prerequisiteIds = new string[0];
[SerializeField] private bool starter;
```

`rank` используй как **ярус** дерева; порядок в `DaoPath.techniques` — как ветвь.

### 3.2 Очки и изучение

`DaoSystem.cs`:

- добавь `Dictionary<DaoType,int> spentPoints`;
- уровень считай как `startingLevel + (points - spent) / PointsPerLevel` (правка `:248` и `GetPointsIntoLevel` `:135-138`);
- добавь `bool TryUnlockTechnique(TechniqueData technique)`: проверить `IsDaoUnlocked`, предпосылки через
  `HasLearnedTechnique`, хватает ли `GetPoints - spent`, затем списать и вызвать **существующий**
  `AddLearnedTechnique` (`:150-169`).

Почему так: `AddLearnedTechnique` уже даёт обход требований по уровню/реалму (`:390`, `:402`) и **уже
сохраняется** (`GetLearnedTechniqueIds` `:187-190` → `CultivationStateCache.cs:82`). Древо получает
персистентность бесплатно, формат `Cultivation.Save.v2` не ломается. `spentPoints` добавь в снимок
(`CultivationSnapshot`), предусмотрев чтение старых сейвов без падений.

### 3.3 Импортёр префабов

Новый Editor-скрипт `Assets/Scripts/Cultivation/Editor/DaoTreeSetup.cs`, меню
`Tools → Cultivation → Build Dao Tree From Magic`. По образцу `CultivationSetup.cs:150-186`
(создание `TechniqueData` + запись в `DaoPath.techniques`):

1. `AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Magic" })`.
2. Маппинг папка/имя → `DaoType` (константой в файле, легко правится):
   `Fire`, `1Dammage/Fire Bullet`, `2Dammage/Fire Tornado` → Fire; `2Dammage/Lightning Strike`,
   `1Dammage/Electricity`, `Plasma Ball` → Thunder; `FihtEffect/Slash*` → Sword;
   `FihtEffect/*Impact`, `Gun*` → Fist; `otherAura`, `PersonAura`, `Shield`, `Fire`, `Portal` → Body.
3. `delivery` по категории: PROJECTILE → `Projectile`; BEAM → `Beam`; AURA/AREA/SHIELD/PORTAL → `Aura`;
   IMPACT/SLASH/MUZZLE → `Impact`; TRAIL → не создавать узел (это шлейф, а не скилл).
4. `rank` = ярус в ветке; `unlockCost` = f(rank) (например 1/2/4/8); `prerequisiteIds` = id предыдущего
   яруса той же ветки; `starter = rank == 1`.
5. `power`, `qiCost`, `cooldown` — из категории (урон 30–110, Ци 12–70, КД 4–50).
6. `technique.Setup(...)` с `vfx = prefab` — сигнатура уже принимает VFX (`TechniqueData.cs:68-87`),
   затем `AssetDatabase.CreateAsset` и добавление в `DaoPath.techniques`.
7. `icon` — оставь пустым (в UI есть рабочий фолбэк-руна) либо сгенерируй превью префаба в спрайт.
8. Импортёр **идемпотентный**: повторный запуск обновляет существующие ассеты, не плодит дубликаты.
9. Обнови `CultivationDefaults.cs` (`:72-110`) теми же полями, иначе рантайм-фолбэк без ассетов
   останется плоским.
10. Обнови `CultivationSetup.ValidateSetup` (`:50-...`): требовать, чтобы у каждого узла с
    `delivery != Impact` был непустой `vfxPrefab`, а у каждой предпосылки — существующий id.

### 3.4 Экран дерева

Новый `Assets/Scripts/Cultivation/Runtime/UI/DaoTreeUI.cs` — по образцу `SkillListUI.cs`
(строительные блоки готовы: `CultivationUIFactory.CreateImage/Place/CreateButton/AddGoldBorder`,
образец строки — `SkillListUI.BuildRow` `:245-307`).

Минимум: узлы колонками по `rank`, состояния locked/available/owned, клик → `TryUnlockTechnique`,
подпись с ценой и причиной недоступности. Вход — кнопка в `CultivationUI` рядом с существующей
«Техники» (`CultivationUI.cs:265-268`) либо третья вкладка в `SkillListUI` (`:105`).

Слотов всего 2 на техники (`CultivationAbilityBridge.cs:99`). Если узлов станет 40+ — добавь
страницы слотов или расширь `AbilitySystem.SlotCount` **отдельным срезом**, не в этом.

### Приёмка среза 3

- В дереве видны узлы из `Assets/Magic` с ценой и предпосылкой.
- «Изучить» списывает очки Дао; без очков — отказ с причиной; без предпосылки — отказ с причиной.
- После перезапуска игры изученное и потраченные очки на месте.
- Старый сейв `Cultivation.Save.v2` читается без ошибок в консоли.
- Повторный запуск импортёра не создаёт дубликатов.

---

## Срез 4. Гигиена префабов (P2, точечно)

Не переделывай все 57. Только те, что пойдут в узлы:

- снять демо-скрипты AllIn1 (`AllIn1VfxAutoDestroy`, `AllIn1DoShake`, `AllIn1VfxFadeLight`,
  `AllIn1AutoRotate`), если они навязывают свои тайминги и тряску камеры;
- одноразовым выставить `looping = 0` и `stopAction = Destroy` (сейчас вечные эмиттеры у
  `Fire Bullet`, `Plasma Ball`, `Magic Spiral`, `Ice Projectile`, все Trail);
- добавить пустой дочерний GO `Root` в начале координат как точку-якорь (имён `Muzzle/Start/End/Point`
  в префабах нет вообще);
- в `Ice Impact` погасить 2 ParticleSystem с `loop = 1, duration = 5` (снег сыпется после смерти эффекта).

Учти вес: 20 префабов 0.7–1.8 МБ. Если снаряды начнут срабатывать чаще ~1 раза в секунду — нужен пул
объектов вместо `Instantiate` (в проекте уже есть предупреждение: `CombatFeedback.cs:160`).

---

## Отчёт исполнителя (обязательно)

1. Что сделано по каждому срезу + список изменённых/созданных файлов.
2. Вывод `read_console` после каждого среза (ошибки/варнинги).
3. Что проверено в Play mode и как именно; что осталось на ручную проверку.
4. Были ли изменения публичных сигнатур — какие и кто вызывающие.
5. Любые отклонения от промта с причиной.
