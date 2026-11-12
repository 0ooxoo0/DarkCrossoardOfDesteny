# Дизайн: система оружия, боя и способностей

Дата: 2026-11-09
Статус: на утверждении
Проект: `D:\.Programm\UnitySave\DarkCrossoardOfDesteny`

## 1. Зачем это нужно

Расширить уже существующего игрока (Humanoid `BaseMale`) так, чтобы оружие
(кулаки/меч) было данными, а не кодом: одно и то же оружие должно менять
стойку/анимации, набор способностей, визуальную модель и хитбокс. Способности
должны запускаться с клавиш 1–5, иметь кулдауны, UI и поддержку магии с VFX.
При этом нельзя ломать существующие гардероб, `DestructibleObject`,
`PlayerController`, поворот персонажа и базовый Animator.

## 2. Цели

- `WeaponData` и `AbilityData` как ScriptableObject-ассеты, настраиваемые в
  инспекторе.
- `WeaponManager` на игроке: экипировка, смена `AnimatorOverrideController`,
  привязка модели меча к правой руке, переключение активного хитбокса.
- Гардероб получает только два новых публичных члена:
  `event Action<WeaponData> OnWeaponChanged` и
  `void EquipWeapon(WeaponData weapon)`. Существующие поля/методы не
  переименовываются.
- В Canvas гардероба появляется кнопка(и) выбора оружия.
- Урон обычной атаки и melee-способности идёт через trigger-коллайдер с тегом
  `DammageObject`; `DestructibleObject.OnTriggerEnter` остаётся единственным
  обработчиком уничтожения. Список тегов урона вынесен в
  `WeaponData.damageTags`/`AbilityData.damageTags` (по умолчанию
  `{"DammageObject"}`), чтобы позже расширять без переписывания.
- `AbilitySystem`: 5 слотов, кулдауны, Melee/Magic, `castAnimation`, VFX.
- `AbilityBarUI` + `AbilitySlotUI`: панель снизу по центру, иконки, оверлей
  кулдауна, номер слота.
- Hybrid-подход: если у оружия есть готовый `AnimatorOverrideController` —
  используется он; если нет — контроллер собирается в рантайме из клипов.

## 3. Не-цели

- Не делать здоровье, инвентарь, AI врагов, систему уровней.
- Не добавлять новые состояния/переходы в `PlayerController.controller`.
- Не переписывать `DestructibleObject` и `PlayerController`.
- Не делать снаряды для магии; магия — мгновенная зона. Снаряд можно добавить
  позже отдельной способностью.
- Не трогать поворот персонажа и `AimController`.
- Не менять имена существующих полей и публичных методов `Garderob`.
- Новые хитбоксы не наносят урон `HarvestableObject`/`Enemy`/`IInteractable`.
  Существующий `PlayerInteraction` (левый клик, конус 25 урона) остаётся как
  есть и не отключается. Урон новых хитбоксов — только по
  `DestructibleObject` через тег `DammageObject`; список тегов расширяемый,
  но фактическое поведение сейчас прежнее.

## 4. Ограничения и факты проекта

- Игрок: `Assets/Player.prefab`, Humanoid-модель `BaseMale`, кость правой руки
  `R_wrist_joint` (доступна через
  `Animator.GetBoneTransform(HumanBodyBones.RightHand)`).
- Базовый контроллер: `Assets/PlayerAnimations/PlayerController.controller`
  (guid `d72ac30c17265054499cbbf60f69b7ee`). В нём:
  - `Locomotion` — BlendTree `idle`/`Sprint` по параметру `Speed`;
  - `Attack` — клип `PunchRight`, триггер `Attack`, скорость состояния `1.25`,
    выход в `Locomotion` по `0.92`.
- Оригинальные клипы базового контроллера:
  - idle: `Assets/Assets/HumanLowPoly/Art/Animations/Animations_Starter_Pack/Movement/Idle.fbx`;
  - run: `.../Movement/Sprint.fbx`;
  - attack: `.../Combat/PunchRight.fbx`.
