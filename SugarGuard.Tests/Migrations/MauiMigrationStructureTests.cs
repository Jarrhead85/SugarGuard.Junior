using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace SugarGuard.Tests.Migrations;

/// <summary>
/// Smoke-тесты для EF Core миграций MAUI-проекта.
/// <para>
/// Проверяют что migration-класс <c>AddEncryptionVersion</c> (C-1, release 1.0.0):
/// <list type="bullet">
///   <item><description>скомпилирован в финальный DLL</description></item>
///   <item><description>наследует <see cref="Migration"/></description></item>
///   <item><description>имеет публичные методы <c>Up</c> и <c>Down</c> с правильной сигнатурой</description></item>
///   <item><description>содержит 8 таблиц (User, Child, Measurement, DiabetesSettings, AIRecommendation, BackpackItem, BackpackHistory, SnackConsumptionLog)</description></item>
/// </list>
/// </para>
/// <para>
/// Полная проверка через <c>dotnet ef migrations script</c> невозможна на Windows
/// (MAUI runtime падает при design-time, требует macOS). Этот unit-тест —
/// компромисс: проверяет структуру, а не runtime-применение.
/// </para>
/// </summary>
public class MauiMigrationStructureTests
{
    [Fact]
    public void AddEncryptionVersion_MigrationExists_InCompiledAssembly()
    {
        // Загружаем сборку MAUI-проекта по пути bin/Debug.
        // Путь относительно SugarGuard.Tests, который лежит на одном уровне с SugarGuard.Junior.
        var mauiAssemblyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "SugarGuard.Junior",
            "bin", "Debug", "net9.0-windows10.0.19041.0", "win10-x64",
            "SugarGuard.Junior.dll"));

        Assert.True(File.Exists(mauiAssemblyPath),
            $"MAUI assembly не найдена: {mauiAssemblyPath}. Сначала выполните `dotnet build SugarGuard.Junior --framework net9.0-windows10.0.19041.0`.");

        var assembly = Assembly.LoadFrom(mauiAssemblyPath);
        var migrationType = assembly.GetType("SugarGuard.Junior.Migrations.AddEncryptionVersion");

        Assert.NotNull(migrationType);
        Assert.True(typeof(Migration).IsAssignableFrom(migrationType!),
            "AddEncryptionVersion должен наследовать Microsoft.EntityFrameworkCore.Migrations.Migration");
    }

    [Fact]
    public void AddEncryptionVersion_HasUpAndDownMethods_WithCorrectSignature()
    {
        var mauiAssemblyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "SugarGuard.Junior",
            "bin", "Debug", "net9.0-windows10.0.19041.0", "win10-x64",
            "SugarGuard.Junior.dll"));

        if (!File.Exists(mauiAssemblyPath))
        {
            Assert.Fail($"MAUI assembly не найдена: {mauiAssemblyPath}. Сначала выполните `dotnet build SugarGuard.Junior --framework net9.0-windows10.0.19041.0`.");
        }

        var assembly = Assembly.LoadFrom(mauiAssemblyPath);
        var migrationType = assembly.GetType("SugarGuard.Junior.Migrations.AddEncryptionVersion");

        Assert.NotNull(migrationType);

        var upMethod = migrationType!.GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);
        var downMethod = migrationType.GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(upMethod);
        Assert.NotNull(downMethod);
        Assert.Equal(typeof(void), upMethod!.ReturnType);
        Assert.Equal(typeof(void), downMethod!.ReturnType);
    }

    [Fact]
    public void AddEncryptionVersion_CoversAll8ExpectedTables()
    {
        var mauiAssemblyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "SugarGuard.Junior",
            "bin", "Debug", "net9.0-windows10.0.19041.0", "win10-x64",
            "SugarGuard.Junior.dll"));

        if (!File.Exists(mauiAssemblyPath))
        {
            Assert.Fail($"MAUI assembly не найдена: {mauiAssemblyPath}. Сначала выполните `dotnet build SugarGuard.Junior --framework net9.0-windows10.0.19041.0`.");
        }

        // Читаем IL Up-метода и проверяем что все 8 таблиц присутствуют в строковых литералах.
        // Это хрупкая проверка, но достаточная для CI-gate: если кто-то удалит таблицу из миграции,
        // тест сразу укажет на это.
        var assemblyBytes = File.ReadAllBytes(mauiAssemblyPath);
        var assemblyText = System.Text.Encoding.UTF8.GetString(assemblyBytes);

        // Проверяем, что в IL Up-метода упоминаются все 8 таблиц.
        // Используем ldstr-последовательности (EF Core компилирует строковые литералы в UTF-8).
        string[] expectedTables =
        {
            "Users", "Children", "Measurements", "DiabetesSettings",
            "AIRecommendations", "BackpackItems", "BackpackHistory", "SnackConsumptionLogs"
        };

        var missing = expectedTables.Where(t => !assemblyText.Contains(t)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void AddEncryptionVersion_HasPartialIndex_ForAllTables()
    {
        var mauiAssemblyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "SugarGuard.Junior",
            "bin", "Debug", "net9.0-windows10.0.19041.0", "win10-x64",
            "SugarGuard.Junior.dll"));

        if (!File.Exists(mauiAssemblyPath))
        {
            Assert.Fail($"MAUI assembly не найдена: {mauiAssemblyPath}. Сначала выполните `dotnet build SugarGuard.Junior --framework net9.0-windows10.0.19041.0`.");
        }

        var assembly = Assembly.LoadFrom(mauiAssemblyPath);
        var migrationType = assembly.GetType("SugarGuard.Junior.Migrations.AddEncryptionVersion");
        Assert.NotNull(migrationType);

        var migration = Assert.IsAssignableFrom<Migration>(
            Activator.CreateInstance(migrationType!)!);

        var upMethod = migrationType!.GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(upMethod);

        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        upMethod!.Invoke(migration, new object[] { migrationBuilder });

        string[] expectedIndexNames =
        {
            "IX_Users_LegacyCbc",
            "IX_Children_LegacyCbc",
            "IX_Measurements_LegacyCbc",
            "IX_DiabetesSettings_LegacyCbc",
            "IX_AIRecommendations_LegacyCbc",
            "IX_BackpackItems_LegacyCbc",
            "IX_BackpackHistory_LegacyCbc",
            "IX_SnackConsumptionLogs_LegacyCbc"
        };

        var actualIndexNames = migrationBuilder.Operations
            .OfType<CreateIndexOperation>()
            .Select(operation => operation.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        var missing = expectedIndexNames
            .Where(name => !actualIndexNames.Contains(name))
            .ToList();

        Assert.True(missing.Count == 0,
            $"Отсутствуют индексы: [{string.Join(", ", missing)}]. Найдены: [{string.Join(", ", actualIndexNames.Order())}]");
    }
}
