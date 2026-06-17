namespace SugarGuard.Shared.Constants;

/// <summary>
/// Лимиты профиля ребёнка
/// </summary>
public static class ChildProfileLimits
{
    public const double WeightMin = 10;
    public const double WeightMax = 200;
    public const double HeightMin = 60;
    public const double HeightMax = 200;
    public const int AgeMin = 4;
    public const int AgeMax = 18;

    public static bool IsValidWeight(double weight) => weight >= WeightMin && weight <= WeightMax;
    public static bool IsValidHeight(double height) => height >= HeightMin && height <= HeightMax;
    public static bool IsValidAge(int age) => age >= AgeMin && age <= AgeMax;
}
