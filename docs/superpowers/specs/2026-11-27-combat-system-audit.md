# Аудит боевой системы (игрок, враги, урон, фидбек)

Дата: 2026-11-27
Область: `Assets/Scripts/Combat/**`, `Assets/Scripts/CombatSystem/**`, `Assets/Scripts/Enemy/**`,
`Assets/Scripts/Player/**`, `Assets/Scripts/Drop/**`, `Assets/Scripts/UI/{DamagePopup,AbilityBarUI}.cs`,
префабы `Assets/Player.prefab`, `Assets/Prefabs/Enemies/EnemyCombat.prefab`, сцена `Assets/Scenes/Game.unity`,
контроллеры `Assets/Animations/Combat/{PlayerCombat,EnemyCombat}.controller`, данные `Assets/Combat/**`.

Цель: описать, как бой работает сейчас, что из этого видит и чувствует игрок, какие места сломаны
или вводят в заблуждение, и что чинить в первую очередь.

---

## 1. Краткий вывод

Боевая система **технически собрана и в сцене `Game` работает**: игрок бьёт мечом/кулаками,
враги (`EnemyCombat`) агрятся, бьют, блокируют, получают урон, мигают, отлетают, умирают.
Но как **игровой цикл** она недоделана:

1. Магия не наносит урона и не имеет визуала — нажатие 2 либо блокируется (нужен Дао Огня),
   либо не делает ничего.
2. Урон по врагам и урон по ресурсам/животным идёт **двумя разными путями**: видимый хитбокс
   бьёт только `IDamageable` (враги), а ресурсы/животные получают урон от невидимой сферы
   `PlayerInteraction` через 0.3 с — независимо от анимации, хитбокса и стен.
3. Убийство врага не даёт **ничего**: ни лута, ни опыта, ни эффекта добивания. Труп исчезает
   через 6 секунд. Врагов в сцене два, респавна нет.
4. Смерть игрока — это **мгновенный тихий респавн** на том же алтаре: без экрана смерти,
   без анимации, без сообщения. Игрок теряет 25 % опыта ступени и может этого не заметить.
5. У врагов **нет полосы здоровья**, в бою **нет ни одного звука**, цифры урона одинаковые
   и когда бьёшь ты, и когда бьют тебя.

То есть «подраться» можно, но «понять, что происходит, и захотеть драться ещё» — почти нельзя.

---

## 2. Как бой устроен сейчас

### 2.1 Игрок: ввод → анимация → хитбокс → урон

```
LMB (PlayerControls.Player.Attack)
  └─ PlayerController.OnAttackPressed
       ├─ PlayerInteraction.CanStartAttack  (проверка UI/лута/блок-слоёв)
       ├─ BufferAttack()                    (буфер 0.35 с)
       └─ TryAttack()
            ├─ StartAttack(0)              → Animator: Attack + AttackIndex
            └─ AttackStarted               → PlayerInteraction.AttackRoutine (см. 2.5)

Animator (PlayerCombat.controller)
  └─ CombatAnimationWindow (StateMachineBehaviour на атакующих стейтах)
       ├─ OnAttackWindowOpen  → PlayerController.OnAttackWindowOpen
       │                          → WeaponManager.SetHitboxActive(true, true)
       └─ OnAttackWindowClose → ...SetHitboxActive(false, true)

WeaponManager
  ├─ кулаки: CombatHitbox на объекте Weapon (fists)
  └─ меч:    при экипировке создаётся дочерний "Hitbox" c BoxCollider + CombatHitbox

CombatHitbox (FixedUpdate overlap + OnTriggerEnter/Stay)
  ├─ ищет IDamageable в родителях коллайдера
  ├─ проверяет ICombatBlockable (блок)
  ├─ target.TakeDamage(DamageInfo)
  └─ CombatEvents.Report(CombatImpact)  → CombatFeedback / EnemyHitReaction
```

Ключевые числа (сцена `Game`):

