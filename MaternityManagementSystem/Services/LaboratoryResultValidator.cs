
using System;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public static class LaboratoryResultValidator
{
    public static void Validate(LaboratoryResult laboratoryResult)
    {
        ArgumentNullException.ThrowIfNull(laboratoryResult);

        if (laboratoryResult.PatientId <= 0)
        {
            throw new ArgumentException(
                "A valid patient must be selected.");
        }

        if (laboratoryResult.PrenatalVisitId is <= 0)
        {
            throw new ArgumentException(
                "A valid prenatal visit must be selected.");
        }

        if (string.IsNullOrWhiteSpace(laboratoryResult.TestName))
        {
            throw new ArgumentException(
                "Laboratory test name is required.");
        }

        if (laboratoryResult.TestName.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Laboratory test name cannot exceed 150 characters.");
        }

        if (laboratoryResult.TestDate == default)
        {
            throw new ArgumentException(
                "A valid laboratory test date is required.");
        }

        if (laboratoryResult.TestDate > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException(
                "Laboratory test date cannot be in the future.");
        }

        if (string.IsNullOrWhiteSpace(laboratoryResult.Result))
        {
            throw new ArgumentException(
                "Laboratory result is required.");
        }

        if (laboratoryResult.Result.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Laboratory result cannot exceed 2000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(laboratoryResult.Unit) &&
            laboratoryResult.Unit.Trim().Length > 50)
        {
            throw new ArgumentException(
                "Laboratory result unit cannot exceed 50 characters.");
        }

        if (!string.IsNullOrWhiteSpace(laboratoryResult.ReferenceRange) &&
            laboratoryResult.ReferenceRange.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Reference range cannot exceed 200 characters.");
        }

        if (!string.IsNullOrWhiteSpace(laboratoryResult.Notes) &&
            laboratoryResult.Notes.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Laboratory notes cannot exceed 2000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(laboratoryResult.ArchiveReason) &&
            laboratoryResult.ArchiveReason.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Archive reason cannot exceed 1000 characters.");
        }

        if (laboratoryResult.IsArchived &&
            laboratoryResult.ArchivedAtUtc == null)
        {
            throw new ArgumentException(
                "An archived laboratory record must have an archive date.");
        }

        if (!laboratoryResult.IsArchived &&
            laboratoryResult.ArchivedAtUtc != null)
        {
            throw new ArgumentException(
                "A non-archived laboratory record cannot have an archive date.");
        }
    }
}
