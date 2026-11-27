# Промт: починить систему скиллов (техники, пассивы, хотбар)

> Скопируй весь текст ниже в агента-исполнителя (Claude Code / Codex / OpenCode).
> Промт самодостаточен: контекст, инварианты, задачи по срезам, приёмка.

---

## Роль и контекст

Ты работаешь в Unity-проекте `DarkCrossoardOfDesteny` (жанр — культивация/уся,
ПК, мышь + клавиатура). В проекте уже есть система культивации: Дао-пути,
реалмы, техники, пассивы, медитация, алхимия, лут. Тебе нужно **довести до
играбельного состояния систему скиллов**: техники должны назначаться,
применяться, наносить урон, пассивы — открываться и влиять на цифры,
назначения — сохраняться.

Сначала прочитай (не меняй):
- `Assets/Scripts/Cultivation/README.md`
- `docs/superpowers/specs/2026-11-17-cultivation-alchemy-loot-design.md` (разделы 7.6, 7.7, 12)
- `docs/superpowers/specs/2026-11-27-skill-system-audit.md` (аудит с точными находками)

Ключевые файлы:
```
Assets/Scripts/Cultivation/Runtime/TechniqueData.cs
Assets/Scripts/Cultivation/Runtime/DaoSystem.cs
Assets/Scripts/Cultivation/Runtime/DaoPath.cs
Assets/Scripts/Cultivation/Runtime/CultivationDefaults.cs
Assets/Scripts/Cultivation/Runtime/CultivationBootstrap.cs
Assets/Scripts/Cultivation/Runtime/CultivationStateCache.cs
Assets/Scripts/Cultivation/Runtime/CultivationAbilityBridge.cs
Assets/Scripts/Cultivation/Runtime/Passives/PassiveSkillService.cs
Assets/Scripts/Cultivation/Runtime/UI/SkillListUI.cs
Assets/Scripts/Cultivation/Runtime/UI/TechniqueListUI.cs
Assets/Scripts/Cultivation/Runtime/UI/CultivationUI.cs
Assets/Scripts/Cultivation/Runtime/UI/CultivationUIRoot.cs
Assets/Scripts/Cultivation/Editor/CultivationSetup.cs
Assets/Scripts/Combat/AbilitySystem.cs
Assets/Scripts/Combat/AbilityData.cs
Assets/Scripts/Combat/WeaponManager.cs
Assets/Scripts/CombatSystem/CombatHitbox.cs
Assets/Scripts/UI/AbilityBarUI.cs
Assets/Scripts/UI/AbilitySlotUI.cs
Assets/Scripts/UI/GameMenuController.cs
Assets/Scripts/Legacy/UiManager.cs
Assets/Scripts/Inventary/Script/SO/ItemData.cs
```

## Главная цель (definition of done)

Игрок может: получить технику → увидеть её в одном понятном экране →
назначить в конкретный слот → применить в бою с уроном → получить пассивку
по прогрессу → увидеть изменение цифр → перезапустить игру и найти
назначения на месте.

## Инварианты (не нарушать)

1. Не ломать инвентарь, экипировку, лут-контейнеры, оружие, его способности
   1–3, хитбоксы, `DestructibleObject`, `PlayerController`.
2. Не менять публичные сигнатуры существующих API без необходимости.
   Новые методы — аддитивные. Если сигнатуру менять всё же нужно — обнови
   всех вызывающих и напиши об этом в отчёте.
3. Не удалять `CultivationCastController`/`CultivationQuickBarUI`: это
   fallback для сцен без `AbilitySystem`.
4. Сохранение должно читать старый формат (`Cultivation.Save.v2`) без падений.
5. Никаких новых художественных ассетов: иконки/руны — процедурные
   плейсхолдеры.
6. Работай срезами: после каждого — компиляция, проверка в Play mode,
   атомарный коммит. Не смешивай срезы в одном коммите.
7. Если доступен Unity MCP — используй `read_console` после каждой правки
   C# и `manage_editor` для Play mode. Если нет — требуй ручной проверки.

---

## Срез 1. Контент и пассивы (P0)

### 1.1 Пересоздать ассеты техник

Проблема: в коде 10 пассивов, в ассетах 0. `Dao_Sword.asset` содержит 3 техники
вместо 6.

Сделай:
- Убедись, что `CultivationDefaults.GetTechniques(type)` возвращает и активные,
  и пассивные техники (сейчас возвращает — проверь).