| Параметр | Значение | Где |
|---|---|---|
| HP игрока | 100 + бонус реалма | `Player.prefab` → `PlayerHealth.baseMaxHealth: 100` |
| Реген игрока | 0.5/с через 5 с после урона | `PlayerHealth` |
| Урон кулаков | 15 | `Fists.asset` (поле `damage` отсутствует → дефолт 15) |
| Урон меча | 15 | `Sword.asset` (поле `damage` отсутствует → дефолт 15) |
| HP врага | 120 | `EnemyCombat.prefab` → `EnemyHealth.maxHealth` |
| Урон врага | 12 (int 2) | `EnemyCombat.prefab` → `EnemyCombatAI.attackDamage` |
| Кулдаун врага | 2 с (int 2) | `EnemyCombatAI` |
| Радиус агро | 12 | `EnemyCombatAI.aggroRadius` |
| Дистанция атаки | 1.8 | `EnemyCombatAI.attackRange` |
| Окно хитбокса врага | 0.18–0.42 нормализованного времени | `EnemyCombat.controller` |
| Блок игрока | 120° конус, Shift или ПКМ, без затрат | `PlayerBlockController` |
| Уворот | i-frames 0.45 с, кулдаун ~0.99 с | `PlayerController` |
| Хит-стоп | 0.055 с при timeScale 0.05, КД 0.08 | `CombatFeedback` |

Итог по числам: враг умирает за **8 попаданий** (120 / 15), игрок — за **9 ударов** врага
(100 / 12), но с регеном и уворотом. Кулаки и меч бьют **одинаково**.

### 2.2 Враг: агро → атака → хитбокс → урон

```
EnemyCombatAI.Update
  ├─ ResolveTarget(): GameObject.FindGameObjectWithTag("Player")
  ├─ Idle → Chase (distance <= 12)
  ├─ Chase → Attack (distance <= 1.8, кулдаун 2 с)
  ├─ раз в ~0.5 с (int 2) может уйти в Block, если игрок IsAttacking и distance <= 3
  └─ StartAttack(): Animator Attack/AttackIndex

EnemyCombat.controller
  └─ CombatAnimationWindow
       └─ EnemyCombatAI.OnAttackWindowOpen → attackHitbox.SetActive(true)
            → CombatHitbox (team Enemy, damage 12) → PlayerHealth.TakeDamage

EnemyHealth.TakeDamage
  ├─ OnHealthChanged (никто не слушает)
  ├─ ai.NotifyDamaged → Hit-stun 0.35 с, анимация Hit
  └─ currentHealth <= 0 → deathPending → Die() в LateUpdate
       ├─ ai.NotifyDeath (Knock Down, отключение агента)
       ├─ bodyCollider.enabled = false
       ├─ OnDied (никто не слушает)
       └─ Destroy(gameObject, 6)
```

### 2.3 Блок и уворот

- Блок: `PlayerBlockController.Update` держит `IsBlocking`, пока зажаты Shift или ПКМ;
  блок запрещает атаку и движение, снимается при атаке/смерти.
  `CombatHitbox` спрашивает `ICombatBlockable` **до** урона: успешный блок = 0 урона,
  проигрывается `BlockHit`, летит «БЛОК» и **не** играет хит-стоп.
- Уворот: `PlayerController.TryDodge` (клавиша `dodgeKey`, геймпад East) даёт
  `PlayerHealth.SetInvulnerable(0.45)`, двигает по профилю клипа, отменяет атаку после 60 %.
- Блок и уворот **ничего не стоят**: ни стамины, ни Ци, ни прочности, ни guard break.

### 2.4 Смерть игрока

```
PlayerHealth.Die
  ├─ animator.SetBool("IsDead", true)
  └─ OnDied
       ├─ PlayerController.HandleDied (стоп движения/анимаций)
       ├─ PlayerBlockController.HandleDied (снять блок)
       ├─ PlayerDeathPresentation.HandleDied (IsDead = true)
       └─ PlayerRespawnService.HandleDied → Respawn()  ← в том же кадре
            ├─ LoseExperiencePercent(0.25)
            ├─ Teleport к первому Altar (он же точка спавна)
            ├─ RestorePercent(0.5)
            ├─ SetInvulnerable(3)
            └─ NotifyRespawned → animator IsDead = false
```

`Respawn()` вызывается **синхронно** из `OnDied`. Экран смерти, задержка, анимация смерти,
сообщение о потере опыта — отсутствуют. Алтарь стоит там же, где стартует игрок,
поэтому смерть выглядит как «мигнуло и я на месте с половиной HP».

