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
| **Esc** | Единое меню: закрыть верхнюю панель, иначе пауза |
| Полоса Дао слева | Нажатие открывает экран медитации |
| Клик по пустому слоту | Открыть список техник |
| «Прорыв» в панели | 3 с прогресса + шанс (база + Прозрение) |

Первый запуск: слот 1 уже занят «Ударом клинка» — жми **1**, чтобы увидеть каст, трату Ци, КД и VFX.
Медитация даёт опыт Ци и очки Дао; через ~7 секунд доступен прорыв.

Без ассетов работает **демо-конфиг**: открыты все 5 путей и разрешена смена Дао из панели.
После `Tools → Cultivation → Setup Test Assets` включается канонический контент: 2 стартовых пути,
смена — только у алтаря через `DaoSystem.SetNearAltar(true)` (подключит Промт B).

`Tools → Cultivation → 4. Setup World Content (Game)` дополняет сцену миром: ставит `EnemyAI` на врагов,
создаёт трёх животных (лошадь, свинья, овца) с добычей, печь `AlchemyStation` рядом с кузницей и
прописывает в `CultivationConfig` конфиги риска, алхимии и VFX. Меню идемпотентно.

Прогресс сохраняется между сценами и перезапусками (PlayerPrefs). Сброс:
**Tools → Cultivation → Reset Saved Progress**.

## Что внутри

- **Ци** — `QiComponent`: MaxQi/CurrentQi, пассивный реген, `Spend/Restore/RestoreFull`, события
  `OnQiChanged(int,int)`, `OnQiDepleted`, `OnQiSpent(int)`. Про HP и AbilitySystem не знает.
- **Здоровье и смерть** — `PlayerHealth` (`DamageInfo`, события `OnDamaged`/`OnDied`, бонусы реалма и
  пассивов) и `PlayerRespawnService`: смерть = возврат к алтарю и потеря 25 % опыта ступени.
- **Культивация** — `CultivationSystem` + `CultivationRealm`: реалмы/ступени, опыт Ци, Прозрение,
  `TryBreakthrough()` → `BreakthroughResult { Success, Failed, NotReady }`, бонусы реалма событием
  `OnRealmBonusesChanged(int qiBonus, int hpBonus, float regenBonus)`, скорость изучения техник —
  `OnTechniqueLearningChanged(float multiplier)`, потеря опыта — `LoseExperiencePercent(float)`.
- **Дао** — `DaoSystem` + `DaoPath`: уровни по очкам, `ActiveDao`, `IsDaoUnlocked/UnlockDao`,
  `GetAvailableTechniques()` (фильтр по активному Дао + уровню + реалму).
- **Техники** — `TechniqueData`: только данные (тип, ранг, требования, Ци, КД, сила, клип, VFX).
- **Медитация** — `MeditationController` + `MeditationZone` (×2), `MeditationSession`,
  `MeditationRiskConfig`, `CultivationDebuffController`: риск, дебафы и три стадии последствий.
- **Прорыв** — `BreakthroughSequence`: прогресс + шанс, столб света и вспышка.
- **Алхимия** — `AlchemySkill` (грейды `AlchemyGrade`, качество `PillQuality`), `AlchemyService`
  (шанс успеха, провал, качество), `AlchemyStation` + `AlchemyPanelUI` у печи, походный крафт из Esc-меню.
- **Пассивы** — `PassiveSkillService`: `PassiveEffectKind`, `PassiveModifier`, десять пассивов школ
  (меч, кулак, тело, огонь, гром) пишут в отдельные множители, не затирая дебафы.
- **Добыча** — `HarvestableObject.lootTable` → `LootTable`/`LootRoll`/`LootDrop`; враги (`EnemyAI`)
  и животные (`AnimalAI`) падают контейнерами `WorldLootContainer`.