- Кулаки: стойка `FIGHTING SWAY` (guid `ffa71c695d0a2e441a0e7ad80cd615fe`),
  атака `JAB` (guid `af968bf57b7f2c741bb6e44deadee8a0`).
- Меч: `idle` (guid `641e2dd2f9e63e64f8d9a1cb3853e767`), `run02_front`
  (guid `d5ba8c42eb73fef4591f1cae52a6723c`), `attack_01`
  (guid `8dddeab80bf640d41a7760089fc65f67`). Все клипы Humanoid.
- Модель меча: `Assets/Assets/DarkFantasy/PolygonDarkFantasy/Prefabs/Weapons/SM_Wep_Sword_01.prefab`.
  В префабе есть `MeshCollider`; при установке в руку его нужно отключить.
- Существующий хитбокс кулаков: дочерний `Cube` в `Player.prefab`, тег
  `DammageObject`, trigger BoxCollider, объект неактивен, рендер выключен.
  В `Game.unity` на нём добавлен kinematic `Rigidbody`; в самом prefab
  `Rigidbody` нет. Чтобы не плодить дубликаты prefab/scene, `WeaponManager`
  добавляет kinematic `Rigidbody` на `Cube` в рантайме, только если его нет.
- В сцене два экземпляра `Garderob`: на манекене `BasicHero_M`
  (fileID `394657650`, к нему привязаны существующие кнопки гардероба) и на
  экземпляре игрока (fileID `1161921854`, ни одна кнопка на него не ссылается).
  `WeaponManager` подписывается на события **всех** найденных `Garderob`,
  поэтому неважно, какая копия используется UI.
- У импортированных клипов `events: []`; AnimationEvent можно добавить
  Editor-скриптом через `AnimationUtility.SetAnimationEvents`.
- В проекте включены оба Input System (`activeInputHandler: 2`); используем
  `UnityEngine.InputSystem.Keyboard.current`.
- В сцене уже есть `Canvas`, `PauseMenu`, `GarderobCanvas` (неактивен) и
  `GarderobSystem`. UI строится на uGUI + TextMeshPro.
- `UiManager.IsOpen` уже управляет паузой; способности не должны срабатывать
  при открытом UI.

## 5. Рассмотренные подходы

### A. Asset-driven

Создать `.overrideController`-ассеты и `WeaponData`/`AbilityData`-ассеты,
всё назначать в инспекторе. Плюс: прозрачно, удобно дизайнеру. Минус: много
ручной работы, новые виды оружия требуют новых ассетов.

### B. Runtime-generated

`WeaponData` хранит базовый контроллер и клипы, `WeaponManager` собирает
`AnimatorOverrideController` в рантайме. Плюс: минимум ассетов. Минус: override
не виден в Animator, смена контроллера сбрасывает состояние, сложнее отлаживать.

### C. Hybrid — выбран

`WeaponData.animatorOverride` опционален. Если он есть — используется ассет;
если нет — `WeaponManager` собирает override из клипов. Кулаки и меч получают
готовые override-ассеты, созданные setup-скриптом, но система не ломается,
если ассет не назначен. Это даёт удобство инспектора и запасной путь.

Причина выбора: пользователь хочет гибкость без обязательной ручной возни с
ассетами; при этом готовые override-ассеты для двух стартовых оружий всё равно
полезны.

## 6. Архитектура и файлы

### Новые runtime-скрипты

- `Assets/Scripts/Combat/WeaponData.cs`
  - `enum WeaponType { Any, Fists, Sword }`
  - `[CreateAssetMenu] class WeaponData : ScriptableObject`
- `Assets/Scripts/Combat/AbilityData.cs`
  - `enum AbilityType { Melee, Magic }`
  - `[CreateAssetMenu] class AbilityData : ScriptableObject`
- `Assets/Scripts/Combat/WeaponManager.cs`
- `Assets/Scripts/Combat/AbilitySystem.cs`
- `Assets/Scripts/Combat/AttackHitbox.cs`
- `Assets/Scripts/Combat/AnimationEventRelay.cs`
- `Assets/Scripts/Combat/WeaponEquipButton.cs` — помощник для кнопки гардероба.
- `Assets/Scripts/UI/AbilityBarUI.cs`
- `Assets/Scripts/UI/AbilitySlotUI.cs`

