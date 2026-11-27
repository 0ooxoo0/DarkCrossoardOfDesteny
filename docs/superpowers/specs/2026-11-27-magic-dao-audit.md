# Аудит: магия и Дао (взгляд игрока)

Дата: 2026-11-27
Охват: `Assets/Scripts/Combat/{AbilitySystem,AbilityData,WeaponManager}.cs`,
`Assets/Scripts/Cultivation/**`, `Assets/Scripts/CombatSystem/CombatHitbox.cs`,
`Assets/Scripts/Player/{PlayerController,PlayerInteraction,AimController}.cs`,
`Assets/Magic/**` (57 префабов), `Assets/Scripts/Cultivation/Data/**`,
`Assets/Resources/Cultivation/CultivationVfxConfig.asset`, `Assets/Scenes/Game.unity`.

Две претензии игрока:

1. Магия должна лететь в точку, на которую смотрит центр экрана.
2. Магия должна быть настраиваемой: есть префабы `Assets/Magic`, из них нужны скиллы древа Дао.

Обе претензии подтверждаются кодом. Ниже — что игрок видит, почему так, и что мешает.

---

## 1. Что происходит в игре сейчас

| Шаг игрока | Что видит | Что в коде |
|---|---|---|
| Жмёт `4` (слот техники) | Ничего | `SpawnVfx` выходит на первой строке: у всех 25 техник `vfxPrefab: {fileID: 0}` |
| Враг в 1.5 м перед персонажем | Враг теряет HP | `CreateMagicHitbox` ставит невидимую trigger-сферу в 1.5 м вперёд |
| Враг под прицелом в 10 м | Ничего | Луч камеры не используется вообще; зона до него не достаёт |
| Смотрит в стену и жмёт `4` | Всё равно бьёт вперёд | Направление — `transform.forward` персонажа, не камера |
| Открывает «Скиллы» | 25 техник, из них 20 серые «Механика в разработке» | Мост отвергает `Block`/`Aura`, пассивы не назначаются |
| Ищет «древо изучения» | Не находит | Древа нет: плоский список + гейт по уровню Дао |

Итог: магия — невидимая сфера у ног персонажа. Претензия «не летит туда, куда смотрю» — точное описание поведения.

---

## 2. Претензия 1: направление и точка прицела

### 2.1 Где задаётся направление

`Assets/Scripts/Combat/AbilitySystem.cs:504-513`:

```csharp
private void SpawnVfx(AbilityData ability)
{
    if (ability.vfxPrefab == null) return;                       // :506 — у техник Дао всегда null
    Transform origin = playerController != null ? playerController.transform : transform;
    Vector3 spawn = origin.position + origin.forward * ability.magicSpawnDistance;  // :509
    GameObject vfx = Instantiate(ability.vfxPrefab, spawn, origin.rotation);        // :510
```

`Assets/Scripts/Combat/AbilitySystem.cs:515-521` (урон):

```csharp
Transform origin = playerController != null ? playerController.transform : transform;
Vector3 spawn = origin.position + origin.forward * ability.magicSpawnDistance;
GameObject zone = new GameObject("MagicHitbox_Temp");
zone.transform.position = spawn;                                  // :521
```

Что не так:

1. **Направление — `origin.forward` персонажа.** Каст по клавише не вызывает `FaceTowards`: поворот к цели делает только клик-атака (`Assets/Scripts/Drop/PlayerInteraction.cs:62-69`). Значит летит туда, куда персонаж повернулся последним движением (`PlayerController.cs:413-418`).
2. **`ScreenPointToRay`/`ViewportPointToRay` из каста не вызывается нигде.** Единственные лучи в проекте — по **позиции мыши** и только для кликов: `PlayerInteraction.cs:87,103,123`, `LootPointerController.cs:74`, `AimController.cs:49`. Центра экрана нет ни в одном.
3. **`AbilityData.magicRange` (`AbilityData.cs:56`) не читается нигде** — поле мёртвое. Дальность фактически = `magicSpawnDistance` = 1.5 м (`AbilityData.cs:58`).
4. **Полёта нет.** `MagicHitbox_Temp` — статичная сфера, живёт `magicHitboxLifetime = 0.2` с (`AbilitySystem.cs:548`). «Снаряд» — мгновенный взрыв в 1.5 м.
5. **Высота = ступни.** `origin.position` без `Vector3.up` (`AbilitySystem.cs:509,518`), тогда как мёртвая демо-ветка подъём делала (`CultivationCastController.cs:203`).

### 2.2 Что уже есть для переиспользования

