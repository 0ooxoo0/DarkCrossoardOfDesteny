# Система оружия и способностей — план реализации

> **Для исполнителя:** обязательный скилл — `superpowers:executing-plans` (native) или
> `superpowers:subagent-driven-development`. Шаги отмечаются чекбоксами.

**Goal:** Добавить в существующий Unity-проект data-driven оружие (кулаки/меч),
хитбоксы через `DammageObject`, 5 слотов способностей с кулдаунами, UI и магией,
не ломая гардероб, `DestructibleObject`, `PlayerController` и базовый Animator.

**Architecture:** `WeaponData`/`AbilityData` — ScriptableObject-контракты.
`WeaponManager` на игроке меняет `AnimatorOverrideController`, создаёт модель меча
и его trigger-хитбокс, делегирует способности в `AbilitySystem`. Источник истины
хитбокса — `WeaponData.useAnimationEvents`: AnimationEvent либо `PlayerController`
по `normalizedTime`, но не оба сразу. Setup разделён на два Editor-скрипта:
ассеты (шаг 1) и обвязка prefab/сцены (шаг 2).

**Tech Stack:** Unity 6000.2.10f1, Assembly-CSharp (без asmdef), uGUI,
TextMeshPro, Input System (`Keyboard.current`), AnimatorOverrideController.

**Spec:** `docs/superpowers/specs/2026-11-09-weapon-ability-system-design.md`

## Global Constraints

- `DestructibleObject.cs`, `PlayerInteraction.cs`, `PlayerController.controller` —
  не изменять.
- `Garderob.cs` — только добавление `OnWeaponChanged` и `EquipWeapon`; ничего не
  переименовывать.
- Новый урон — только по `DestructibleObject` через тег `DammageObject`.
  `HarvestableObject`/`Enemy`/`IInteractable` не трогать.
- `WeaponData.useAnimationEvents = true` по умолчанию. Флаг проверяется и в
  `PlayerController.SetWeaponHitbox`, и в `WeaponManager.SetHitboxActive`.
- `WeaponManager.EquipWeapon`: `if (weapon == currentWeapon) return;`.
- `Rigidbody` на `Cube`: не добавлять, если уже есть; добавляемый —
  `isKinematic = true`, `useGravity = false`; существующий в `Game.unity` не менять.
- Melee-способность переиспользует штатный триггер `Attack` и хитбокс оружия.
- Setup: шаг 1 (`WeaponAssetSetup`) можно запускать до основного кода; шаг 2
  (`WeaponSceneSetup`) — после.
- UI-панель способностей создаётся в рантайме; prefab слота — отдельный ассет.
- Ввод способностей блокируется при `UiManager.IsOpen` и `Time.timeScale == 0`.
- Проект без Unity Test Framework: цикл проверки задачи = компиляция Roslyn
  (`dotnet build Assembly-CSharp-Editor.csproj`, exit 0, 0 строк `error CS`) +
  целевая ручная проверка в Play mode.

## Review Focus

1. **Двойное событие от двух `Garderob`.** Повторный `EquipWeapon` того же оружия
   не должен сбрасывать Animator и кулдауны — early-return в `WeaponManager`.
2. **Оба пути хитбокса одновременно.** При `useAnimationEvents = true`
   `PlayerController` не должен вызывать `SetHitboxActive`; при `false` —
   AnimationEvent не должен включать хитбокс. Проверяется в двух местах.
3. **Trigger без Rigidbody.** Меч/магия не нанесут урон, если на trigger-объекте
   нет kinematic `Rigidbody`. Это проверяется при создании хитбоксов.
4. **Animator застревает в касте.** После `PlayCastAnimation` контроллер оружия
   возвращается, `SuppressHitbox` снимается, персонаж снова в idle/run.
5. **`run02_front` не зациклен.** В импорте клипа выставляется `loopTime = true`,
   иначе бег с мечом замирает после первого проигрывания.

---

### Task 1: Контракты данных и слот UI

**Files:**
- Create: `Assets/Scripts/Combat/WeaponData.cs`
- Create: `Assets/Scripts/Combat/AbilityData.cs`
- Create: `Assets/Scripts/UI/AbilitySlotUI.cs`