### Новый Editor-скрипт

- `Assets/Editor/WeaponSystemSetup.cs`
  - меню `Tools/Combat/Setup Weapon & Ability System`;
  - создаёт папки/ассеты, override-контроллеры, prefab слота, добавляет
    компоненты на игрока, панель на Canvas, кнопки в GarderobCanvas;
  - добавляет AnimationEvent в `JAB` и `attack_01`;
  - идемпотентен: повторный запуск обновляет, а не дублирует.

### Минимальные правки существующих файлов

- `Assets/Garderob.cs`
  - добавить `public event Action<WeaponData> OnWeaponChanged;`
  - добавить `public void EquipWeapon(WeaponData weapon)`.
  - Больше ничего не менять.
- `Assets/Scripts/Player/PlayerController.cs`
  - новое поле `[SerializeField] private WeaponManager weaponManager;`
  - в `SetWeaponHitbox` использовать `weaponManager`, если он назначен;
  - добавить `public void ResetHitboxState()` для сброса `hitboxActive` при
    смене оружия;
  - (опционально) `public event Action OnAttackEnded` — если понадобится
    AbilitySystem; в текущем дизайне не обязателен.
- `Assets/Scripts/Player/Weapon.cs`
  - `SetHitboxActive` должен включать GameObject при необходимости и
    переключать только коллайдер, не выключая весь объект. Это нужно, чтобы
    `Cube` можно было активировать для кулаков и чтобы компонент не пропадал.
- `Assets/Player.prefab`
  - добавить `WeaponManager`, `AbilitySystem`, `AnimationEventRelay`;
  - назначить ссылки;
  - **не** добавлять `Rigidbody` на `Cube` в prefab (в `Game.unity` он уже
    есть); `WeaponManager` добавит kinematic `Rigidbody` в рантайме, только
    если его нет. Это избегает дубликата prefab/scene.
- `Assets/Scenes/Game.unity`
  - добавить `AbilityBarUI` на существующий `Canvas`;
  - добавить панель с кнопками оружия в `GarderobCanvas`;
  - назначить ссылки.

## 7. Данные

### WeaponData

```csharp
public enum WeaponType { Any, Fists, Sword }

[CreateAssetMenu(menuName = "Combat/Weapon Data", fileName = "NewWeapon")]
public class WeaponData : ScriptableObject
{
    public string weaponId;
    public string displayName;
    public AnimatorOverrideController animatorOverride; // опционально
    public WeaponType type;
    public List<AbilityData> abilities = new List<AbilityData>();

    // true — хитбокс переключают AnimationEvent; false — PlayerController
    // по normalizedTime. Два источника одновременно не работают.
    // По умолчанию true: события расставляет setup-скрипт.
    public bool useAnimationEvents = true;

    [Header("Damage")]
    // Сейчас поведение = только "DammageObject". Список оставлен для
    // расширения; первый тег используется как тег hitbox-коллайдера.
    public List<string> damageTags = new List<string> { "DammageObject" };

    [Header("Visual")]
    public GameObject weaponModelPrefab;       // null для кулаков
    public Vector3 weaponLocalPosition;
    public Vector3 weaponLocalEulerAngles;
    public Vector3 weaponLocalScale = Vector3.one;

    [Header("Sword Hitbox")]
    public Vector3 hitboxLocalPosition;
    public Vector3 hitboxLocalEulerAngles;
    public Vector3 hitboxSize = new Vector3(0.15f, 0.15f, 1.2f);
}
```

`animatorOverride == null` означает «использовать базовый контроллер».
Для меча `weaponModelPrefab = SM_Wep_Sword_01`, для кулаков — `null`.
`useAnimationEvents = true` выставляется setup-скриптом для кулаков и меча
после добавления событий в `JAB`/`attack_01`. Если клип заменили и событий
больше нет — флаг нужно выключить, иначе хитбокс не будет переключаться.

### AbilityData