### 2.5 Ресурсы и животные: второй контур урона

`HarvestableObject` (деревья, руда, животные) реализует `IInteractable`, **не** `IDamageable`.
Поэтому:

- `CombatHitbox` (меч/кулаки) его **не видит**;
- урон наносит `PlayerInteraction.AttackRoutine`: через 0.3 с после нажатия
  `Physics.OverlapSphere(2.5 м)` + конус 150°, ищет `IInteractable` и вызывает
  `TakeDamage(25, point)`.

То есть по врагу урон идёт от хитбокса оружия, а по дереву/кабану — от невидимой сферы.
Сфера бьёт сквозь стены, за спину (конус 150°) и не зависит от того, попал ли меч.

### 2.6 Магия и техники

```
AbilitySystem.Update: 1–5
  └─ TryActivate
       ├─ CanActivateAbility: слот, КД, оружие, активный Дао + уровень, реалм, Ци
       └─ ExecuteMagic:
            ├─ SuppressHitbox + StopMovement
            ├─ PlayCastAnimation (если есть castAnimation)
            ├─ ждёт vfxDelay
            ├─ SpawnVfx (если есть vfxPrefab)
            └─ CreateMagicHitbox:
                 ├─ создаёт SphereCollider + Rigidbody + тег "DammageObject"
                 ├─ Physics.OverlapSphere — только Debug.Log
                 └─ Destroy через 0.2 с
```

`CreateMagicHitbox` **не наносит урон**: он рассчитывает, что по тегу сработает
`DestructibleObject.OnTriggerEnter`, но `DestructibleObject` просто удаляет себя
и на врагах его нет. У `EnemyHealth` нет реакции на теги.

Данные магии:

| Поле | `MagicBurst.asset` |
|---|---|
| `type` | Magic |
| `vfxPrefab` | **null** |
| `castAnimation` | **null** |
| `power` | **отсутствует → 0** |
| `requiredDao` | Fire (3), уровень 1 |
| `qiCost` | 20 |
| `hotkey` | 2 |

Стартовые Дао в `CultivationConfig.asset`: Sword + Fist (`startingUnlockedDaos: 0000000001000000`).
Fire не открыт → `CanActivateAbility` вернёт «Дао не открыт: Огонь», `AbilityBarUI`
покажет тост. Даже если открыть Fire: VFX нет, урона нет, `power` = 0.

`SwordSlash` (Melee) — это обычная атака оружием с КД 1.2 с; `power` не используется,
урон = `WeaponData.damage` = 15.

---

## 3. Что видит и чувствует игрок (сценарии)

### 3.1 «Иду на врага с мечом»

1. Игрок подходит, враг агрится с 12 м, идёт навстречу.
2. Игрок кликает. Анимация замаха, на 0.22–0.48 окна открывается хитбокс.
3. Если попал: цифра «15», вспышка врага, микро-откидывание, хит-стоп, тряска камеры.
4. Враг иногда блокирует (25 % при int 2) → «БЛОК», урона нет.
5. Враг бьёт: цифра «12» **того же жёлтого цвета**, хит-стоп, тряска.
6. HP врага нигде не видно — непонятно, сколько осталось.
7. На 8-м попадании враг падает (Knock Down), через 6 с исчезает.
8. **Ни лута, ни опыта, ни эффекта добивания.** `killEffect` не срабатывает (см. C-03).
9. Второй враг стоит в 25 м; после него бой в игре заканчивается.

### 3.2 «Пробую магию»

1. Игрок жмёт 2. Появляется тост «Магический взрыв: Дао не открыт: Огонь».
2. Если открыть Огонь: анимация каста не играет (её нет), VFX нет, враг не получает урона.
3. Ци списывается (20), КД 3 с — игрок заплатил и не получил ничего.

### 3.3 «Бью дерево/кабана»

1. Игрок кликает рядом. Меч может даже не коснуться.
2. Через 0.3 с дерево/кабан получает 25 урона от невидимой сферы.
3. Меч при этом не «чувствует» попадания: нет цифры урона (её даёт только `CombatHitbox`),
   нет вспышки, нет хит-стопа. Объект просто ломается.

### 3.4 «Умираю»

