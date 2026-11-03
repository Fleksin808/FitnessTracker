using FitnessTracker.Domain.BodyMeasurements;

namespace FitnessTracker.Features.BodyMeasurements;

/// <summary>Text pro UI oddělený od stabilní hodnoty, kterou ukládá doména.</summary>
public sealed record BodyPartOption(BodyPart Value, string Name)
{
    public static IReadOnlyList<BodyPartOption> All { get; } = Array.AsReadOnly<BodyPartOption>([
        new(BodyPart.Waist, "Pas"),
        new(BodyPart.Chest, "Hrudník"),
        new(BodyPart.Hips, "Boky"),
        new(BodyPart.Neck, "Krk"),
        new(BodyPart.LeftUpperArm, "Levá paže"),
        new(BodyPart.RightUpperArm, "Pravá paže"),
        new(BodyPart.LeftThigh, "Levé stehno"),
        new(BodyPart.RightThigh, "Pravé stehno"),
        new(BodyPart.LeftCalf, "Levé lýtko"),
        new(BodyPart.RightCalf, "Pravé lýtko")
    ]);
}