```csharp
public enum AbilityType { Melee, Magic }

[CreateAssetMenu(menuName = "Combat/Ability Data", fileName = "NewAbility")]
public class AbilityData : ScriptableObject
{
    public string abilityId;
    public string displayName;
    public Sprite icon;
    public KeyCode hotkey = KeyCode.Alpha1;
    public AbilityType type;
    public float cooldown = 1f;
    public GameObject vfxPrefab;
    public AnimationClip castAnimation;
    public WeaponType requiredWeapon = WeaponType.Any;

    [Header("Damage")]
    // Сейчас поведение = только "DammageObject". Список — задел на будущее.
    public List<string> damageTags = new List<string> { "DammageObject" };
    public LayerMask damageLayers = ~0;

    [Header("Magic")]
    public float magicRange = 2.5f;
    public float magicRadius = 2f;
    public float magicSpawnDistance = 1.5f;
    public float vfxDelay = 0.2f;
    public float vfxLifetime = 2f;
    public float magicHitboxLifetime = 0.2f;
}
```

`hotkey` используется для отображения и для фактической клавиши, если это
`Alpha1..Alpha5`; иначе слот использует свой номер. `requiredWeapon = Any`
означает «доступно с любым оружием».

## 8. WeaponManager

Поля/ссылки:

- `Animator animator`, `PlayerController playerController`,
  `AbilitySystem abilitySystem`, `Weapon fistWeapon` (компонент на `Cube`).
- `RuntimeAnimatorController baseController` — базовый контроллер игрока.
- `AnimationClip baseAttackClip` — оригинальный `PunchRight` (нужен для
  временной подмены клипа способности).
- `Transform rightHand` — кэш `GetBoneTransform(RightHand)`.
- `WeaponData startingWeapon`.
- `List<Garderob> garderobs` — все найденные в сцене; подписка на события
  каждого.

Публичные члены:

- `WeaponData CurrentWeapon { get; }`
- `bool SuppressHitbox { get; set; }`
- `bool UseAnimationEvents { get; }` — `currentWeapon != null &&
  currentWeapon.useAnimationEvents`.
- `void EquipWeapon(WeaponData weapon)`
- `void SetHitboxActive(bool active, bool fromAnimationEvent = false)`
- `void EnableHitbox()` / `void DisableHitbox()` — для `AnimationEventRelay`.
- `bool PlayCastAnimation(AnimationClip clip)` — временная подмена Attack-клипа.

`EquipWeapon`:

0. `if (weapon == currentWeapon) return;` — защита от двойного события
   (например, от двух `Garderob`). Одинаковое оружие не пересбрасывает
   аниматор и кулдауны.
1. Сохраняет `currentWeapon`.
2. Останавливает текущий cast/restore-корутин.
3. Устанавливает `animator.runtimeAnimatorController`:
   - `weapon.animatorOverride`, если он есть;
   - иначе `baseController`.
4. Удаляет предыдущую визуальную модель (`Destroy`).
5. Для меча:
   - создаёт экземпляр `weaponModelPrefab` под `rightHand`;
   - применяет `weaponLocalPosition/EulerAngles/Scale`;
   - отключает все `Collider` на модели;
   - создаёт дочерний `Hitbox` с `BoxCollider` (trigger), `Rigidbody`
     (kinematic), тегом из `damageTags[0]` (по умолчанию `DammageObject`),
     компонентом `AttackHitbox`;
   - применяет `hitboxLocalPosition/EulerAngles/Size`;
   - выключает хитбокс.
6. Для кулаков:
   - удаляет модель меча;
   - активирует `Cube` и оставляет его collider выключенным;
   - если на `Cube` нет `Rigidbody`, добавляет его и ставит
     `isKinematic = true`, `useGravity = false`. Если `Rigidbody` уже есть —
     не трогает его настройки.
7. Вызывает `abilitySystem.SetAbilities(weapon.abilities)`.
8. Сбрасывает состояние хитбокса в `PlayerController`.
9. Уведомляет подписчиков (если понадобится UI оружия).

Подписка на гардероб:

