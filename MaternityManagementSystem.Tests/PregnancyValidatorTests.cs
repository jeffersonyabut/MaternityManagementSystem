
using System;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class PregnancyValidatorTests
{
    [Fact]
    public void Validate_ValidPregnancy_DoesNotThrow()
    {
        var pregnancy = new Pregnancy
        {
            PatientId = 1,
            Status = "Current"
        };

        var exception = Record.Exception(
            () => PregnancyValidator.Validate(pregnancy));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MissingPatientId_ThrowsArgumentException()
    {
        var pregnancy = new Pregnancy
        {
            PatientId = 0,
            Status = "Current"
        };

        Assert.Throws<ArgumentException>(
            () => PregnancyValidator.Validate(pregnancy));
    }

    [Fact]
    public void Validate_InvalidStatus_ThrowsArgumentException()
    {
        var pregnancy = new Pregnancy
        {
            PatientId = 1,
            Status = "InvalidStatus"
        };

        Assert.Throws<ArgumentException>(
            () => PregnancyValidator.Validate(pregnancy));
    }

    [Fact]
    public void Validate_EndDateBeforeLmp_ThrowsArgumentException()
    {
        var pregnancy = new Pregnancy
        {
            PatientId = 1,
            Status = "Completed",
            LastMenstrualPeriod = new DateOnly(2026, 5, 10),
            PregnancyEndDate = new DateOnly(2026, 5, 9)
        };

        Assert.Throws<ArgumentException>(
            () => PregnancyValidator.Validate(pregnancy));
    }

    [Fact]
    public void Validate_EndDateBeforeEstimatedDueDate_DoesNotThrow()
    {
        var pregnancy = new Pregnancy
        {
            PatientId = 1,
            Status = "Completed",
            LastMenstrualPeriod = new DateOnly(2026, 1, 1),
            EstimatedDueDate = new DateOnly(2026, 10, 8),
            PregnancyEndDate = new DateOnly(2026, 8, 1)
        };

        var exception = Record.Exception(
            () => PregnancyValidator.Validate(pregnancy));

        Assert.Null(exception);
    }


    [Fact]
    public void Validate_EstimatedDueDateBeforeLmp_ThrowsArgumentException()
    {
        var pregnancy = new Pregnancy
        {
            PatientId = 1,
            Status = "Current",
            LastMenstrualPeriod = new DateOnly(2026, 5, 10),
            EstimatedDueDate = new DateOnly(2026, 5, 9)
        };

        Assert.Throws<ArgumentException>(
            () => PregnancyValidator.Validate(pregnancy));
    }

    [Fact]
    public void Validate_NotesExceeding2000Characters_ThrowsArgumentException()
    {
        var pregnancy = new Pregnancy
        {
            PatientId = 1,
            Status = "Current",
            Notes = new string('A', 2001)
        };

        Assert.Throws<ArgumentException>(
            () => PregnancyValidator.Validate(pregnancy));
    }

}
