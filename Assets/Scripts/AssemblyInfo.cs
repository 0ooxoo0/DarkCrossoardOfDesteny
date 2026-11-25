using System.Runtime.CompilerServices;

// Тестовая сборка проверяет внутренние члены игровой логики напрямую,
// без отражения: точечное расширение доступа вместо доступа к private.
[assembly: InternalsVisibleTo("DarkCrossroad.EditModeTests")]