- В `Start`: `FindObjectsOfType<Garderob>(true)`; подписаться на
  `OnWeaponChanged` **каждого** экземпляра (в сцене их два: манекен
  `BasicHero_M` и игрок). Это снимает неоднозначность, какая копия
  используется кнопками гардероба.
- В `OnDestroy`: отписка от всех.
- Если `startingWeapon != null`, экипируется в `Start`.

`SetHitboxActive(bool active, bool fromAnimationEvent = false)`:

- Если `SuppressHitbox` — выключить оба хитбокса и выйти.
- Если `currentWeapon != null` и `currentWeapon.useAnimationEvents`:
  - вызовы от `PlayerController` (`fromAnimationEvent == false`) игнорируются;
  - вызовы от `AnimationEventRelay` (`fromAnimationEvent == true`)
    выполняются.
- Если `currentWeapon != null` и `useAnimationEvents == false`:
  - вызовы от `AnimationEventRelay` игнорируются;
  - вызовы от `PlayerController` выполняются.
- Если `currentWeapon == null` — работает fallback `PlayerController`.
- Применение: кулаки/`null` — `fistWeapon.SetHitboxActive(active)`,
  sword-хитбокс выключен; меч — `fistWeapon.SetHitboxActive(false)`,
  sword-хитбокс `active`.

`PlayCastAnimation(clip)`:

- Если `clip == null` — вернуть `false`.
- Собрать временный `AnimatorOverrideController`:
  - базовый контроллер — `baseController`;
  - скопировать текущие overrides (если текущий контроллер —
    `AnimatorOverrideController`);
  - заменить `baseAttackClip` на `clip`.
- Назначить временный контроллер, вызвать
  `playerController.PlayAttackAnimation()`.
- Если атака не началась — вернуть контроллер оружия и вернуть `false`.
- Иначе запустить корутин восстановления через
  `clip.length / 1.25f + 0.1f` секунд; восстановить контроллер оружия.
- При смене оружия корутин останавливается и контроллер восстанавливается.

## 9. AbilitySystem

- `AbilitySlot[] slots` длиной 5. Каждый слот: `AbilityData ability`,
  `float cooldownEnd`.
- `SetAbilities(List<AbilityData> list)`:
  - копирует до 5 способностей, остальные слоты очищает;
  - сбрасывает кулдауны;
  - вызывает `OnAbilitiesChanged`.
- `Update`:
  - если `Keyboard.current == null` — выход;
  - если `UiManager.IsOpen` — выход;
  - для каждого слота определяет клавишу (`hotkey`, если это Alpha1..Alpha5,
    иначе номер слота) и при `wasPressedThisFrame` вызывает `TryActivate(i)`.
- `TryActivate(int index)`:
  - слот занят, кулдаун истёк, `requiredWeapon` совпадает;
  - вызывает `ActivateAbility(ability)`;
  - при успехе ставит кулдаун и вызывает `OnAbilityActivated(index)`.
- `ActivateAbility(AbilityData ability)`:
  - Melee:
    - если `castAnimation != null` — `weaponManager.PlayCastAnimation(...)`;
      иначе `playerController.PlayAttackAnimation()`;
    - `PlayerController.PlayAttackAnimation()` сам вызывает `StopMovement()`,
      ставит `isAttacking` и проигрывает состояние `Attack`;
    - окно хитбокса — единый источник истины: `WeaponData.useAnimationEvents`
      (AnimationEvent) либо `PlayerController` (normalizedTime 0.15–0.55);
    - `AbilitySystem` не переключает хитбокс напрямую, чтобы не спорить с
      `PlayerController`/событиями;
    - **melee-способность не имеет собственного хитбокса**: она запускает
      штатный триггер `Attack` текущего оружия, а урон наносит тот же
      `Cube`/клинок, что и обычная атака;
    - по завершении `PlayerController` сам вызывает `ResumeMovement()`.
  - Magic:
    - `weaponManager.SuppressHitbox = true` **до** запуска анимации;
    - `playerController.StopMovement()`;
    - если `castAnimation != null` — `weaponManager.PlayCastAnimation(...)`,
      иначе короткая пауза/без анимации;
    - через `vfxDelay` спавнит `vfxPrefab` перед игроком;
    - создаёт короткоживущий trigger-объём (`SphereCollider`, kinematic
      `Rigidbody`, тег `DammageObject`, радиус `magicRadius`,
      жизнь `magicHitboxLifetime`) в точке перед игроком;
    - `Physics.OverlapSphere` использует для обнаружения целей/лога и выбора
      точки VFX; уничтожение выполняет существующий
      `DestructibleObject.OnTriggerEnter`;
    - по завершении — `SuppressHitbox = false`, `ResumeMovement()`.

