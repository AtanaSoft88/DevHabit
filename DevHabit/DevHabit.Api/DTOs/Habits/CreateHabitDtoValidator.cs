using DevHabit.Api.Entities;
using FluentValidation;

namespace DevHabit.Api.DTOs.Habits;

public sealed class CreateHabitDtoValidator : AbstractValidator<CreateHabitDto>
{
    private static readonly string[] AllowedUnits =
    [
        "minutes","hours","steps","km","cal",
        "pages","books","tasks","sessions"
    ];

    private static readonly string[] AllowedUnitsForBinaryHabits = ["sessions", "tasks"];

    public CreateHabitDtoValidator()
    {
        RuleFor(dto => dto.Name)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(100)
            .WithMessage("Habit name must be between 3 and 100 characters.");

        RuleFor(dto => dto.Description)
            //.MinimumLength(3)
            //.When(dto => dto.Description is not null)
            //.WithMessage("Description cannot be less than 3 characters.")
            .MaximumLength(500)
            .When(dto => dto.Description is not null)
            .WithMessage("Description cannot exceed 500 characters.");

        RuleFor(dto => dto.Type)
            .IsInEnum()
            .WithMessage("Invalid habit type.");

        //Frequency validation
        RuleFor(dto => dto.Frequency.Type)
            .IsInEnum()
            .WithMessage("Invalid frequency period.");

        RuleFor(dto => dto.Frequency.TimesPerPeriod)
            .GreaterThan(0)
            .WithMessage("Times per period must be greater than zero.");

        //Target validation 
        RuleFor(dto => dto.Target.Value)
            .GreaterThan(0)
            .WithMessage("Target value must be greater than zero.");
        
        RuleFor(dto => dto.Target.Unit)
            .NotEmpty()
            .Must(unit => AllowedUnits.Contains(unit.ToLowerInvariant()))
            .WithMessage($"Unit must be one of the following: {string.Join(", ", AllowedUnits)}.");

        // EndDate validation: if provided, it must be in the future
        RuleFor(dto => dto.EndDate)
            .Must(date => date is null || date.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("End date must be in the future.");

        // Milestone validation: if provided, it must have a valid unit and value
        When(dto => dto.Milestone is not null, () => 
        {
            RuleFor(dto => dto.Milestone!.Target)
            .GreaterThan(0)
            .WithMessage("Milestone target must be greater than zero.");
        });

        RuleFor(dto => dto.Target.Unit)
            .Must((dto, unit) => IsTargetCompatibleWithType(dto.Type, unit))
            .WithMessage("Target unit is not compatible with the habit type");
    }

    private static bool IsTargetCompatibleWithType(HabitType type, string unit)
    {
        string normalizedUnit = unit.ToLowerInvariant();    

        return type switch
        {
            //Binary habits should only use count-based units.
            HabitType.Binary => AllowedUnitsForBinaryHabits.Contains(normalizedUnit),

            //Measurable habits can use any of the allowed units.
            HabitType.Measurable => AllowedUnits.Contains(normalizedUnit),            
            _ => false // For HabitType.None or any other undefined types, we consider the unit incompatible.
        };
    }
}
