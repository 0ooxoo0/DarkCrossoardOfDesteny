# Игрок: движение и атака — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Персонаж анимированно ходит на WASD и бьёт кулаком по ЛКМ в сторону курсора, нанося урон врагам вблизи.

**Architecture:** Пересобираем существующий `PlayerController.controller` (GUID сохраняется) в один слой с Blend Tree Idle↔Sprint и стейтом Attack на `PunchRight`. `PlayerController` пишет `Speed`, умеет поворачиваться к точке удара и запускать триггер. `PlayerInteraction` переводится на Input System и наносит урон в конусе после задержки.

**Tech Stack:** Unity 6000.2.10f1, C#, UnityEngine.AI, Input System 1.14.2, Humanoid-анимации HumanLowPoly.

**Spec:** `docs/superpowers/specs/2026-10-22-player-movement-attack-design.md`

## Global Constraints

- Unity 6000.2.10f1; проект открыт в редакторе, сцена `Game`.
- Контроллер: GUID `d72ac30c17265054499cbbf60f69b7ee` не меняется.
- Не трогать `BaseMale.controller`, FBX и их `.meta`.
- Параметры Animator: `Speed` (Float), `Attack` (Trigger). Стейты: `Locomotion`, `Attack`.
- Урон: 25, радиус 2.5 м, конус 150°, задержка удара 0.3 с, кулдаун 0.7 с.
- Бэкапы кладутся вне `Assets` (Unity их не импортирует).
- Проект не под git — коммитов нет, вместо них бэкапы и проверка в Play mode.

## Review Focus

- Ретаргетинг Humanoid-анимаций на модель `BaseMale` может дать искажённые конечности — проверяем визуально в Play mode.
- Движение через `agent.velocity` может не двигать персонажа вне NavMesh — проверяем фактическое изменение позиции.
- Луч из камеры может попасть в коллайдер самого игрока и увести прицел — исключаем слой игрока, есть fallback на плоскость.
- Один клик может нанести урон дважды (несколько коллайдеров/родитель+ребёнок) — `HashSet<IInteractable>` и `GetComponentInParent`.
- Клик во время удара может поставить триггер в очередь и выдать второй удар после анимации — `PlayAttackAnimation()` возвращает `false`, пока стейт Attack активен; кулдаун 0.7 с.

---

### Task 1: Контроллер и префаб

**Files:**
- Backup: `Backups/2026-10-22-player-movement/PlayerController.controller`
- Modify: `Assets/PlayerAnimations/PlayerController.controller` (полная замена содержимого)
- Modify: `Assets/Player.prefab:4919`

**Interfaces:**
- Produces: Animator с параметрами `Speed` (Float), `Attack` (Trigger), стейтами `Locomotion` и `Attack`; слой `Base Layer` с весом 1. Ссылка на контроллер в префабе — GUID `d72ac30c17265054499cbbf60f69b7ee`.

- [ ] **Step 1: Сделать бэкапы**

Скопировать в `Backups/2026-10-22-player-movement/`:
`Assets/PlayerAnimations/PlayerController.controller`, `Assets/PlayerAnimations/PlayerController.controller.meta`, `Assets/Player.prefab`, `Assets/Scripts/Player/PlayerController.cs`, `Assets/Scripts/Drop/PlayerInteraction.cs`.

- [ ] **Step 2: Заменить содержимое `PlayerController.controller`**

Полный YAML — в приложении A. Ключевые значения: `DefaultWeight: 1`; Blend Tree `Speed` 0→`Idle` (`c346a9e2503cda64ba4698db6aa5e707`, fileID `-6206981513316247533`), 1→`Sprint` (`dcacd08f98af5794db5b56a6a66048e7`, fileID `7212731245373533189`); Attack — `PunchRight` (`b4ed12fcea5716a4b88a664cf716cfc6`, fileID `190365492218098281`), `m_Speed: 1.25`; AnyState→Attack по триггеру `Attack`, `m_CanTransitionToSelf: 0`; Attack→Locomotion exit time 0.92.

- [ ] **Step 3: Переключить Animator в `Player.prefab`**

В строке 4919 заменить
`m_Controller: {fileID: 9100000, guid: f551bf2e7708b274785462f5f80d25ff, type: 2}`
на
`m_Controller: {fileID: 9100000, guid: d72ac30c17265054499cbbf60f69b7ee, type: 2}`.

- [ ] **Step 4: Дождаться реимпорта и проверить ошибки**

