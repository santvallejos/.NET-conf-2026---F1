using System.ComponentModel.DataAnnotations;

namespace PitWall.Replay;

/// <summary>Sección <c>Replay</c> de la configuración (docs/configuration.md §1).</summary>
public sealed class ReplayOptions : IValidatableObject
{
    public const string SectionName = "Replay";

    public static readonly IReadOnlyList<int> DefaultSpeeds = [1, 5, 20];

    /// <summary>
    /// Velocidades permitidas. Sin valor por defecto en la propiedad: el binder de configuración añade los
    /// elementos de un array a los ya existentes, así que el default se aplica en <see cref="GetSpeeds"/>.
    /// </summary>
    public int[] Speeds { get; set; } = [];

    [Range(1, 60_000)]
    public int TickMs { get; set; } = 250;

    public IReadOnlyList<int> GetSpeeds() => Speeds.Length == 0 ? DefaultSpeeds : [.. Speeds.Distinct().Order()];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Speeds.Any(speed => speed <= 0))
        {
            yield return new ValidationResult("Every replay speed must be greater than zero.", [nameof(Speeds)]);
        }
    }
}
