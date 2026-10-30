using System;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Services;
using Xunit;

namespace MaternityManagementSystem.Tests;

public class PrenatalVisitValidatorTests
{
    [Fact]
    public void Validate_ValidVisit_DoesNotThrow()
    {
        var visit = new PrenatalVisit
        {
            PregnancyId = 1,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = "Routine prenatal visit."
        };

        var exception = Record.Exception(
            () => PrenatalVisitValidator.Validate(visit));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MissingPregnancyId_ThrowsArgumentException()
    {
        var visit = new PrenatalVisit
        {
            PregnancyId = 0,
            VisitDate = new DateOnly(2026, 1, 15)
        };

        Assert.Throws<ArgumentException>(
            () => PrenatalVisitValidator.Validate(visit));
    }

    [Fact]
    public void Validate_DefaultVisitDate_ThrowsArgumentException()
    {
        var visit = new PrenatalVisit
        {
            PregnancyId = 1,
            VisitDate = default
        };

        Assert.Throws<ArgumentException>(
            () => PrenatalVisitValidator.Validate(visit));
    }

    [Fact]
    public void Validate_NotesExceeding2000Characters_ThrowsArgumentException()
    {
        var visit = new PrenatalVisit
        {
            PregnancyId = 1,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = new string('A', 2001)
        };

        Assert.Throws<ArgumentException>(
            () => PrenatalVisitValidator.Validate(visit));
    }

    [Fact]
    public void Validate_NotesExactly2000Characters_DoesNotThrow()
    {
        var visit = new PrenatalVisit
        {
            PregnancyId = 1,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = new string('A', 2000)
        };

        var exception = Record.Exception(
            () => PrenatalVisitValidator.Validate(visit));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_NullNotes_DoesNotThrow()
    {
        var visit = new PrenatalVisit
        {
            PregnancyId = 1,
            VisitDate = new DateOnly(2026, 1, 15),
            Notes = null
        };

        var exception = Record.Exception(
            () => PrenatalVisitValidator.Validate(visit));

        Assert.Null(exception);
    }
}