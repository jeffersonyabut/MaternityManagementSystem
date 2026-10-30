
using System;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public static class PrenatalVisitValidator
{
    public static void Validate(PrenatalVisit prenatalVisit)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisit);

        if (prenatalVisit.PregnancyId <= 0)
        {
            throw new ArgumentException(
                "A valid pregnancy record must be selected.");
        }

        if (prenatalVisit.VisitDate == default)
        {
            throw new ArgumentException(
                "A valid prenatal visit date is required.",
                nameof(prenatalVisit));
        }

        if (!string.IsNullOrWhiteSpace(prenatalVisit.Notes) &&
            prenatalVisit.Notes.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Visit notes cannot exceed 2000 characters.");
        }
    }
}