1. HP = 0. В тот же кадр — телепорт на алтарь, 50 % HP, 3 с неуязвимости.
2. Ни экрана смерти, ни анимации, ни звука, ни сообщения.
3. Игрок не знает, что потерял 25 % опыта ступени.
4. Если алтарь рядом с врагом — враг может продолжить бой (неуязвимость продлевается).

### 3.5 «Блокирую»

1. Зажал Shift/ПКМ — встал, анимация блока.
2. Все удары спереди в конусе 120° не проходят, летит «БЛОК».
3. Блок можно держать **бесконечно**: нет стамины, нет chip-урона, нет слома блока.
4. Оптимальная тактика против любого врага — держать блок и бить в окна. Враг
   (int 2) почти не наказывает за это.

---

## 4. Находки

Severity: **P0** — бой не выполняет обещание; **P1** — ломает логику/ощущение;
**P2** — полировка.

### P0

**C-01. Магия не наносит урона и не имеет визуала.**
`AbilitySystem.CreateMagicHitbox` (`Assets/Scripts/Combat/AbilitySystem.cs:448-480`) создаёт
trigger-сферу с тегом `DammageObject`, делает `Physics.OverlapSphere` только для `Debug.Log`
и удаляет зону. Урон никто не наносит. `MagicBurst.asset`: `vfxPrefab: 0`, `castAnimation: 0`,
поля `power` нет (0). Плюс `requiredDaoType: 3` (Fire), а стартовые Дао — Sword/Fist.
Итог: единственная магия в игре невидима, беззвучна и безвредна.

**C-02. Два контура урона: хитбокс не бьёт ресурсы, сфера не бьёт врагов.**
`HarvestableObject` — `IInteractable`, не `IDamageable` (`Assets/Scripts/Drop/HarvestableObject.cs:10`),
поэтому `CombatHitbox.TryDamage` (`CombatHitbox.cs:82`) его игнорирует. Ресурсы/животные
получают урон только от `PlayerInteraction.AttackRoutine`
(`Assets/Scripts/Drop/PlayerInteraction.cs:139-171`): сфера 2.5 м, конус 150°, через 0.3 с,
сквозь стены и независимо от анимации. Один и тот же клик обрабатывают две разные системы.

**C-03. Убийство не даёт ничего, а `ImpactKind.Killed` не наступает.**
`EnemyHealth.OnDied` не имеет подписчиков: нет лута, опыта, эффекта, звука.
`CombatHitbox.TryDamage` (`CombatHitbox.cs:113-124`) читает `target.IsDead` сразу после
`TakeDamage`, но `EnemyHealth` и `HarvestableObject` откладывают смерть до `LateUpdate`
(`EnemyHealth.cs:54-64`, `HarvestableObject.cs:46-51`) → всегда `ImpactKind.Hit`.
Поэтому `killEffect` и `killShake` из `CombatFeedback` **никогда** не проигрываются.

**C-04. Смерть игрока — мгновенный тихий респавн.**
`PlayerRespawnService.HandleDied → Respawn()` (`PlayerRespawnService.cs:71-107`) вызывается
синхронно; `NotifyRespawned` снимает `IsDead` в том же кадре. Нет экрана смерти, задержки,
анимации, звука и сообщения о потере 25 % опыта. Точка респавна — первый `Altar`,
который стоит на старте, поэтому смерть выглядит как «мигнуло».

### P1

**C-05. У врагов нет полосы здоровья.**
`EnemyHealth.OnHealthChanged` никто не слушает; в `EnemyCombat.prefab` нет health bar.
Игрок не видит, сколько осталось, и не понимает ценность своих ударов.

**C-06. Дружественный огонь: `CombatHitbox.team` не используется.**
`TryDamage` (`CombatHitbox.cs:78-125`) проверяет только `IDamageable`, владельца и `hitTargets`.
Поле `team` (`CombatTeam.Player/Enemy`) нигде не читается. Два врага рядом бьют друг друга;
при появлении союзников/питомцев проблема станет массовой.

**C-07. Кулаки и меч бьют одинаково, сила техник не применяется.**
В `Fists.asset`/`Sword.asset` нет поля `damage` → оба используют дефолт 15.
`AbilityData.power` не читается ни в melee, ни в magic. Нет разницы между оружием
и техниками по урону.