| Что | Где | Годность |
|---|---|---|
| `PlayerController.ResolveViewCamera()` | `PlayerController.cs:975-990` | `internal static`, отсеивает камеры с `targetTexture` — брать вместо `Camera.main` |
| `PlayerController.FaceTowards(point, t)` | `PlayerController.cs:432-436` | public, готов |
| `PlayerInteraction.GetAimPoint()` | `PlayerInteraction.cs:116-138` | **private**, луч по мыши + фолбэк на плоскость земли — логику копировать, звать нельзя |
| `AimController` + `AimTarget` | `Assets/Scripts/Player/AimController.cs` | **пустышка**: guid скрипта не встречается ни в `Player.prefab`, ни в `Game.unity`; `PlayerController.cs:198-199` ищет его и не находит |
| `MuzzlePoint`/`FirePoint` у оружия | — | нет; `WeaponManager` даёт только `rightHand` |
| `Camera.main` | `Game.unity` | ненадёжен: объект `Main Camera` в сцене **выключен** (`m_IsActive: 0`), рабочая камера живёт в `CameraContainer` |

### 2.3 Риски прицела (обязательно учесть)

- **Луч упрётся в себя.** Слой игрока = 0 (`Player.prefab`), врагов тоже 0. Приём `~(1 << layer)` при слое 0 ничего не исключает (`PlayerInteraction.cs:88,104`). Свой корень отсекать сравнением `hit.collider.transform.root == origin.root`.
- **Trigger-коллайдеры.** Зона магии — trigger (`CombatHitbox.Awake` принудительно ставит `isTrigger = true`). Луч идёт с `QueryTriggerInteraction.Ignore`, иначе попадает в свои прошлые зоны и в `WorldLootContainer`.
- **Камера отстаёт.** `CameraOrbitMode.autoOrbit` в сцене выключен override'ом → ручное вращение в `LateUpdate`. Точку считать в момент каста, не кэшировать.
- **LOS.** `CombatHitbox.HasLineOfSight` (`CombatHitbox.cs:175-194`) при пустом `ownerLayers` считает препятствием всё → цель за укрытием урона не получит. Это ожидаемо, но объясняет «не долетело».
- **EditMode-тесты.** `Assets/Tests/EditMode/SkillSystemTests.cs:157` создаёт `AbilitySystem` на пустом GameObject без камеры: новый код обязан молча уходить в фолбэк `origin.forward * range`, иначе NRE.
- **Сеть.** Мультиплеера в проекте нет (`Mirror|Netcode|FishNet|Photon` — 0 совпадений). Синхронизировать нечего.

---

## 3. Претензия 2: настраиваемость и префабы

### 3.1 Инвентаризация `Assets/Magic`

**57 префабов** (не 55), все собраны на AllIn1 Vfx Toolkit. Полный разбор — в таблице приложенного аудита префабов; сводка:

| Категория | Кол-во | Примеры | Годится как |
|---|---|---|---|
| PROJECTILE | 6 | `Fire Bullet`, `Digital Projectile`, `Plasma Ball`, `Ice Projectile`, `Gun Bullet`, `Magic Spiral` | снаряд |
| BEAM | 2 | `Beam Blue`, `Toon Beam Orange` | луч |
| IMPACT / EXPLOSION / SLASH / MUZZLE | 22 | `Explosion Bomb`, `Lightning Strike`, `Slash Blue`, `Fire Muzzle Flash` | попадание / вспышка каста |
| AURA / FIELD / AREA / ORB | 17 | `Holy Aura`, `Evil Aura`, `Dark Magic Orb`, `Blue Fire`, `Red Area` | аура, зона |
| SHIELD | 5 | `SciFi Shield`, `Fire Shield`, `Water Shield` | щит (у SciFi уже есть BoxCollider) |
| PORTAL | 2 | `Green Portal`, `Blue Pixel Portal` | призыв/телепорт |
| TRAIL | 3 | `Fire Trail`, `Ghost Trail`, `Pink Trail` | шлейф снаряда |
| не VFX | 2 | `Toon Character`, `Screenspace Galaxy Character` | не использовать (риг + Animator) |

**Связано с игрой сейчас: 10 из 57.** В `CultivationVfxConfig.asset` — `Holy Aura`, `Green Area`, `Blue Area + Distort Sphere`, `Explosion Galaxy`, `Thick Smoke`, `Blue Tornado`, `Dark Magic Orb`, `Red Area`; в `MagicBurst.asset` — `Magic Explosive Spell`. Остальные **46 префабов не подключены никуда**.

### 3.2 Чего не хватает префабам

