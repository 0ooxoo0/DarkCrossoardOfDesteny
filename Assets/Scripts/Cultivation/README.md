# Культивация и Дао (изолированная система)

Всё лежит в `Assets/Scripts/Cultivation/` и не пересекается с инвентарём, Garderob, WeaponManager,
AbilitySystem, хотбаром, оружием, хитбоксами, магией и DestructibleObject.

## Как играть сразу (без настройки)

1. Открой любую сцену с игроком и нажми **Play**.
2. Bootstrap сам найдёт `PlayerController`, повесит компоненты, соберёт UI и восстановит прогресс.

Управление:

| Клавиша / кнопка | Действие |
|---|---|
| **Tab** или **C** | Панель культивации (реалм, опыт, Прозрение, 5 созвездий Дао) |
| **M** | Медитация вкл/выкл (×2 в зоне `MeditationZone`) |
| **1–5** | Быстрый каст назначенных техник |
| **Esc** | Закрыть панель |
| Клик по пустому слоту | Открыть список техник |
| «Прорыв» в панели | 3 с прогресса + шанс (база + Прозрение) |

Первый запуск: слот 1 уже занят «Ударом клинка» — жми **1**, чтобы увидеть каст, трату Ци, КД и VFX.
Медитация даёт опыт Ци и очки Дао; через ~7 секунд доступен прорыв.

Без ассетов работает **демо-конфиг**: открыты все 5 путей и разрешена смена Дао из панели
(алтаря в проекте пока нет). После `Tools → Cultivation → Setup Test Assets` включается
канонический контент: 2 стартовых пути, смена — только у алтаря через `DaoSystem.SetNearAltar(true)`
(подключит Промт B).

Прогресс сохраняется между сценами и перезапусками (PlayerPrefs). Сброс:
**Tools → Cultivation → Reset Saved Progress**.

## Что внутри

- **Ци** — `QiComponent`: MaxQi/CurrentQi, пассивный реген, `Spend/Restore/RestoreFull`, события
  `OnQiChanged(int,int)`, `OnQiDepleted`, `OnQiSpent(int)`. Про HP и AbilitySystem не знает.
- **Культивация** — `CultivationSystem` + `CultivationRealm`: реалмы/ступени, опыт Ци, Прозрение,
  `TryBreakthrough()` → `BreakthroughResult { Success, Failed, NotReady }`, бонусы реалма событием
  `OnRealmBonusesChanged(int qiBonus, int hpBonus, float regenBonus)`, скорость изучения техник —
  `OnTechniqueLearningChanged(float multiplier)`.
- **Дао** — `DaoSystem` + `DaoPath`: уровни по очкам, `ActiveDao`, `IsDaoUnlocked/UnlockDao`,
  `GetAvailableTechniques()` (фильтр по активному Дао + уровню + реалму).
- **Техники** — `TechniqueData`: только данные (тип, ранг, требования, Ци, КД, сила, клип, VFX).
- **Медитация** — `MeditationController` + `MeditationZone` (×2), прерывание уроном через `IMeditationInterrupter`.
- **Прорыв** — `BreakthroughSequence`: прогресс + шанс, столб света и вспышка.
- **Быстрый каст** — `CultivationCastController` + `CultivationQuickBarUI` (демо-исполнитель).
- **UI** — `CultivationUIRoot`: QiBar под HP, панель Tab/C, список техник, оверлей медитации, модалка прорыва.
- **VFX** — `DaoAuraController` + процедурные плейсхолдеры `CultivationVfxFactory`.
- **Контент** — `CultivationDefaults` (4 реалма × 3 ступени, 5 путей, 15 техник), ассеты создаёт
  `Tools → Cultivation → Setup Test Assets`.

## Подключение со стороны AbilitySystem / хотбара (Промт B)

```csharp
bool paid = qi.Spend(technique.QiCost);
List<TechniqueData> available = daoSystem.GetAvailableTechniques();

techniqueListUI.OnTechniqueAssignRequested += technique => hotbar.Assign(technique);

daoSystem.SetNearAltar(true);              // у алтаря
daoSystem.SetActiveDao(DaoType.Sword);

CultivationBootstrap.PlayerRealmBonusesChanged += (qiBonus, hpBonus, regenBonus) => hp.ApplyRealmBonus(hpBonus);
cultivationSystem.HasBreakthroughMaterials = () => inventory.HasTags(realm.RequiredMaterials);

MeditationInterruptSource.NotifyPlayerDamaged(player);
CultivationHooks.NotifyBossDefeated("boss_id", 3, 150);
```

Когда на игроке появится компонент `AbilitySystem` или `WeaponManager`, демо-каст и его панель
отключатся сами (`CultivationCastController.CheckExternalSystems`), а подписка на
`OnTechniqueAssignRequested` перехватит назначение в хотбар.

## Ограничения

- Состояния **Meditate** в Animator игрока нет (`Assets/Animation/Cube.controller` — Idle/Attak/Run/Walk),
  `animatorTriggerName` пустой. Появится состояние — впишите имя триггера.
- Пакета «All In 1 Vfx Toolkit v1.51» в проекте нет: VFX процедурные, поля под префабы есть
  (`DaoAuraController.auraPrefab`, `MeditationController.meditationVfxPrefab`,
  `BreakthroughSequence.breakthroughVfxPrefab`, `TechniqueData.vfxPrefab`).
- Иероглифы требуют CJK-шрифта: `CultivationConfig.calligraphyFont` + `showChineseNames`.
- Демо-каст наносит урон через `SendMessage("TakeDamamge"/"TakeDammage")` — в проекте встречаются оба
  написания (BossController / PlayerHpController). Настоящие хитбоксы — зона Промта B.
- `PlayerController.cs`, `PlayerHpController.cs`, инвентарь, оружие, хотбар, магия не изменялись.