Подождать 5–10 с, затем проверить `%LOCALAPPDATA%\Unity\Editor\Editor.log` на строки `error CS`, `AnimatorController`, `Could not produce class`. Ожидается: ошибок нет, контроллер импортирован.
В Unity выделить игрока в `Game` и открыть Animator: один слой `Base Layer` с весом 1, параметры `Speed`/`Attack`, стейты `Locomotion`/`Attack`.

---

### Task 2: `PlayerController`

**Files:**
- Modify: `Assets/Scripts/Player/PlayerController.cs`

**Interfaces:**
- Consumes: Animator с параметрами `Speed`, `Attack`.
- Produces:
  - `public void FaceTowards(Vector3 worldPoint, float duration)`
  - `public bool PlayAttackAnimation()`

- [ ] **Step 1: Добавить поля**

```csharp
[SerializeField] private string attackTrigger = "Attack";
private float attackAimTimer;
private Vector3 attackAimPoint;
```

- [ ] **Step 2: Включить запись скорости**

В `UpdateAnimation()` заменить комментарий на `animator.SetFloat(speedParam, Mathf.Clamp01(speed));`.

- [ ] **Step 3: Добавить поворот к точке удара**

`FaceTowards` записывает точку и длительность в `attackAimPoint`/`attackAimTimer`. В начале `HandleRotation()` — если таймер > 0, уменьшать его и поворачивать к `attackAimPoint` (только по Y, `Quaternion.Slerp`, `rotationSpeed`), затем `return`. Приоритет: удар → `aimTarget` → направление движения.

- [ ] **Step 4: Добавить запуск атаки**

```csharp
public bool PlayAttackAnimation()
{
    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
    if (state.IsName("Attack") && state.normalizedTime < 0.95f) return false;

    animator.ResetTrigger(attackTrigger);
    animator.SetTrigger(attackTrigger);
    return true;
}
```

- [ ] **Step 5: Проверить компиляцию**

Подождать реимпорт, проверить `Editor.log` на `error CS`. Ожидается: ошибок нет.

---

### Task 3: `PlayerInteraction`

**Files:**
- Modify: `Assets/Scripts/Drop/PlayerInteraction.cs` (полная замена)

**Interfaces:**
- Consumes: `PlayerController.FaceTowards(Vector3, float)`, `PlayerController.PlayAttackAnimation()`, action `Player.Attack` из `PlayerControls`.
- Produces: урон `IInteractable.TakeDamage(25, hitPoint)` в конусе 150° и радиусе 2.5 м через 0.3 с после клика.

- [ ] **Step 1: Переписать скрипт**

Поля: `attackDamage = 25f`, `attackRange = 2.5f`, `attackAngle = 150f`, `hitDelay = 0.3f`, `attackCooldown = 0.7f`, `playerCamera`, `attackOrigin`. В `Awake` получить `PlayerController`, камеру (fallback `Camera.main`), создать `PlayerControls`. В `OnEnable` подписаться на `controls.Player.Attack.performed`, в `OnDisable` отписаться и отключить.

`OnAttack`: выход, если `Time.time < nextAttackTime`; получить точку прицела; `FaceTowards(point, 0.45f)`; если `PlayAttackAnimation()` вернул `false` — выход; иначе `nextAttackTime = Time.time + attackCooldown`, `StartCoroutine(AttackRoutine())`.

`GetAimPoint`: луч из `playerCamera` через `Mouse.current.position`; маска `~(1 << gameObject.layer)`; при попадании — `hit.point`; иначе пересечение с горизонтальной плоскостью на высоте игрока; fallback — `transform.position + transform.forward * attackRange`.

`AttackRoutine`: `WaitForSeconds(hitDelay)`; `OverlapSphere` с `origin = attackOrigin ?? transform.position + transform.forward * 0.5f`; для каждого коллайдера `GetComponentInParent<IInteractable>()`; пропуск при `dot(forward, toTarget.normalized) < cos(attackAngle/2)`; урон один раз на `IInteractable` (`HashSet`), точка — `col.bounds.ClosestPoint(origin)`; в конце сбросить флаг атаки.

- [ ] **Step 2: Проверить компиляцию**

Подождать реимпорт, проверить `Editor.log` на `error CS`. Ожидается: ошибок нет.

---

### Task 4: Проверка в Play mode и подстройка

**Files:**
- Modify (при необходимости): `Assets/Scripts/Drop/PlayerInteraction.cs`, `Assets/Scripts/Player/PlayerController.cs`, `Assets/PlayerAnimations/PlayerController.controller`