- Обнови `CultivationSetup.ValidateSetup`: требовать ≥ 20 техник всего,
  ≥ 1 пассив на каждый Дао, `passiveValue > 0` у пассивов.
- Запусти `Tools → Cultivation → Setup Test Assets` (через Unity MCP
  `execute_menu_item` или попроси пользователя). Ожидание:
  `Assets/Scripts/Cultivation/Data/Techniques/Technique_sword_passive_1.asset`
  и т.д., в каждом `Dao_*.asset` 5–6 ссылок.
- Проверь диффом, что конфиг не потерял нужные поля.

### 1.2 Активировать пассивы по прогрессу

Проблема: `PassiveSkillService` берёт только `dao.GetLearnedTechniques()`,
а свитков пассивов нет. В результате пассивы недостижимы.

Сделай:
- В `PassiveSkillService.TryAdd` считать пассив активным, если
  `Kind == Passive`, `PassiveValue > 0` и выполнены `RequiredDaoLevel`
  и `RequiredRealmIndex`. «Выучен» (`HasLearnedTechnique`) — дополнительный
  обход требований, а не обязательное условие.
- Пересчёт оставить на событиях (`OnTechniqueLearned`, `OnDaoLevelChanged`,
  `OnRealmChanged`, `OnStageChanged`), не в `Update`.
- Убедись, что пассив чужого (закрытого) Дао не активируется: требование
  `IsDaoUnlocked`.
- В `SkillListUI` для пассивов показывать статус «Активна» / «Нужен Дао X» /
  «Нужен реалм Y», а не «Можно изучить».

### 1.3 Побочный эффект новых ассетов

После появления пассивов в ассетах `TechniqueListUI` начнёт показывать их
в сетке с кнопкой «В слот», а `CultivationAbilityBridge` откажет
(«пассивная техника, в слот не кладётся»).

Сделай:
- в `TechniqueListUI` отфильтровать пассивы из вкладки активных техник
  (или показать их без кнопки назначения);
- в `AutoRegisterStarterTechnique` брать первую **непассивную** доступную
  технику (не просто `available[0]`).

### 1.4 Обновить README

`Assets/Scripts/Cultivation/README.md`: убрать ложное «слот 1 уже занят»,
описать реальный старт, вкладку «Пассивные» и правило активации пассивов.

**Приёмка среза 1:** вкладка «Пассивные» показывает 10 пассивов; при
Дао 3 + реалм 1 «Несокрушимость» становится активной; HP/реген в UI меняются.

---

## Срез 2. Боевая корректность (P0)

### 2.1 Магия должна наносить урон

Проблема: `AbilitySystem.CreateMagicHitbox` создаёт trigger с тегом
`DammageObject`; урон получает только `DestructibleObject` (и то уничтожает
себя). Враги — `IDamageable` через `CombatHitbox`.

Сделай в `AbilitySystem.CreateMagicHitbox`:
- добавь на зону `CombatHitbox`;
- `Configure(power * DamageMultiplier, CombatTeam.Player, ownerRoot)`,
  где `power = Mathf.Max(0, ability power)`. Если `AbilityData` не хранит
  `power`, добавь поле `public int power;` и заполняй его в
  `CultivationAbilityBridge.ToAbilityData` из `technique.Power`;
- `targetLayers = ability.damageLayers`;
- `SetActive(true)`;
- оставь тег `DammageObject` для пропов;
- уничтожай зону через `magicHitboxLifetime`.

Убедись, что `CombatHitbox` не бьёт владельца (`OwnerRoot`) и одну цель
дважды (`hitTargets`).

### 2.2 Melee должен использовать силу техники

Проблема: `ExecuteMelee` играет анимацию и бьёт штатным хитбоксом оружия,
`TechniqueData.Power` игнорируется.

Сделай:
- добавь в `CombatHitbox` поле/метод override урона
  (`SetDamageOverride(float? value)`), которое применяется в `TryDamage`
  вместо `damage`;
- добавь в `WeaponManager` метод `SetDamageOverride(float? value)`, который
  прокидывает override в активный хитбокс (меч или кулаки);
- в `AbilitySystem.ExecuteMelee` перед атакой:
  `override = ability.power > 0 ? ability.power * DamageMultiplier : (float?)null`;
- после окончания каста (в `finally`/после цикла) сбрось override в `null`.

Правило: `Power = 0` (Block) — урон оружия; `Power > 0` — урон техники.

### 2.3 Применить `DamageMultiplier`

