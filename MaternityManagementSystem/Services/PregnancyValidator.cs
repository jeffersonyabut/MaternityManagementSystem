
using System;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public static class PregnancyValidator
{
    private static readonly string[] ValidStatuses =
    {
        "Current",
        "Completed",
        "Other"
    };

    private static readonly string[] ValidOutcomes =
    {
        "Live Birth",
        "Stillbirth",
        "Miscarriage",
        "Ectopic Pregnancy",
        "Molar Pregnancy",
        "Other"
    };

    private static readonly string[] ValidDeliveryMethods =
    {
        "Vaginal",
        "Cesarean Section",
        "Assisted Vaginal",
        "Other"
    };

    public static void Validate(Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        if (pregnancy.PatientId <= 0)
        {
            throw new ArgumentException(
                "A valid patient must be selected.");
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        if (pregnancy.LastMenstrualPeriod.HasValue &&
            pregnancy.LastMenstrualPeriod.Value > today)
        {
            throw new ArgumentException(
                "The last menstrual period cannot be in the future.");
        }

        if (pregnancy.PregnancyEndDate.HasValue &&
            pregnancy.PregnancyEndDate.Value > today)
        {
            throw new ArgumentException(
                "The pregnancy end date cannot be in the future.");
        }

        if (pregnancy.LastMenstrualPeriod.HasValue &&
            pregnancy.PregnancyEndDate.HasValue &&
            pregnancy.PregnancyEndDate.Value <
            pregnancy.LastMenstrualPeriod.Value)
        {
            throw new ArgumentException(
                "The pregnancy end date cannot be earlier than the last menstrual period.");
        }

        if (pregnancy.EstimatedDueDate.HasValue &&
            pregnancy.LastMenstrualPeriod.HasValue &&
            pregnancy.EstimatedDueDate.Value <
            pregnancy.LastMenstrualPeriod.Value)
        {
            throw new ArgumentException(
                "The estimated due date cannot be earlier than the last menstrual period.");
        }

        if (string.IsNullOrWhiteSpace(pregnancy.Status) ||
            !Array.Exists(
                ValidStatuses,
                status => string.Equals(
                    status,
                    pregnancy.Status.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "The pregnancy status is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(pregnancy.PregnancyOutcome) &&
            !Array.Exists(
                ValidOutcomes,
                outcome => string.Equals(
                    outcome,
                    pregnancy.PregnancyOutcome.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "The pregnancy outcome is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(pregnancy.DeliveryMethod) &&
            !Array.Exists(
                ValidDeliveryMethods,
                method => string.Equals(
                    method,
                    pregnancy.DeliveryMethod.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "The delivery method is invalid.");
        }

        if (pregnancy.GestationalAgeAtEndWeeks is < 0 or > 45)
        {
            throw new ArgumentException(
                "Gestational age in weeks must be between 0 and 45.");
        }

        if (pregnancy.GestationalAgeAtEndDays is < 0 or > 6)
        {
            throw new ArgumentException(
                "Additional gestational age days must be between 0 and 6.");
        }

        if (pregnancy.NumberOfInfants is < 1 or > 10)
        {
            throw new ArgumentException(
                "The number of infants must be between 1 and 10.");
        }

        if (pregnancy.LivingChildrenCount is < 0)
        {
            throw new ArgumentException(
                "The number of living children cannot be negative.");
        }

        if (pregnancy.NumberOfInfants.HasValue &&
            pregnancy.LivingChildrenCount.HasValue &&
            pregnancy.LivingChildrenCount.Value >
            pregnancy.NumberOfInfants.Value)
        {
            throw new ArgumentException(
                "The number of living children cannot exceed the number of infants.");
        }

        if (!string.IsNullOrWhiteSpace(pregnancy.Complications) &&
            pregnancy.Complications.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Complications cannot exceed 2000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(pregnancy.Notes) &&
            pregnancy.Notes.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Notes cannot exceed 2000 characters.");
        }
    }
}