- [ ] **Step 1: `Game` — движение и анимация**

Открыть `Game`, нажать Play. Проверить: без ввода — Idle; WASD — персонаж едет, поворачивается и играет бег; позиция в инспекторе меняется. Записать скриншот.

- [ ] **Step 2: `Game` — удар**

Кликнуть ЛКМ: персонаж поворачивается к курсору, играет PunchRight, кулак доходит до цели; повторный быстрый клик не запускает второй удар поверх первого. Если рядом есть `IInteractable`, в Console появляется строка `... получил урон 25, осталось 75 HP` ровно один раз за клик.

- [ ] **Step 3: `Dungeon 2.0` — урон по врагу**

Остановить Play, открыть `Dungeon 2.0`, нажать Play. Дождаться спавна врагов, подойти к ближайшему, ударить. В Console — одна строка урона на клик, здоровье врага уменьшается. Проверить, что анимации движения работают и здесь.

- [ ] **Step 4: Подстройка**

Если удар не попадает: уменьшить `hitDelay` до 0.2–0.25 или увеличить радиус/угол. Если анимация слишком медленная — поднять `m_Speed` Attack до 1.4. Если ретаргетинг выглядит сломанным — заменить клип на `MeleeAttack_OneHanded` или `PunchLeft`, проверить аватар. Зафиксировать итоговые значения в спеке.

---

## Приложение A: YAML контроллера

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1102 &-2000000000000000200
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Locomotion
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {x: 300, y: 0, z: 0}
  m_IKOnFeet: 1
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {fileID: -2000000000000000400}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!206 &-2000000000000000400
BlendTree:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: LocomotionBlend
  m_Childs:
  - serializedVersion: 2
    m_Motion: {fileID: -6206981513316247533, guid: c346a9e2503cda64ba4698db6aa5e707, type: 3}
    m_Threshold: 0
    m_Position: {x: 0, y: 0}
    m_TimeScale: 1
    m_CycleOffset: 0
    m_DirectBlendParameter: Speed
    m_Mirror: 0
  - serializedVersion: 2
    m_Motion: {fileID: 7212731245373533189, guid: dcacd08f98af5794db5b56a6a66048e7, type: 3}
    m_Threshold: 1
    m_Position: {x: 1, y: 0}
    m_TimeScale: 1
    m_CycleOffset: 0
    m_DirectBlendParameter: Speed
    m_Mirror: 0
  m_BlendParameter: Speed
  m_BlendParameterY: Blend
  m_MinThreshold: 0
  m_MaxThreshold: 1
  m_UseAutomaticThresholds: 0
  m_NormalizedBlendValues: 0
  m_BlendType: 0
--- !u!1102 &-2000000000000000300
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Attack
  m_Speed: 1.25
  m_CycleOffset: 0
  m_Transitions:
  - {fileID: -2000000000000000600}
  m_StateMachineBehaviours: []
  m_Position: {x: 600, y: 0, z: 0}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {fileID: 190365492218098281, guid: b4ed12fcea5716a4b88a664cf716cfc6, type: 3}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1101 &-2000000000000000500
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Attack
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: -2000000000000000300}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.1
  m_TransitionOffset: 0
  m_ExitTime: 0.75
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
--- !u!1101 &-2000000000000000600
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: 
  m_Conditions: []
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: -2000000000000000200}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.15
  m_TransitionOffset: 0
  m_ExitTime: 0.92
  m_HasExitTime: 1
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
--- !u!1107 &-2000000000000000100
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Base Layer
  m_ChildStates:
  - serializedVersion: 1
    m_State: {fileID: -2000000000000000200}
    m_Position: {x: 300, y: 0, z: 0}
  - serializedVersion: 1
    m_State: {fileID: -2000000000000000300}
    m_Position: {x: 600, y: 0, z: 0}
  m_ChildStateMachines: []
  m_AnyStateTransitions:
  - {fileID: -2000000000000000500}
  m_EntryTransitions: []
  m_StateMachineTransitions: {}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {x: 50, y: 30, z: 0}
  m_EntryPosition: {x: 50, y: 140, z: 0}
  m_ExitPosition: {x: 800, y: 120, z: 0}
  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}
  m_DefaultState: {fileID: -2000000000000000200}
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: PlayerController
  serializedVersion: 5
  m_AnimatorParameters:
  - m_Name: Speed
    m_Type: 1
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  - m_Name: Attack
    m_Type: 9
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {fileID: -2000000000000000100}
    m_Mask: {fileID: 0}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 1
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {fileID: 9100000}
```