`castAnimation` и возврат из каста:

- `PlayCastAnimation` ждёт `clip.length / 1.25f + 0.15f` секунд — это позже
  выхода состояния `Attack` по `0.92`/`0.95`, поэтому к моменту возврата
  контроллера Animator уже в `Locomotion`.
- После возврата контроллера `PlayerController.UpdateAnimation` каждый кадр
  выставляет `Speed`, поэтому персонаж снова переходит в idle/run.
- При смене оружия/прерывании: остановить корутин, вернуть контроллер
  оружия, `SuppressHitbox = false`, `PlayerController.ResumeMovement()`,
  `ResetHitboxState()`. Animator не остаётся в состоянии каста.
- Если `castAnimation == null`, Animator вообще не переключается; магия
  просто ждёт `vfxDelay` и возвращает управление.
- Кулдаун обновляется в `Update`; UI читает `GetCooldownRemaining(i)`.
- Публичные методы для UI:
  - `AbilityData GetAbility(int index)`;
  - `float GetCooldownRemaining(int index)`;
  - `float GetCooldownFraction(int index)`;
  - `bool IsAbilityAvailable(int index)`;
  - события `OnAbilitiesChanged`, `OnAbilityActivated`.

## 10. AttackHitbox и AnimationEventRelay

`AttackHitbox`:

- `[SerializeField] Collider hitboxCollider;`
- `public void SetActive(bool active)` — `collider.enabled = active`.
- `public bool IsActive`.
- Компонент не реализует `OnTriggerEnter`: урон по `DestructibleObject` уже
  обрабатывается его собственным `OnTriggerEnter` по тегу `DammageObject`.
- На объекте обязателен kinematic `Rigidbody`, иначе trigger-события не
  гарантированы.

`AnimationEventRelay` (на игроке, рядом с Animator):

- `public void EnableHitbox()` → `weaponManager.EnableHitbox()`.
- `public void DisableHitbox()` → `weaponManager.DisableHitbox()`.
- Используется, если в клип добавлены AnimationEvent. Setup-скрипт добавляет
  события `EnableHitbox` (15% длины) и `DisableHitbox` (55% длины) в `JAB` и
  `attack_01`.
- Источник истины выбирается флагом `WeaponData.useAnimationEvents`
  (по умолчанию `true`):
  - `true` — `AnimationEventRelay` управляет хитбоксом; `PlayerController`
    проверяет `WeaponManager.UseAnimationEvents` и **вообще не вызывает**
    `SetHitboxActive`; события от `AnimationEventRelay` выполняются;
  - `false` — `PlayerController` управляет хитбоксом по `normalizedTime`;
    `AnimationEventRelay` проверяет `UseAnimationEvents` и игнорирует
    события; `WeaponManager` дополнительно страхует.
- Флаг проверяется в двух местах: в `PlayerController.SetWeaponHitbox` и в
  `WeaponManager.SetHitboxActive`. Это гарантирует, что при `true` путь
  `PlayerController` действительно не включается.
- `PlayerController.ResetHitboxState()` вызывается при смене оружия, чтобы
  его кэш `hitboxActive` не протух.

## 11. UI

`AbilitySlotUI`:

- Ссылки: `Image icon`, `Image cooldownOverlay`, `TMP_Text keyLabel`,
  `TMP_Text cooldownLabel`.
- API без прямой зависимости от `AbilitySystem` (чтобы prefab-ассет можно
  было создать до основного кода):
  - `SetAbility(AbilityData ability, int slotIndex, KeyCode hotkey)`;
  - `SetCooldown(float remaining, float total)`;
  - `SetAvailable(bool available)`.
