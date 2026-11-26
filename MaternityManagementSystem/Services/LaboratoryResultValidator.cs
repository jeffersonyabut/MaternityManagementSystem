
using System;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public static class LaboratoryResultValidator
{
    public static void ValidateTransition(
        LaboratoryResultStatus? currentStatus,
        LaboratoryResultStatus? updatedStatus)
    {
        if (currentStatus is null || updatedStatus is null)
        {
            return;
        }

        bool isAllowed = currentStatus == updatedStatus ||
            (currentStatus == LaboratoryResultStatus.Requested &&
             updatedStatus is LaboratoryResultStatus.AwaitingResult or
                 LaboratoryResultStatus.Resulted) ||
            (currentStatus == LaboratoryResultStatus.AwaitingResult &&
             updatedStatus == LaboratoryResultStatus.Resulted);

        if (!isAllowed)
        {
            throw new ArgumentException(
                $"A laboratory result cannot move backward from {currentStatus} to {updatedStatus}.");
        }
    }

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

        if (laboratoryResult.Status is null ||
            !Enum.IsDefined(laboratoryResult.Status.Value))
        {
            throw new ArgumentException(
                "A valid laboratory result status is required.");
        }

        if (laboratoryResult.RequestedDate is { } requestedDate &&
            requestedDate == default)
        {
            throw new ArgumentException(
                "A valid laboratory requested date is required.");
        }

        if (laboratoryResult.RequestedDate is null &&
            laboratoryResult.Status != LaboratoryResultStatus.Resulted)
        {
            throw new ArgumentException(
                "A valid laboratory requested date is required.");
        }

        if (laboratoryResult.TestDate is { } testDate &&
            testDate == default)
        {
            throw new ArgumentException(
                "A valid laboratory test date is required.");
        }

        if (laboratoryResult.TestDate.HasValue &&
            laboratoryResult.TestDate.Value >
            DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException(
                "Laboratory test date cannot be in the future.");
        }

        bool hasResult =
            !string.IsNullOrWhiteSpace(laboratoryResult.Result);

        switch (laboratoryResult.Status.Value)
        {
            case LaboratoryResultStatus.Requested:
                if (laboratoryResult.TestDate.HasValue || hasResult)
                {
                    throw new ArgumentException(
                        "A requested test cannot have a performed date or result.");
                }

                break;

            case LaboratoryResultStatus.AwaitingResult:
                if (!laboratoryResult.TestDate.HasValue)
                {
                    throw new ArgumentException(
                        "A performed date is required while awaiting a result.");
                }

                if (hasResult)
                {
                    throw new ArgumentException(
                        "A test awaiting a result cannot already have a result.");
                }

                break;

            case LaboratoryResultStatus.Resulted:
                if (!laboratoryResult.TestDate.HasValue)
                {
                    throw new ArgumentException(
                        "A performed date is required for a resulted test.");
                }

                if (!hasResult)
                {
                    throw new ArgumentException(
                        "A laboratory result is required for a resulted test.");
                }

                if (laboratoryResult.Result!.Trim().Length > 2000)
                {
                    throw new ArgumentException(
                        "Laboratory result cannot exceed 2000 characters.");
                }

                break;
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