**Interfaces:**
- Produces: `enum WeaponType { Any, Fists, Sword }`, `enum AbilityType { Melee, Magic }`.
- Produces: `WeaponData` поля `weaponId`, `displayName`, `animatorOverride`,
  `type`, `abilities`, `useAnimationEvents = true`, `damageTags =
  {"DammageObject"}`, `weaponModelPrefab`, `weaponLocalPosition`,
  `weaponLocalEulerAngles`, `weaponLocalScale = Vector3.one`,
  `hitboxLocalPosition`, `hitboxLocalEulerAngles`, `hitboxSize =
  (0.15, 0.15, 1.2)`, свойство `string PrimaryDamageTag`.
- Produces: `AbilityData` поля `abilityId`, `displayName`, `icon`, `hotkey`,
  `type`, `cooldown`, `vfxPrefab`, `castAnimation`, `requiredWeapon`,
  `damageTags = {"DammageObject"}`, `damageLayers = ~0`, `magicRange`,
  `magicRadius`, `magicSpawnDistance`, `vfxDelay`, `vfxLifetime`,
  `magicHitboxLifetime`, свойство `string PrimaryDamageTag`.
- Produces: `AbilitySlotUI.SetVisualReferences(Image, Image, TMP_Text, TMP_Text)`,
  `SetAbility(AbilityData, int, KeyCode)`, `SetCooldown(float, float)`,
  `SetAvailable(bool)`.

- [ ] **Step 1: Создать `WeaponData.cs`**

Глобальное пространство имён, `[CreateAssetMenu(menuName = "Combat/Weapon Data")]`.
`PrimaryDamageTag` возвращает первый непустой тег из `damageTags`, иначе
`"DammageObject"`. Комментарий: список оставлен для расширения, сейчас поведение
только `DammageObject`.

- [ ] **Step 2: Создать `AbilityData.cs`**

Глобальное пространство имён, `[CreateAssetMenu(menuName = "Combat/Ability Data")]`.
`PrimaryDamageTag` — та же логика. `damageLayers` — `LayerMask` со значением `~0`.

- [ ] **Step 3: Создать `AbilitySlotUI.cs`**

`[SerializeField] private Image icon;`, `cooldownOverlay`; `TMP_Text keyLabel`,
`cooldownLabel`. `SetVisualReferences` присваивает их. `SetAbility`:
иконка из `ability.icon`; если null — цвет Melee `(0.75, 0.2, 0.2)`, Magic
`(0.2, 0.35, 0.9)`, пустой слот `(0.15, 0.15, 0.15)`; номер клавиши.
`SetCooldown`: `fillAmount = total > 0 ? remaining / total : 0`, текст секунд.
`SetAvailable`: приглушает иконку (alpha 0.35), если недоступна.