- В melee — через override (см. 2.2).
- В magic — при создании `CombatHitbox` (см. 2.1).
- Убедись, что `PassiveSkillService` пишет `SpellPowerPercent` в
  `AbilitySystem.DamageMultiplier` (уже пишет).

### 2.4 `MeleeDamagePercent`

Проблема: пассив «+10 % урона оружием» нигде не читается.

Сделай:
- добавь в `WeaponManager` (или `CombatHitbox`) множитель урона оружия,
  который выставляет `PassiveSkillService`:
  `weaponManager.PlayerDamageMultiplier = GetMultiplier(MeleeDamagePercent)`;
- применяй его к урону оружия, но не к override техники (чтобы не удвоить).

### 2.5 Вернуть Ци при провале каста

Если `PlayCastAnimation` вернул `false`, верни `qi.Restore(cost)` и не ставь КД.

### 2.6 Block/Aura — не обманывать игрока

Проблема: `Kind` нигде не ветвится, кроме Passive; «Стойка клинка» — обычный
удар с `Power 0`, «Дождь тысячи клинков» — один удар.

На MVP выбери один вариант и зафиксируй в отчёте:
- **A (рекомендуется):** скрыть `Aura` и `Block` из назначаемых техник,
  в UI показывать «Механика в разработке»; данные не удалять.
- **B:** реализовать минимально: Aura — временный бафф (нужно добавить
  `duration` в `TechniqueData`), Block — `PlayerBlockController` на N секунд.

**Приёмка среза 2:** «Огненный шар» наносит урон врагу; «Удар клинка» бьёт
сильнее кулаков; «+10 % силы техник» и «+10 % урона оружием» дают измеримую
разницу; Ци не теряется при провале каста.

---

## Срез 3. Слоты, сохранение, обратная связь (P0/P1)

### 3.1 Сохранение назначений

Сделай:
- `CultivationSnapshot` v3: `public string[] techniqueSlotIds = new string[2];`
  (индекс 0 = слот 4, индекс 1 = слот 5);
- `CultivationStateCache.Capture(...)` принимает `CultivationAbilityBridge`
  (необязательный параметр) и сохраняет ID по слотам;
- `TryRestore` после `Dao.RestoreState` восстанавливает назначения через
  новый метод `CultivationAbilityBridge.RestoreAssignments(string[] ids)`;
- порядок в `CultivationBootstrap`: `TryRestore` → `SetupTechniqueOutput`
  → `RestoreAssignments` (или восстановление внутри `SetupTechniqueOutput`);
- миграция: старый JSON без поля — пустой массив, без исключений;
- сохранение при назначении/снятии (`OnTechniqueRegistered` и unassign).

### 3.2 Стартовая техника

- После инициализации моста, если сохранённых слотов нет и
  `Bridge.RegisteredCount == 0`, вызови `AutoRegisterStarterTechnique()`
  (для демо — `AutoAssignStarterTechnique()`).
- Убедись, что метод не дублирует технику при повторном вызове.

### 3.3 Явное управление слотами

Сделай:
- `CultivationAbilityBridge.AssignTechnique(TechniqueData, int slotIndex)` —
  валидировать слот 3/4, заменять содержимое только по явному запросу;
- `UnassignTechnique(int slotIndex)`;
- повторное назначение той же техники в тот же слот — no-op, не переносить;
- при занятом слоте UI спрашивает замену (или показывает две кнопки
  «В слот 4» / «В слот 5» с пометкой «занято: X»);
- при отсутствии свободных слотов — сообщение игроку, не только `Debug.Log`.

### 3.4 Фидбек блокировки

- `AbilityBarUI` подписать на `AbilitySystem.OnAbilityActivationBlocked`
  и показывать причину тостом 2–3 с (создай простой TMP-лейбл, не тяни
  новые ассеты).
- `AbilitySlotUI`: при наведении показывать `BlockedReason` (tooltip).

### 3.5 Tooltip и читаемость хотбара

- Tooltip слота: имя, Дао, тип, ранг, Ци, КД, `Power`, требования,
  «Активный Дао: X».
- Если `TechniqueData.icon == null`, рисовать руну: первая буква названия
  на фоне цвета Дао; на слоте дублировать короткое имя.
- В `SkillListUI` показывать `Power` и требования, а не только Ци/КД.

### 3.6 Чужой Дао