| Чего нет | Факт | Следствие |
|---|---|---|
| Скрипт полёта | В 57 префабах только демо-скрипты AllIn1 (`AutoDestroy`, `DoShake`, `FadeLight`, `AutoRotate`, `BounceAnimation`, `LookAt`, `ScrollShaderProperty`); `AllIn1DemoProjectile` не подключён нигде | снаряд не двигается |
| Коллайдер, Rigidbody | `!u!54` = 0, `!u!135` = 0 во всём каталоге | попадание не определяется |
| Время жизни | `AllIn1VfxAutoDestroy` есть только в 16 префабах; порталы, щиты, ауры, огонь, трейлы — без него | эффект эмитит вечно |
| `loop=1` у снарядов | `Fire Bullet`, `Plasma Ball`, `Magic Spiral`, `Ice Projectile` — вечные эмиттеры | снаряд не «заканчивается» |
| Точка-якорь | имён `Muzzle/Start/End/Point/Spawn/Socket` в префабах нет (только кости `*_end_end` в двух Character) | спавнить можно лишь в origin префаба |
| Звук | `!u!82` = 0 | каст немой |
| Слои | все 524 GO — `m_Layer: 0`, `Untagged` | свои/чужие не различаются |

Плюс вес: 20 префабов 0.7–1.8 МБ. В проекте уже есть предупреждение об этом — `Assets/Scripts/Combat/CombatFeedback.cs:160` («Instantiate тяжёлого VFX прямо из OnTriggerStay роняет редактор»).

### 3.3 Куда техника кладёт префаб

Поле уже есть и уже доезжает до исполнителя:

- `TechniqueData.cs:43` — `[SerializeField] private GameObject vfxPrefab;` → `:64 public GameObject VfxPrefab => vfxPrefab;`
- `CultivationAbilityBridge.cs:309` — `data.vfxPrefab = technique.VfxPrefab;`
- `AbilitySystem.cs:506` — `if (ability.vfxPrefab == null) return;`
- демо-ветка: `CultivationCastController.cs:205-212`, иначе процедурный `CultivationVfxFactory.CreateCastBurst`.

**Но у всех 25 ассетов `Assets/Scripts/Cultivation/Data/Techniques/Technique_*.asset` стоит `vfxPrefab: {fileID: 0}`** (проверено сканом всех 25). Пусто и `castAnimationClip`, и `icon`. Отсюда «магия без визуала».

Вывод: связывать префаб↔скилл новым кодом не нужно — нужен импортёр, который эти поля заполнит.

---

## 4. Состояние «древа Дао»

**Древа изучения нет.** Есть плоский список + гейт по двум числам.

Гейт целиком — `DaoSystem.cs:407-411`:

```csharp
public bool IsTechniqueAvailable(TechniqueData technique, int level, int realmIndex)
{
    if (technique == null) return false;
    return technique.RequiredDaoLevel <= level && technique.RequiredRealmIndex <= realmIndex;
}
```

Что отсутствует (grep `SkillTree|SkillNode|Prerequisite|SkillPoint|unlockCost|Respec` по системе культивации — пусто):

| Нет | Следствие для игрока |
|---|---|
| Узлы, ветки, ярусы | `rank` участвует только в подписи «Ранг N» (`TechniqueListUI.cs:260-262`) |
| Предпосылки | любую технику можно взять в обход предыдущей |
| Стоимость в очках | очки Дао — это опыт: `level = startingLevel + points / PointsPerLevel` (`DaoSystem.cs:248`). Кнопки «Изучить» нет ни на одном экране |
| Трата и перераспределение | API `SpendPoints/RemovePoints/Respec` отсутствует; очки упираются в потолок `maxLevel * PointsPerLevel` (`DaoSystem.cs:244-246`) |
| Иконки узлов | `icon: {fileID: 0}` у 25 техник и 5 `Dao_*.asset`; UI рисует букву-заглушку (`TechniqueListUI.cs:246-252`) |

Контент: 5 Дао × 5 техник = 25 (`Data/Techniques/`), из них Active 5, Block 5, Aura 5, Passive 10. Назначить в слот можно **только Active**, потому что:

`CultivationAbilityBridge.cs:69-82`:

```csharp
if (technique.Kind == TechniqueKind.Passive) { ... return false; }
if (technique.Kind == TechniqueKind.Block || technique.Kind == TechniqueKind.Aura)
{
    LastAssignMessage = technique.DisplayName + " — механика в разработке, в слот не назначается.";
    return false;
}
```

Слотов всего 5, из них 1–3 заняты оружием (`AbilitySystem.cs:107-110`), техникам остаются 3–4 (`CultivationAbilityBridge.cs:99`). То есть **из 25 техник играбельных 5, и только у активного Дао** (`AbilitySystem.cs:277-281` требует совпадения активного Дао).

Смена Дао — только у алтаря (`canSwitchAnywhere: 0`, `CultivationConfig.asset`), поэтому техника чужой школы в слоте «мертва» до похода к алтарю.

---

## 5. Прочее, что бьёт по игроку