- [ ] **Step 4: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 строк `error CS`. Если Unity ещё не добавил новые файлы в
сгенерированный `.csproj`, временно добавить их `<Compile Include="..."/>` в
`Assembly-CSharp.csproj` перед сборкой.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Combat/WeaponData.cs Assets/Scripts/Combat/AbilityData.cs Assets/Scripts/UI/AbilitySlotUI.cs
git commit -m "feat(combat): add weapon and ability data contracts"
```

---

### Task 2: Шаг 1 — создание ассетов (`WeaponAssetSetup`)

**Files:**
- Create: `Assets/Editor/WeaponAssetSetup.cs`
- Generated (не коммитить до запуска в Unity): `Assets/Combat/**`

**Interfaces:**
- Consumes: `WeaponData`, `AbilityData`, `AbilitySlotUI` из Task 1.
- Produces: ассеты `Fists.asset`, `Sword.asset`, `FistsJab.asset`,
  `SwordSlash.asset`, `MagicBurst.asset`, `Fists.overrideController`,
  `Sword.overrideController`, `AbilitySlot.prefab`.

**Точные пути:**
- Base controller: `Assets/PlayerAnimations/PlayerController.controller`
- Fists idle: `Assets/Unity Technologies/Combat animations - Kickboxing and Muay Thai V1/Animations/AmineSAADI@FIGHTING_SWAY.fbx`
- Fists attack: `.../AmineSAADI@JAB 01.fbx`
- Sword idle: `Assets/Unity Technologies/Sword_sheath_AnimSet/Animation/Humanoid/idle.fbx`
- Sword run: `.../run02_front.fbx`
- Sword attack: `.../attack_01.fbx`
- Sword model: `Assets/Assets/DarkFantasy/PolygonDarkFantasy/Prefabs/Weapons/SM_Wep_Sword_01.prefab`

- [ ] **Step 1: Создать меню и папки**

`[MenuItem("Tools/Combat/1. Create Weapon Assets")]`. Создать
`Assets/Combat/{Weapons,Abilities,Overrides,UI}` через `AssetDatabase`.

- [ ] **Step 2: Настроить импорт клипов**

Один helper `ConfigureClip(fbxPath, clipName, loopTime, addHitboxEvents)`:
читает `ModelImporter.clipAnimations` (если пусто —
`defaultClipAnimations`), находит клип по имени, выставляет `loopTime`, при
необходимости заменяет события `EnableHitbox`/`DisableHitbox` на
`clip.length * 0.15f` и `clip.length * 0.55f`, сохраняет настройки и делает
`SaveAndReimport()`.

Вызвать:
- `FIGHTING SWAY`: loop `true`, без событий.
- `JAB`: loop `false`, с событиями.
- `idle` (Sword): loop `true`, без событий.
- `run02_front`: loop `true`, без событий.
- `attack_01`: loop `false`, с событиями.

- [ ] **Step 3: Создать/обновить override-контроллеры**

`CreateOrUpdateOverride(path, baseController, Dictionary<string, AnimationClip>)`:
загрузить существующий `AnimatorOverrideController` или создать
`new AnimatorOverrideController(baseController)`; для каждой пары
`(originalName, replacement)` найти клип в `baseController.animationClips` по
имени и присвоить `overrideController[original] = replacement`.

- `Fists.overrideController`: `Idle → FIGHTING SWAY`, `PunchRight → JAB`,
  `Sprint` не менять.
- `Sword.overrideController`: `Idle → idle`, `Sprint → run02_front`,
  `PunchRight → attack_01`.

- [ ] **Step 4: Создать/обновить `AbilityData`**

- `FistsJab.asset`: id `fists_jab`, имя «Джеб», Melee, cooldown `0.8`,
  hotkey `Alpha1`, requiredWeapon `Fists`, damageTags `{"DammageObject"}`.
- `SwordSlash.asset`: id `sword_slash`, имя «Удар мечом», Melee, cooldown `1.2`,
  hotkey `Alpha1`, requiredWeapon `Sword`.
- `MagicBurst.asset`: id `magic_burst`, имя «Магический взрыв», Magic,
  cooldown `3`, hotkey `Alpha2`, requiredWeapon `Any`, `magicRange 2.5`,
  `magicRadius 2`, `magicSpawnDistance 1.5`, `vfxDelay 0.2`,
  `vfxLifetime 2`, `magicHitboxLifetime 0.2`, `damageLayers ~0`.
  `icon`, `vfxPrefab`, `castAnimation` — null (заполняет пользователь).

- [ ] **Step 5: Создать/обновить `WeaponData`**

- `Fists.asset`: id `fists`, имя «Кулаки», type `Fists`, override
  `Fists.overrideController`, abilities `[FistsJab]`, model null,
  `useAnimationEvents = true`, damageTags `{"DammageObject"}`.
- `Sword.asset`: id `sword`, имя «Меч», type `Sword`, override
  `Sword.overrideController`, abilities `[SwordSlash, MagicBurst]`, model
  `SM_Wep_Sword_01`, `useAnimationEvents = true`, hitboxSize `(0.15,0.15,1.2)`,
  трансформы `(0,0,0)` и scale `(1,1,1)`.

- [ ] **Step 6: Создать `AbilitySlot.prefab`**

Если ассета нет: root `AbilitySlot` 80×80 с `Image` (тёмный фон),
`CanvasRenderer`, `AbilitySlotUI`. Дети:
`Icon` (Image, stretch, padding 6), `CooldownOverlay` (Image, stretch,
чёрный alpha 0.65, `Image.Type.Filled`, `FillMethod.Radial360`,
`fillOrigin = Top`, `raycastTarget = false`), `KeyLabel` (TMP, верхний левый,
fontSize 18), `CooldownLabel` (TMP, центр, fontSize 22, bold). Вызвать
`slotUI.SetVisualReferences(...)`, сохранить `PrefabUtility.SaveAsPrefabAsset`,
удалить временный объект.

- [ ] **Step 7: Сохранить и проверить**

`AssetDatabase.SaveAssets(); AssetDatabase.Refresh();` Лог: пути созданных
ассетов, количество событий в JAB/attack_01, `loopTime` run02_front.

- [ ] **Step 8: Проверить компиляцию Editor-сборки**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 9: Ручная проверка шага 1 (пользователь)**

В Unity: `Tools/Combat/1. Create Weapon Assets`. Ожидается: папки и 8 ассетов
существуют; в `JAB` и `attack_01` по 2 события (`EnableHitbox` 15%,
`DisableHitbox` 55%); `run02_front` зациклен. Повторный запуск не создаёт
дубликаты.

- [ ] **Step 10: Commit**

```powershell
git add Assets/Editor/WeaponAssetSetup.cs
git commit -m "feat(combat): add weapon asset setup step 1"
```

---

### Task 3: Боевое ядро (`WeaponManager`, `AbilitySystem`, хитбоксы)

**Files:**
- Create: `Assets/Scripts/Combat/AttackHitbox.cs`
- Create: `Assets/Scripts/Combat/AnimationEventRelay.cs`
- Create: `Assets/Scripts/Combat/WeaponManager.cs`
- Create: `Assets/Scripts/Combat/AbilitySystem.cs`
- Create: `Assets/Scripts/Combat/WeaponEquipButton.cs`

**Interfaces:**
- Produces: `WeaponManager.CurrentWeapon`, `SuppressHitbox`, `UseAnimationEvents`,
  `EquipWeapon(WeaponData)`, `SetHitboxActive(bool, bool fromAnimationEvent =
  false)`, `EnableHitbox()`, `DisableHitbox()`, `PlayCastAnimation(AnimationClip)`.
- Produces: `AbilitySystem.SlotCount = 5`, события `OnAbilitiesChanged`,
  `OnAbilityActivated`, `SetAbilities(List<AbilityData>)`, `GetAbility(int)`,
  `GetCooldownRemaining(int)`, `GetCooldownFraction(int)`,
  `IsAbilityAvailable(int)`, `CancelCurrentAbility()`.
- Produces: `AttackHitbox.SetActive(bool)`, `IsActive`;
  `AnimationEventRelay.EnableHitbox()/DisableHitbox()`;
  `WeaponEquipButton.Equip()`.

- [ ] **Step 1: Создать `AttackHitbox.cs`**

`[SerializeField] private Collider hitboxCollider;`; `Awake` берёт
`GetComponent<Collider>()`, если ссылка пуста. `SetActive` только переключает
`collider.enabled`. `OnTriggerEnter` не реализовывать: урон обрабатывает
`DestructibleObject` по тегу. Комментарий про обязательный kinematic `Rigidbody`.

- [ ] **Step 2: Создать `AnimationEventRelay.cs`**

Ссылка на `WeaponManager`. `EnableHitbox`/`DisableHitbox` вызывают
`weaponManager.EnableHitbox()`/`DisableHitbox()`, но только если
`weaponManager != null && weaponManager.UseAnimationEvents`. Комментарий: это
вторая проверка флага, чтобы при `useAnimationEvents = false` события не
включали хитбокс.

- [ ] **Step 3: Создать `WeaponManager.cs`**

Поля: `animator`, `playerController`, `abilitySystem`, `fistWeapon`,
`baseController`, `baseAttackClip`, `rightHand`, `startingWeapon`; приватные
`garderobs`, `currentWeapon`, `weaponModelInstance`, `swordHitbox`,
`currentWeaponController`, `castOverrideInstance`, `castRestoreRoutine`,
`suppressHitbox`.

`Awake`: найти компоненты, `baseController = animator.runtimeAnimatorController`,
`baseAttackClip = FindClipByName(baseController, "PunchRight")`, `rightHand =
animator.GetBoneTransform(HumanBodyBones.RightHand)`, найти `fistWeapon` среди
`GetComponentsInChildren<Weapon>(true)` (приоритет тегу `DammageObject`),
`EnsureFistRigidbody()`.

`Start`: `FindObjectsByType<Garderob>(FindObjectsInactive.Include,
FindObjectsSortMode.None)`, подписаться на `OnWeaponChanged` каждого; если
`startingWeapon != null` — `EquipWeapon(startingWeapon)`.

`OnDestroy`: отписаться, остановить корутин, уничтожить модель и cast override.

`EquipWeapon`: early-return при равенстве; остановить каст/restore; вернуть
контроллер оружия; `abilitySystem.CancelCurrentAbility()`; установить
`animator.runtimeAnimatorController = weapon.animatorOverride ?? baseController`;
`ClearWeaponModel()`; для `WeaponType.Sword` — `CreateSword(weapon)` (модель под
`rightHand`, локальные трансформы из данных, отключить все `Collider` модели,
создать дочерний `Hitbox` с trigger `BoxCollider`, tag из `PrimaryDamageTag`,
kinematic `Rigidbody`, `AttackHitbox`, выключить; `Cube` выключить); иначе —
активировать `Cube`, выключить collider, prior `EnsureFistRigidbody`;
`abilitySystem.SetAbilities(weapon.abilities)`; `ApplyHitbox(false)`;
`playerController.ResetHitboxState()`; сохранить `currentWeaponController`.

`SetHitboxActive(active, fromAnimationEvent)`: при `suppressHitbox` —
`ApplyHitbox(false)`; при `currentWeapon != null` сравнить
`currentWeapon.useAnimationEvents` с `fromAnimationEvent` и игнорировать
неактивный путь; при `currentWeapon == null` игнорировать события. `ApplyHitbox`
для меча выключает `Cube` и переключает `swordHitbox`, иначе выключает
`swordHitbox` и переключает `fistWeapon`.

`PlayCastAnimation`: если `clip == null` — false; собрать временный
`AnimatorOverrideController` на базе `baseController`, скопировать текущие
overrides оружия (через `weaponOverride[baseClip]`), заменить `baseAttackClip` на
`clip`; назначить, вызвать `playerController.PlayAttackAnimation()`; при неудаче
вернуть контроллер; иначе запустить корутин восстановления через
`clip.length / 1.25f + 0.15f`, затем вернуть `currentWeaponController`,
`ResetHitboxState()`, `ResumeMovement()`, уничтожить временный override.

`EnsureFistRigidbody`: если на `Cube` нет `Rigidbody` — добавить и выставить
`isKinematic = true`, `useGravity = false`; существующий не трогать.

- [ ] **Step 4: Создать `AbilitySystem.cs`**

`[Serializable] class AbilitySlot { AbilityData ability; float cooldownEnd; }`,
массив из 5, `EnsureSlots()`. `Awake`: найти `WeaponManager`, `PlayerController`,
`UiManager` (`FindFirstObjectByType<UiManager>()`), `EnsureSlots`.

`Update`: выход при `Keyboard.current == null`, `Time.timeScale <= 0`,
`uiManager != null && uiManager.IsOpen`; для каждого слота взять
`Key.Digit1..Digit5` (хоткей `Alpha1..Alpha5` маппится на тот же Digit) и при
`wasPressedThisFrame` вызвать `TryActivate(i)`.

`SetAbilities`: скопировать до 5, сбросить кулдауны, вызвать
`OnAbilitiesChanged`. `TryActivate`: слот занят, кулдаун истёк, оружие
совместимо, нет активной способности; запустить и выставить кулдаун,
`OnAbilityActivated`.

Melee: `castAnimation != null` → `weaponManager.PlayCastAnimation`, иначе
`playerController.PlayAttackAnimation`; корутин ждёт, пока
`playerController.IsAttacking == false` (таймаут 5 с). Свой хитбокс не создаётся.

Magic: `SuppressHitbox = true`; `StopMovement`; при наличии `castAnimation` —
`PlayCastAnimation`; `WaitForSeconds(vfxDelay)`; заспавнить `vfxPrefab` (если
задан) с lifetime `vfxLifetime`; создать временный `SphereCollider` trigger +
kinematic `Rigidbody` + tag `PrimaryDamageTag`, радиус `magicRadius`, уничтожить
через `magicHitboxLifetime`; `Physics.OverlapSphere` с `damageLayers` — только
для лога/диагностики; дождаться конца каста; `SuppressHitbox = false`,
`ResumeMovement()`.

`CancelCurrentAbility`: остановить корутин, `SuppressHitbox = false`,
`ResumeMovement`.

Геттеры UI: `GetAbility`, `GetCooldownRemaining = max(0, cooldownEnd - Time.time)`,
`GetCooldownFraction`, `IsAbilityAvailable` (слот, кулдаун, совместимость).

- [ ] **Step 5: Создать `WeaponEquipButton.cs`**

Ссылки `Garderob garderob`, `WeaponData weapon`. `Equip()`: если `garderob`
null — `FindFirstObjectByType<Garderob>(FindObjectsInactive.Include)`; вызвать
`garderob.EquipWeapon(weapon)`.

- [ ] **Step 6: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 7: Commit**

```powershell
git add Assets/Scripts/Combat/AttackHitbox.cs Assets/Scripts/Combat/AnimationEventRelay.cs Assets/Scripts/Combat/WeaponManager.cs Assets/Scripts/Combat/AbilitySystem.cs Assets/Scripts/Combat/WeaponEquipButton.cs
git commit -m "feat(combat): add weapon manager and ability system"
```

---

### Task 4: Минимальные правки существующих файлов

**Files:**
- Modify: `Assets/Garderob.cs` (только добавление)
- Modify: `Assets/Scripts/Player/PlayerController.cs`
- Modify: `Assets/Scripts/Player/Weapon.cs`

**Interfaces:**
- Produces: `Garderob.OnWeaponChanged`, `Garderob.EquipWeapon(WeaponData)`.
- Produces: `PlayerController.IsAttacking`, `PlayerController.ResetHitboxState()`.
- Produces: `Weapon.SetHitboxActive(bool)` — включает GameObject при `true`,
  переключает только collider.

- [ ] **Step 1: `Garderob.cs` — добавить event и метод**

Сразу после `public class Garderob : MonoBehaviour {`:

```csharp
public event Action<WeaponData> OnWeaponChanged;

public void EquipWeapon(WeaponData weapon)
{
    OnWeaponChanged?.Invoke(weapon);
}
```

Ничего больше не менять; существующие поля/методы не переименовывать.

- [ ] **Step 2: `PlayerController.cs` — поле, свойство, сброс**

После `[SerializeField] private Weapon weapon;` добавить
`[SerializeField] private WeaponManager weaponManager;`.
Добавить `public bool IsAttacking => isAttacking;` и:

```csharp
public void ResetHitboxState()
{
    hitboxActive = false;
    if (weaponManager == null && weapon != null) weapon.SetHitboxActive(false);
}
```

- [ ] **Step 3: `PlayerController.SetWeaponHitbox` — проверка флага**

Заменить тело на:

```csharp
private void SetWeaponHitbox(bool active)
{
    if (weaponManager != null && weaponManager.UseAnimationEvents) return;
    if (hitboxActive == active) return;
    hitboxActive = active;
    if (weaponManager != null) weaponManager.SetHitboxActive(active);
    else if (weapon != null) weapon.SetHitboxActive(active);
}
```

Комментарий: при `useAnimationEvents = true` путь `PlayerController` полностью
отключён; хитбоксом управляет `AnimationEventRelay`.

- [ ] **Step 4: `Weapon.cs` — не выключать GameObject при `false`**

Заменить `SetHitboxActive` на:

```csharp
public void SetHitboxActive(bool active)
{
    if (hitboxCollider == null) hitboxCollider = GetComponent<Collider>();
    if (active && !gameObject.activeSelf) gameObject.SetActive(true);
    if (hitboxCollider != null) hitboxCollider.enabled = active;
}
```

Комментарий: `false` только выключает collider, чтобы `Cube` можно было
активировать для кулаков и компонент не пропадал; для меча `WeaponManager`
отдельно выключает GameObject.

- [ ] **Step 5: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Garderob.cs Assets/Scripts/Player/PlayerController.cs Assets/Scripts/Player/Weapon.cs
git commit -m "feat(combat): integrate weapon manager with player and wardrobe"
```

---

### Task 5: UI панели способностей

**Files:**
- Create: `Assets/Scripts/UI/AbilityBarUI.cs`

**Interfaces:**
- Consumes: `AbilitySystem` (Task 3), `AbilitySlotUI` (Task 1).
- Produces: компонент `AbilityBarUI` с полями `abilitySystem`, `slotPrefab`,
  `targetCanvas`.

- [ ] **Step 1: Создать `AbilityBarUI.cs`**

`Start`: найти `AbilitySystem` (`FindFirstObjectByType`), `targetCanvas` =
`GetComponent<Canvas>()` или найти; построить панель `AbilityBar` под Canvas
(anchor bottom-center, `anchoredPosition (0, 24)`, `HorizontalLayoutGroup`,
`ContentSizeFitter`, полупрозрачный фон, `raycastTarget = false`); создать 5
слотов из `slotPrefab`, иначе — fallback-слоты кодом (`Image` + `Image`-оверлей
`Filled/Radial360` + 2 TMP-текста) и вызвать `SetVisualReferences`.
Подписаться на `OnAbilitiesChanged`, вызвать `RefreshAbilities()`.

`RefreshAbilities`: для каждого слота `SetAbility(GetAbility(i), i, hotkey)`.

`Update`: для каждого слота `SetCooldown(GetCooldownRemaining(i),
ability.cooldown)` и `SetAvailable(IsAbilityAvailable(i))`.

`OnDestroy`: отписаться.

- [ ] **Step 2: Проверить компиляцию**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 3: Commit**

```powershell
git add Assets/Scripts/UI/AbilityBarUI.cs
git commit -m "feat(ui): add runtime ability bar"
```

---

### Task 6: Шаг 2 — обвязка игрока и сцены (`WeaponSceneSetup`)

**Files:**
- Create: `Assets/Editor/WeaponSceneSetup.cs`
- Modified by script (пользователь запускает): `Assets/Player.prefab`,
  `Assets/Scenes/Game.unity`

**Interfaces:**
- Consumes: все runtime-скрипты, ассеты шага 1.
- Produces: компоненты `WeaponManager`, `AbilitySystem`, `AnimationEventRelay` на
  `Player.prefab`; `AbilityBarUI` на `Canvas`; `WeaponPanel` с двумя кнопками в
  `GarderobCanvas`.

- [ ] **Step 1: Создать меню и проверку ассетов**

`[MenuItem("Tools/Combat/2. Wire Player & Scene")]`. Загрузить
`Fists.asset`, `Sword.asset`, `AbilitySlot.prefab`; если чего-то нет —
`EditorUtility.DisplayDialog` с просьбой сначала выполнить шаг 1 и `return`.

- [ ] **Step 2: Обновить `Player.prefab`**

`PrefabUtility.LoadPrefabContents("Assets/Player.prefab")`; найти
`PlayerController`, `Animator`, `Weapon` (приоритет тегу `DammageObject`).
Добавить (если нет) `WeaponManager`, `AbilitySystem`, `AnimationEventRelay`.
Через `SerializedObject` назначить:
- `WeaponManager`: `animator`, `playerController`, `abilitySystem`, `fistWeapon`,
  `baseController` (PlayerController.controller), `baseAttackClip` (клип
  `PunchRight` из base controller), `startingWeapon` = Fists.asset.
- `AbilitySystem`: `weaponManager`, `playerController`.
- `AnimationEventRelay`: `weaponManager`.
- `PlayerController`: `weaponManager`.

Сохранить `PrefabUtility.SaveAsPrefabAsset`, выгрузить contents.
`Rigidbody` на `Cube` в prefab не добавлять.

- [ ] **Step 3: Обновить `Game.unity`**

Если активна не `Game.unity` — сохранить текущие сцены и открыть её. Найти
`Canvas` (имя `Canvas`), добавить `AbilityBarUI`, назначить `slotPrefab`,
`targetCanvas`, `abilitySystem` (если компонент уже виден в сцене). Найти
`GarderobCanvas`, создать (если нет) `WeaponPanel` в левом верхнем углу:
`VerticalLayoutGroup`, `ContentSizeFitter`, полупрозрачный фон, две кнопки
`FistsButton`/`SwordButton` 170×44 с TMP-текстами «Кулаки»/«Меч».
На каждую кнопку добавить `WeaponEquipButton`, назначить `garderob` (экземпляр
`BasicHero_M`, иначе первый найденный) и `weapon` (Fists/Sword), добавить
persistent listener через
`UnityEventTools.AddPersistentListener(button.onClick, equipButton.Equip)`.

- [ ] **Step 4: Сохранить сцену и проверить компиляцию**

`EditorSceneManager.MarkSceneDirty`, `SaveScene`, `AssetDatabase.SaveAssets`.
Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 5: Ручная проверка шага 2 (пользователь)**

В Unity: `Tools/Combat/2. Wire Player & Scene`. Ожидается: на `Player.prefab`
три новых компонента и ссылки; на `Canvas` — `AbilityBarUI`; в
`GarderobCanvas` — `WeaponPanel` с двумя кнопками; кнопки меняют оружие.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Editor/WeaponSceneSetup.cs
git commit -m "feat(combat): add scene wiring setup step 2"
```

---

### Task 7: Финальная проверка

**Files:**
- Verify only.

- [ ] **Step 1: Полная компиляция**

Run: `dotnet build Assembly-CSharp-Editor.csproj -v q --nologo`
Expected: exit 0, 0 `error CS`.

- [ ] **Step 2: Проверить неприкосновенность**

Run: `git diff -- Assets/Scripts/DestructibleObject.cs Assets/Scripts/Drop/PlayerInteraction.cs Assets/PlayerAnimations/PlayerController.controller`
Expected: пустой вывод.

- [ ] **Step 3: Ручной чек-лист в Play mode**

- Кулаки: стойка `FIGHTING SWAY`, атака `JAB`, `Cube` наносит урон
  `DestructibleObject`.
- Меч: модель в правой руке, стойка `idle`, бег `run02_front` (зациклен),
  атака `attack_01`, хитбокс на клинке наносит урон.
- Способности 1–5: «Джеб» у кулаков, «Удар мечом» и «Магический взрыв» у меча;
  кулдауны и иконки обновляются; магия создаёт VFX и временную зону.
- Магия: после каста Animator возвращается в idle/движение, персонаж не
  застревает.
- Гардероб: кнопки «Кулаки»/«Меч» переключают оружие; повторное нажатие того же
  оружия не сбрасывает аниматор/кулдауны.
- `useAnimationEvents = true`: хитбокс открывается только на 15–55% клипа;
  при `false` — только по `PlayerController`.

- [ ] **Step 4: Commit сгенерированных ассетов и сцены (после проверки пользователем)**

```powershell
git add Assets/Combat Assets/Player.prefab Assets/Scenes/Game.unity
git commit -m "feat(combat): generate weapon assets and wire scene"
```

---

## Порядок применения для пользователя

1. Дождаться компиляции скриптов в Unity (или выполнить `dotnet build`).
2. `Tools/Combat/1. Create Weapon Assets` — создать и проверить ассеты.
3. Убедиться, что в `JAB`/`attack_01` есть события, `run02_front` зациклен.
4. `Tools/Combat/2. Wire Player & Scene` — обвязать prefab и сцену.
5. Войти в Play mode и пройти чек-лист Task 7.
6. Подстроить в инспекторе: позицию/поворот/масштаб меча, параметры хитбокса
   клинка, иконки способностей, `vfxPrefab` магии.
