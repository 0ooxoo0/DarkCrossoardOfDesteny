/// <summary>Результат попытки забрать лут — интерфейс решает, что показать игроку.</summary>
public enum LootTakeResult
{
    /// <summary>Предметы уже в инвентаре.</summary>
    Taken,

    /// <summary>Персонаж пошёл к контейнеру, заберёт по прибытии.</summary>
    Walking,

    /// <summary>В инвентаре нет места.</summary>
    NoSpace,

    /// <summary>Контейнер пуст или запрос некорректен.</summary>
    Failed
}