- **Быстрый каст** — `CultivationCastController` + `CultivationQuickBarUI` (демо-исполнитель).
- **UI** — `CultivationUIRoot`: полоса Ци, `PlayerVitalsUI`, `DaoHudBarUI`, `MeditationScreenUI`,
  `DebuffPanelUI`, `SkillListUI`, панель Tab/C, оверлей медитации, модалка прорыва.
- **Esc-меню** — `GameMenuController`: стек панелей, `UiManager` остаётся владельцем паузы,
  кнопки «Навыки», «Карта» и «Журнал» (заглушки).
- **VFX** — `DaoAuraController`, `CultivationVfxBridge` и `CultivationVfxConfig` (префабы из `Assets/Magic`),
  процедурные плейсхолдеры `CultivationVfxFactory` на случай пустых ссылок.
- **Контент** — `CultivationDefaults` (4 реалма × 3 ступени, 5 путей, 15 техник, 10 пассивов), ассеты создаёт
  `Tools → Cultivation → Setup Test Assets`, предметы и алхимию — `4. Setup World Content (Game)`.

## Полный круг прогрессии

1. Убить зверя или врага — падает контейнер с мясом, шкурой, рудой или травами.
2. Забрать лут, открыть печь — сварить пилюлю (грейд алхимика влияет на шанс и качество).
3. Принять пилюлю, помедитировать у алтаря — опыт Ци и очки Дао.
4. Прорваться на следующую ступень — открываются новые техники.
5. Назначить технику в `SkillListUI` (вкладка «Техники», Esc → «Навыки»).

## Медитация: риск и последствия

- Медитировать можно где угодно; в `MeditationZone` и у алтаря — ×2 и нулевой риск.
- Вне зоны действует `MeditationRiskConfig`: шанс срыва, дебафы, три стадии —
  **Срыв Ци → Отклонение Ци → Обрыв культивации**. Стадии копятся и спадают со временем.
- Урон, смерть и агрессия врага прерывают медитацию (`IMeditationInterrupter`), последствия
  обрабатывает `MeditationConsequenceService`, панель `DebuffPanelUI` показывает активные дебафы.
- Сейв v2 (`CultivationStateCache`) хранит дебафы, здоровье и прогресс — после загрузки состояние
  восстанавливается тем же.

## Алхимия

- Рецепт — `AlchemyRecipe` (ингредиенты, требования по грейду и Дао, базовый шанс, качество,
  ссылки `veinsOutput`/`cloudOutput`). Грейд алхимика — `AlchemySkill` (12 грейдов с подрангами).
- Качество пилюли — отдельные варианты `ItemData` (`_Normal`/`_Veins`/`_Cloud`), поэтому `Inventory`
  ничего о качестве не знает.
- У печи (`AlchemyStation`) доступен полный набор рецептов и бонус станции; из Esc-меню — только
  простые рецепты со штрафом к шансу. Крафт может провалиться и забрать часть ингредиентов.

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
- VFX берутся из `CultivationVfxConfig` (префабы `Assets/Magic`, назначает меню сетапа).
  Если ссылка пуста — эффект собирается процедурно и один раз предупреждает в консоль.
  Аура (`DaoAuraController`) умеет только частицы: из префаба берётся первая `ParticleSystem`.
- Иероглифы требуют CJK-шрифта: `CultivationConfig.calligraphyFont` + `showChineseNames`.
- Демо-каст наносит урон через `SendMessage("TakeDamamge"/"TakeDammage")` — в проекте встречаются оба
  написания (BossController / PlayerHpController). Настоящие хитбоксы — зона Промта B.
- Деревья, камни и растения в `Game.unity` до сих пор используют `DestructibleObject` и старый
  `DropManager`: сетап привязывает к ним `LootTable`, но находит только объекты с `HarvestableObject`
  (сейчас это враг, одно дерево и три животных). Сообщение об этом — информационное, не ошибка.
- `PlayerController.cs`, `DestructibleObject.cs`, `WeaponManager.cs`, `WeaponData.cs`,
  `DaoAuraController.cs` не изменялись.