**C-08. Комбо меча длиннее, чем анимации.**
В сцене `PlayerController.maxComboIndex = 3` (override в `Game.unity`), а в
`PlayerCombat.controller` только `Sword Attack 1` и `Sword Attack 2`. Третий и четвёртый
клик с мечом не находят стейт: ввод проглатывается, игрок стоит в конце предыдущей атаки.
У кулаков 4 атаки — там `maxComboIndex = 3` корректен.

**C-09. В бою нет ни одного звука.**
Ни `AudioSource`, ни `PlayOneShot`, ни `SoundManager` в `Assets/Scripts/Combat/**` и
`Assets/Scripts/CombatSystem/**`. Удар, блок, смерть, каст — всё беззвучно.

**C-10. Цифры урона не различают «я попал» и «меня попали».**
`CombatFeedback` подписан на все `CombatEvents.Impact` и красит всё в `hitColor`
(жёлтый). Удар врага по игроку показывает жёлтую «12» над игроком + тряску + хит-стоп.
Единственный намёк — позиция цифры.

**C-11. Блок бесконечный и бесплатный.**
`PlayerBlockController` не имеет стамины/прочности/КД/guard break/chip-урона.
Держать блок можно всегда; единственный контр — враг с `intelligence >= 4`
контратакует (`EnemyCombatAI.OnBlocked`), но в сцене оба врага `int 2`.

**C-12. Префаб игрока по умолчанию использует «старый» Animator.**
`Player.prefab` → `Animator.m_Controller = PlayerController.controller` (guid `d72ac30c...`),
в котором нет `CombatAnimationWindow`, `AttackIndex`, `Combo`, `WeaponMode`, `IsBlocking`,
`Hit`, `IsDead`. В сцене `Game` контроллер переопределён на `PlayerCombat.controller`
(guid `6349a863...`), поэтому в игре работает. Любой другой инстанс префаба или новая сцена
получат: анимации без параметров, хитбокс, который никогда не открывается, комбо и блок
без анимации. Это мина замедленного действия.

**C-13. Врагов два, респавна и наград нет.**
В `Game.unity` два инстанса `EnemyCombat` (позиции примерно `-54,-264` и `-79,-239`).
Спавнеров нет. `EnemyHealth.destroyOnDeath = true`, `destroyDelay = 6` — после смерти
враг исчезает навсегда до перезагрузки сцены.

**C-14. В проекте живут три поколения боевой системы.**
- Новое: `CombatHitbox` + `IDamageable` + `EnemyCombatAI` + `EnemyHealth` + `CombatFeedback`.
- Старое: `HarvestableObject` + `IInteractable` + `PlayerInteraction` + `Weapon` + `EnemyAI` + `Enemy1`.
- Совсем старое: `DestructibleObject` (удаляет себя по тегу) + `AttackHitbox`.

В сцене остался неактивный объект `Enemy` (`m_IsActive: 0`) с `EnemyAI` + `HarvestableObject` +
`Enemy1`: если его включить, он начнёт бить напрямую (без хитбокса и блока) и вести себя
иначе. Это ловушка для любого, кто будет править сцену.

**C-15. Мёртвый override `hitboxStart` на `PlayerController`.**
В `Game.unity` у компонента `PlayerController` переопределён `hitboxStart: 0.4`,
но такого поля у `PlayerController` нет — оно у `CombatAnimationWindow`. Override
молча игнорируется; окно хитбокса остаётся 0.22–0.48 (кулаки) / 0.28–0.55 (меч).

### P2

**C-16. Нет проверки линии видимости.**
`EnemyCombatAI` агрится и бьёт по расстоянию, без LOS; `CombatHitbox` — по overlap,
без LOS. В ближнем бою это почти не заметно, но враг может ударить через тонкую стену.

**C-17. Слабый телеграф атаки врага.**
Окно хитбокса открывается на 0.18 нормализованного времени; кроме анимации нет ни звука,
ни вспышки, ни индикатора. В сочетании с C-09 удар трудно прочитать.

**C-18. Нет фидбека получения урона игроком.**
Кроме `Animator.SetTrigger("Hit")` и тряски камеры — ничего: нет красной вспышки/виньетки,
нет мигания i-frames, нет звука.