1. **Экран «Техники» недостижим.** `CultivationUI.cs:539-551`: если `skills != null`, всегда открывается `SkillListUI`; `TechniqueListUI` со своими фильтрами по Дао в бою не увидеть.
2. **Максимум Дао 4 — UI рисует 10 звёзд.** `maxLevel` ассетов = 4 (`CultivationSetup.cs:192-200`), а `CultivationUI.cs:226-237` строит 10 звёзд и подпись «0 / 10» → 6 звёзд погаснут навсегда.
3. **Свиток `Scroll_SwordStrike` не работает.** `Scroll_SwordStrike.asset` имеет `useKind: 5` (= `BreakthroughMaterial`), мост на такое отвечает отказом (`CultivationItemBridge.cs:81-84`) → изучение в обход уровня недоступно.
4. **Причина отказа каста не доходит до игрока** при основной ветке: `OnAbilityActivationBlocked` (`AbilitySystem.cs:356`) никто не слушает.
5. **Звук каста** зависит от `CombatAudio`; в префабах магии `AudioSource` нет.

---

## 6. Минимальный план исправления

### Волна 0 — прицел (P0, без новых ассетов)

1. Один статический резолвер точки: `Camera` → `ViewportPointToRay(0.5, 0.5)` → `Physics.Raycast(range, mask, Ignore)` → исключить свой корень → фолбэк `ray.GetPoint(range)`; если камеры нет — `origin.position + origin.forward * range`.
2. `AbilitySystem.ExecuteMagic`: считать `aimPoint` **до** `WaitForSeconds(vfxDelay)` (за 0.2 с игрок доворачивает камеру), вызвать `playerController.FaceTowards(aimPoint)`, передать точку в `SpawnVfx` и `CreateMagicHitbox`.
3. `SpawnVfx`: спавн на высоте (`+ Vector3.up * aimHeight`) и `Quaternion.LookRotation(aimPoint - spawn)`.
4. `CreateMagicHitbox`: `zone.transform.position = aimPoint` вместо `spawn`.
5. `magicRange` перестаёт быть мёртвым полем — дальность настраивается в существующих ассетах.
6. Та же правка в демо-ветке `CultivationCastController.SpawnVfx:201-213`.

### Волна 1 — полёт и настраиваемость (P0/P1)

7. `TechniqueData`: `delivery` (`Projectile/Beam/Impact/Aura/Self/Ground`), `speed`, `lifetime`, `radius`, `impactPrefab`, `muzzlePrefab`, `homing`.
8. Один рантайм-компонент `MagicProjectile` (SphereCollider + kinematic Rigidbody + `CombatHitbox`), навешивается **в рантайме**, префабы остаются визуальными — 57 файлов не правим.
9. `AbilitySystem.SpawnVfx`: `switch (delivery)` — снаряд с полётом до `aimPoint`, луч, мгновенный эффект, аура на игроке.
10. `CultivationAbilityBridge.ToAbilityData:297-319`: прокинуть новые поля.

### Волна 2 — древо Дао (P1)

11. `TechniqueData`: `unlockCost`, `prerequisiteIds`, `starter`; `rank` = ярус.
12. `DaoSystem`: `spentPoints`, `TryUnlockTechnique`, уровень = `startingLevel + (points - spent) / PointsPerLevel`; хранилище — уже существующий `learnedTechniques` (сохраняется через `GetLearnedTechniqueIds` → `CultivationStateCache`, формат не ломается).
13. Editor-импортёр `Assets/Magic/**/*.prefab` → `TechniqueData` + запись в `DaoPath.techniques` (маппинг папка/имя → `DaoType`).
14. Экран `DaoTreeUI`: узлы, статусы, цена, клик-покупка.
15. Разрешить `Aura`/`Beam` в мосте либо свести их к `Active` с другим `delivery`.

### Волна 3 — гигиена (P2)

16. Чистка 6–10 префабов-кандидатов: снять демо-скрипты, `loop=0` + `stopAction=Destroy` для одноразовых, пустой дочерний `Root` как якорь, погасить вечный снег в `Ice Impact`.
17. Больше слотов (страницы) — иначе 40+ скиллов некуда класть.
18. Иконки: превью префаба в спрайт на этапе импорта.

---

## 7. Приёмка

Игрок должен увидеть:

1. Наведение в центр экрана + `4` → видимый снаряд летит **в точку под прицелом**, а не вперёд по персонажу.
2. Промах по стене: снаряд гаснет на стене, враг за ней урона не получает.
3. Дальность больше не 1.5 м: настраивается в ассете техники.
4. Открыт экран дерева: узлы из префабов `Assets/Magic`, у каждого цена и предпосылка.
5. «Изучить» списывает очки Дао, узел становится доступным сразу, после перезапуска — на месте.
6. Скиллы разных типов: снаряд, луч, аура, щит — а не только 5 Active.
