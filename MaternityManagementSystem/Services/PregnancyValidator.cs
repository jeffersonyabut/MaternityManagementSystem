
using System;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public static class PregnancyValidator
{
    public static void Validate(Pregnancy pregnancy)
    {
        ArgumentNullException.ThrowIfNull(pregnancy);

        if (pregnancy.PatientId <= 0)
        {
            throw new ArgumentException("A valid patient must be selected.");
        }

        if (pregnancy.LastMenstrualPeriod.HasValue &&
            pregnancy.PregnancyEndDate.HasValue &&
            pregnancy.PregnancyEndDate.Value < pregnancy.LastMenstrualPeriod.Value)
        {
            throw new ArgumentException(
                "The pregnancy end date cannot be earlier than the last menstrual period.");
        }

        if (pregnancy.EstimatedDueDate.HasValue &&
            pregnancy.LastMenstrualPeriod.HasValue &&
            pregnancy.EstimatedDueDate.Value < pregnancy.LastMenstrualPeriod.Value)
        {
            throw new ArgumentException(
                "The estimated due date cannot be earlier than the last menstrual period.");
        }

        if (pregnancy.Status is null ||
            pregnancy.Status.Trim() is not ("Current" or "Completed" or "Other"))
        {
            throw new ArgumentException("The pregnancy status is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(pregnancy.Notes) &&
            pregnancy.Notes.Trim().Length > 2000)
        {
            throw new ArgumentException("Notes cannot exceed 2000 characters.");
        }
    }
}