**C-19. Хит-стоп срабатывает на любой удар, включая урон по игроку.**
`CombatFeedback.HandleImpact` не различает, кто кого ударил. При двух врагах игра
замирает на 55 мс несколько раз в секунду — это может ощущаться как рывки.

**C-20. Спам в консоль.**
`HarvestableObject.TakeDamage` пишет `Debug.Log` на каждый удар; `Weapon.OnTriggerEnter`
тоже логирует. В бою с ресурсами консоль забивается.

**C-21. `EnemyCombat_Strong` — не сильный.**
Инстанс переопределяет только имя и позицию; `intelligence`, HP и урон те же, что у обычного.

**C-22. Нет рамки цели/боевого лога.**
`AbilityBarUI` показывает слоты и КД, `PlayerVitalsUI` — HP/Ци/реалм. Но нет ни имени
текущей цели, ни её HP, ни строки «получено 15 урона».

**C-23. Реген невидим.**
0.5 HP/с через 5 с — игрок не понимает, почему HP медленно растёт.

**C-24. Нет эффектов попадания/смерти у врага, кроме вспышки и рэгдолла.**
`killEffect` есть в `CombatFeedback`, но не срабатывает (C-03). У `EnemyHealth.Die`
нет `destroyEffectPrefab`, в отличие от старого `HarvestableObject`.

---

## 5. Что чинить и в каком порядке

### Волна 0 — привести данные в правду (без изменения логики)

1. Заполнить `WeaponData.damage`: кулаки 8–10, меч 18–22. Заполнить `AbilityData.power`
   для melee/magic (например, SwordSlash 20, MagicBurst 25).
2. `MagicBurst`: задать `vfxPrefab`, `castAnimation`, `power`; либо убрать `requiredDao`
   (стартовая магия), либо открыть Fire на старте.
3. `Player.prefab`: переключить Animator на `PlayerCombat.controller`, чтобы префаб и сцена
   совпадали.
4. Определиться с комбо меча: либо добавить `Sword Attack 3`, либо сделать длину комбо
   зависимой от оружия (`WeaponData.maxComboIndex`), либо вернуть `maxComboIndex = 1` для меча.
5. `EnemyCombat_Strong`: сделать действительно сильным (int 4, HP 200, урон 18) или переименовать.
6. Удалить мёртвый override `hitboxStart` у `PlayerController` (C-15).

### Волна 1 — один контур урона

7. Научить `HarvestableObject` получать урон от `CombatHitbox`: реализовать `IDamageable`
   (или добавить адаптер), чтобы меч/кулаки били ресурсы и животных через тот же хитбокс.
8. Убрать нанесение урона из `PlayerInteraction.AttackRoutine`; оставить только проверки
   UI/лута/взаимодействия. Один клик — один источник урона.
9. Переписать `AbilitySystem.CreateMagicHitbox`: зона должна наносить урон через
   `IDamageable.TakeDamage(power * DamageMultiplier)` с фильтром команды и одним попаданием
   на цель; VFX обязателен.
10. Использовать `CombatTeam` в `CombatHitbox.TryDamage`: не бить своих.
11. Починить `ImpactKind.Killed`: либо `EnemyHealth`/`HarvestableObject` сами сообщают
    о смерти в шину, либо `CombatHitbox` проверяет «умрёт ли» до отложенной смерти.

### Волна 2 — читаемость и фидбек

12. Полоса HP врага (world-space, пул), подписка на `EnemyHealth.OnHealthChanged`,
    скрыта до первого урона.
13. Разные цвета цифр: урон врагу — жёлтый/оранжевый, урон игроку — красный, блок — голубой.
14. Звуки: замах, попадание, блок, смерть, каст. Хотя бы `PlayOneShot` на событиях.
15. Фидбек получения урона игроком: красная виньетка/вспышка, мигание в i-frames.
16. Смерть: 1.5–2 с анимации/экрана, сообщение «Вы погибли», показ потери опыта,
    затем респавн на алтаре. Не респавнить в том же кадре.
17. Убрать спам `Debug.Log` из боевых путей.

### Волна 3 — глубина и баланс