- `AbilityBarUI` каждый кадр/по событию передаёт данные из `AbilitySystem` в
  слоты.
- Отображение:
  - иконка = `ability.icon`; если null — цветной прямоугольник по типу
    (красный Melee, синий Magic);
  - номер/клавиша;
  - оверлей: `fillAmount = remaining / cooldown`, тёмный полупрозрачный;
  - текст оставшихся секунд;
  - недоступная способность (чужое оружие) — приглушённая иконка.

`AbilityBarUI`:

- Ставится на существующий `Canvas` (неактивный `GarderobCanvas` не
  используется).
- В `Start` находит `AbilitySystem` (или берет назначенный), создаёт панель
  `AbilityBar` внизу по центру (`HorizontalLayoutGroup`,
  `ContentSizeFitter`, якоря bottom-center).
- Инстанцирует 5 слотов из `AbilitySlotUI` prefab-ассета; если prefab не
  назначен — строит слот кодом (fallback).
- Подписывается на `OnAbilitiesChanged`; при смене оружия обновляет иконки.
- Панель не должна блокировать raycast (`raycastTarget = false` у фона).

## 12. Интеграция с гардеробом

В `Garderob.cs` добавляется только:

```csharp
public event Action<WeaponData> OnWeaponChanged;

public void EquipWeapon(WeaponData weapon)
{
    OnWeaponChanged?.Invoke(weapon);
}
```

`WeaponManager` подписывается на `OnWeaponChanged` **всех** найденных
`Garderob` (и на манекене, и на игроке), поэтому кнопки могут ссылаться на
любую копию. В `GarderobCanvas` setup-скрипт создаёт панель `WeaponPanel` с
двумя кнопками: «Кулаки» и «Меч». Каждая кнопка содержит
`WeaponEquipButton` с ссылками на `Garderob` и `WeaponData`; `onClick`
вызывает `Equip()`, который вызывает `garderob.EquipWeapon(weapon)`.
Для кнопок берётся тот же `Garderob`, что и у существующих кнопок
(`BasicHero_M`, fileID `394657650`), чтобы не менять текущую логику UI.
Существующие кнопки/списки не трогаются.

Если ни один `Garderob` не найден, `WeaponManager` просто работает без
подписки; экипировка доступна через публичный `EquipWeapon`.

## 13. AnimatorOverrideController и клипы

Setup-скрипт создаёт:

- `Assets/Combat/Overrides/Fists.overrideController`:
  - idle → `FIGHTING SWAY`;
  - attack → `JAB`;
  - run остаётся базовым `Sprint`.
- `Assets/Combat/Overrides/Sword.overrideController`:
  - idle → `idle` (Sword);
  - run → `run02_front`;
  - attack → `attack_01`.

Базовый контроллер: `PlayerController.controller`. Оригинальные клипы для
override берутся из базового контроллера (Idle, Sprint, PunchRight).

Временная подмена для `castAnimation`:

- создаётся `AnimatorOverrideController` на базе `baseController`;
- копируются текущие overrides оружия;
- оригинальный `PunchRight` заменяется на `castAnimation`;
- после каста возвращается контроллер оружия.

## 14. Урон и физика

- Кулаки: существующий `Cube` с тегом `DammageObject`, collider включается на
  время атаки. `Weapon.SetHitboxActive` активирует GameObject при
  необходимости и переключает только collider. `WeaponManager` в рантайме
  проверяет наличие kinematic `Rigidbody` на `Cube` и добавляет его, если
  компонента нет (в prefab его нет, в `Game.unity` он уже есть).
- Меч: дочерний `Hitbox` на модели меча, `BoxCollider` trigger, kinematic
  `Rigidbody`, тег `DammageObject`, компонент `AttackHitbox`. Модель меча
  крепится к `R_wrist_joint`.
- Магия: временный `SphereCollider` trigger + kinematic `Rigidbody` + тег
  `DammageObject`, живёт `magicHitboxLifetime`. Существующий
  `DestructibleObject.OnTriggerEnter` уничтожает объект. `DestructibleObject`
  не меняется.