- В UI назначения, если `technique.DaoType != dao.ActiveDao`, показывать
  предупреждение: «Техника школы X; активный Дао — Y. Не сработает, пока
  не сменишь Дао у алтаря».
- Разрешать назначение, но помечать слот (бейдж/приглушение).

**Приёмка среза 3:** назначение в слот 4/5, «Снять», сохранение после
перезапуска, стартовая техника на месте, понятные сообщения при блокировке.

---

## Срез 4. Консистентность UI (P1)

1. Сделать `SkillListUI` единственным экраном скиллов:
   вкладки «Техники»/«Пассивные» + фильтр по Дао + назначение/снятие.
   `CultivationUI.OpenTechniques` → `Skills.Open()`.
   `TechniqueListUI` оставить только для демо-ветки или удалить после проверки.
2. Убрать расхождение правил: назначение активной техники не требует
   «выучено»; «выучено» — бонус-обход требований.
3. Починить Esc → «Культивация»: в `GameMenuController.OpenCultivation`
   закрывать паузу (`UiManager.CloseUI()`) перед открытием панели,
   либо разрешить `CultivationUI.Open` при открытом `UiManager`, не снимая паузу.
4. `SkillListUI` должен ставить паузу, как `CultivationUI` (если открыт не из
   паузы), и корректно её снимать.

**Приёмка среза 4:** оба входа в скиллы ведут в один экран с одинаковыми
правилами; «Культивация» из паузы открывается.

---

## Срез 5. Контент и техдолг (P1/P2)

1. Добавить `Seed_Body.asset`, `Seed_Thunder.asset` (копия `Seed_Fire`,
   `useKind = DaoSeed`, `unlocksDao`), чтобы все 5 школ были открываемы.
   Способ получения (лут/магазин) — отдельная задача, но ассеты должны быть.
2. Уровни Дао 5–10: либо добавить техники ранга 4+, либо снизить `maxLevel`
   до 4 и показывать «школа освоена».
3. Заполнить `description` техник механикой (урон, радиус, длительность),
   а не заглушкой «Name — Kind техника пути X».
4. UI очков Дао: «ур. 3 · 4/10 до ур. 4» вместо «очки 40».
5. Скорость изучения (`OnTechniqueLearningChanged`): либо реализовать
   (время изучения свитка), либо убрать из публичного API/UI.
6. EditMode-тесты:
   - `DaoSystem`: доступность, выучка, restore;
   - `PassiveSkillService`: множители при выполненных требованиях;
   - `CultivationAbilityBridge`: назначение/замена/снятие/restore;
   - `CultivationStateCache`: v2 → v3 без падений.

---

## Порядок работы и отчёт

1. Прочитай аудит и перечисленные файлы, составь короткий план по срезам.
2. Выполняй срезы по порядку. После каждого:
   - компиляция без ошибок (проверь `read_console`);
   - ручной сценарий из приёмки;
   - атомарный коммит с сообщением `skills: <срез>`.
3. В конце дай отчёт:
   - что изменено (файлы + суть);
   - какие решения приняты (особенно Block/Aura, объединение экранов);
   - что проверено вручную и какие результаты;
   - что осталось и почему;
   - список коммитов.

## Критерии приёмки (полный сценарий)

1. Первый запуск: слот 4 занят «Ударом клинка»; клавиша 4 кастует, тратит Ци,
   наносит урон, запускает КД.
2. «Огненный шар» наносит урон врагу на дистанции.
3. «Удар клинка» (`Power 35`) бьёт сильнее базовых кулаков (`12`).
4. Вкладка «Пассивные»: 10 пассивов; при Дао 3 + реалм 1 пассив активен,
   цифры HP/Ци/урона меняются.
5. Назначение в слот 4/5, «Снять», сохранение после перезапуска.
6. Чужой Дао: понятное сообщение, слот помечен, tooltip объясняет.
7. Занятый слот требует замены; повторное назначение не переносит технику.
8. Один экран скиллов с одинаковыми правилами для обоих входов.
9. Esc → «Культивация» открывает панель.
10. В консоли нет ошибок `[Cultivation]`/`[AbilitySystem]` в штатном сценарии.

## Чего НЕ делать

- Не переписывать `AbilitySystem` целиком: только точечные правки.
- Не менять слоты оружия 1–3 и его способности.
- Не трогать `PlayerController`, `DestructibleObject`, `Garderob`, лут.
- Не добавлять новые пакеты и художественные ассеты.
- Не «улучшать» баланс цифр без запроса: только механика и UI.