18. Блок: стамина/прочность/чип-урон/слом блока; индикатор.
19. Разнообразие врагов: `Strong` с int 4, 4–6 врагов, спавнер/респавн, лут и опыт за убийство.
20. Телеграф атаки: вспышка/звук/индикатор замаха.
21. Разная длина комбо и урон у оружия; техники масштабируются от `power` и пассивов.
22. Убрать/пометить legacy: `EnemyAI`, `Enemy1`, `DestructibleObject`, `AttackHitbox`,
    старый `Weapon`-путь, неактивный объект `Enemy`.

---

## 6. Критерии приёмки

- Удар мечом по врагу: цифра, вспышка, хит-стоп, звук; HP-бар врага падает; 6–8 ударов до смерти.
- Убийство: эффект добивания, лут/опыт, труп исчезает не мгновенно.
- Магия: видимый VFX, урон, цифры, убивает; Ци тратится осмысленно.
- Ресурсы и животные получают урон тем же хитбоксом, что и враги; нет урона сквозь стены.
- Удар врага по игроку: красная цифра, виньетка, тряска; блок — «БЛОК» и 0 урона.
- Блок имеет цену/предел; нельзя держать вечно без последствий.
- Смерть: анимация/экран, сообщение о потере опыта, респавн на алтаре.
- Два врага рядом не наносят урон друг другу.
- В консоли нет спама на каждый удар.
- Префаб игрока и сцена используют один и тот же Animator Controller.

---

## 7. Карта файлов

| Файл | Роль | Что менять |
|---|---|---|
| `Assets/Scripts/CombatSystem/CombatHitbox.cs` | единственный реальный источник урона | team-фильтр, Killed, LOS |
| `Assets/Scripts/CombatSystem/CombatInterfaces.cs` | `IDamageable`, `ICombatBlockable` | добавить `ICombatTeam`/`WillDie` при необходимости |
| `Assets/Scripts/CombatSystem/EnemyCombatAI.cs` | ИИ врага | телеграф, LOS, сильный вариант |
| `Assets/Scripts/CombatSystem/EnemyHealth.cs` | HP врага | health bar, лут/опыт, Killed |
| `Assets/Scripts/Combat/AbilitySystem.cs` | каст | реальный урон магии, VFX |
| `Assets/Scripts/Combat/AbilityData.cs` | данные способности | `power` в бою |
| `Assets/Scripts/Combat/WeaponManager.cs` | оружие/хитбокс | урон по оружию |
| `Assets/Scripts/Combat/PlayerHealth.cs` | HP игрока | фидбек урона |
| `Assets/Scripts/Combat/PlayerRespawnService.cs` | смерть/респавн | задержка, экран, сообщение |
| `Assets/Scripts/Combat/CombatFeedback.cs` | хит-стоп/VFX/цифры | цвета по цели, Killed |
| `Assets/Scripts/Drop/HarvestableObject.cs` | ресурсы/животные | `IDamageable`, убрать Debug.Log |
| `Assets/Scripts/Drop/PlayerInteraction.cs` | скрытая сфера урона | убрать нанесение урона |
| `Assets/Scripts/Player/PlayerController.cs` | атака/комбо/уворот | длина комбо по оружию |
| `Assets/Scripts/CombatSystem/PlayerBlockController.cs` | блок | стамина/чип/слом |
| `Assets/Scripts/Cultivation/Runtime/UI/PlayerVitalsUI.cs` | HUD игрока | оставить, добавить фидбек |
| `Assets/Player.prefab` | игрок | Animator → `PlayerCombat.controller` |
| `Assets/Prefabs/Enemies/EnemyCombat.prefab` | враг | health bar, Strong |
| `Assets/Animations/Combat/PlayerCombat.controller` | анимации игрока | `Sword Attack 3` или лимит комбо |
| `Assets/Animations/Combat/EnemyCombat.controller` | анимации врага | окна/телеграф |
| `Assets/Combat/Weapons/{Fists,Sword}.asset` | данные оружия | `damage` |
| `Assets/Combat/Abilities/{MagicBurst,SwordSlash,FistsJab}.asset` | способности | `power`, VFX, каст |
| `Assets/Scenes/Game.unity` | сцена | убрать мёртвый override, враги |