- `Physics.OverlapSphere` в магии используется для обнаружения целей и
  логирования; фактическое уничтожение — через trigger-объём.

## 15. Setup-скрипт и ручная настройка

Setup разделён на два независимых шага, чтобы ассеты можно было создать и
проверить до основного кода:

**Шаг 1 — `Assets/Editor/WeaponAssetSetup.cs`** (меню
`Tools/Combat/1. Create Weapon Assets`):

- папки `Assets/Combat/{Weapons,Abilities,Overrides,UI}`;
- `Fists.overrideController`, `Sword.overrideController`;
- `WeaponData` `Fists.asset`, `Sword.asset`;
- `AbilityData` `FistsJab.asset`, `SwordSlash.asset`, `MagicBurst.asset`
  (иконки/VFX пустые — заполняет пользователь);
- у `Fists.asset` и `Sword.asset` выставляет `useAnimationEvents = true`
  после добавления событий;
- prefab `AbilitySlot.prefab`;
- AnimationEvent `EnableHitbox`/`DisableHitbox` в `JAB` и `attack_01`.
- Зависит только от `WeaponData`, `AbilityData`, `AbilitySlotUI`; не
  трогает сцену и игрока.

**Шаг 2 — `Assets/Editor/WeaponSceneSetup.cs`** (меню
`Tools/Combat/2. Wire Player & Scene`):

- компоненты `WeaponManager`, `AbilitySystem`, `AnimationEventRelay` на
  `Player.prefab` и ссылки;
- `AbilityBarUI` на `Canvas` и ссылки;
- панель с кнопками в `GarderobCanvas`;
- ссылки на `Fists.asset`/`Sword.asset` и `AbilitySlot.prefab`.

После запуска пользователь вручную настраивает:

- `weaponLocalPosition/EulerAngles/Scale` меча;
- `hitboxLocalPosition/EulerAngles/Size` меча;
- иконки способностей;
- `vfxPrefab` магии (из AllIn1VfxToolkit);
- при необходимости — кулдауны, типы, `requiredWeapon`, `castAnimation`.

## 16. Разумные допущения (фиксируются в коде комментариями)

- Кулаки используют существующий `Cube`; отдельный хитбокс на руке не
  создаётся.
- Меч использует `SM_Wep_Sword_01`; локальные трансформы по умолчанию
  нулевые/единичные, пользователь подстроит.
- Магия — мгновенная зона, не снаряд.
- `vfxPrefab`, иконки и `castAnimation` — плейсхолдеры; пользователь
  назначает.
- Способности по умолчанию: «Джеб» (кулаки, Melee), «Удар мечом» (меч,
  Melee), «Магический взрыв» (меч, Magic, `requiredWeapon = Any`).
- UI использует TextMeshPro; панель создаётся в рантайме.
- Вход блокируется при `UiManager.IsOpen`.
- При смене оружия кулдауны сбрасываются.
- Источник истины для хитбокса — `WeaponData.useAnimationEvents`:
  `true` — AnimationEvent, `false` — `PlayerController` по `normalizedTime`.
  Два пути одновременно не работают.
- Новые хитбоксы не наносят урон `HarvestableObject`/`Enemy`; существующий
  `PlayerInteraction` не отключается.
- `WeaponManager` подписывается на все `Garderob` в сцене; кнопки используют
  экземпляр манекена `BasicHero_M`, как и существующие кнопки.
- `Rigidbody` для `Cube` добавляется в рантайме, а не в prefab.

## 17. Проверка

- Компиляция всех скриптов через Roslyn (как в предыдущих задачах):
  `CSC_EXIT=0`, `ERROR_COUNT=0`.
- Проверка YAML `Player.prefab`/`Game.unity` после правок.
- В Unity (пользователь): запустить setup, проверить:
  - кулаки: стойка FIGHTING SWAY, удар JAB, Cube наносит урон;
  - меч: модель в руке, стойка idle, бег run02_front, удар attack_01,
    хитбокс на клинке;
  - способности 1–5, кулдауны, иконки, магия с VFX;
  - кнопки гардероба меняют оружие и набор способностей.
