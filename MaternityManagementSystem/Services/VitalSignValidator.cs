
using System;
using MaternityManagementSystem.Models;

namespace MaternityManagementSystem.Services;

public static class VitalSignValidator
{
    public static void Validate(VitalSign vitalSign)
    {
        ArgumentNullException.ThrowIfNull(vitalSign);

        if (vitalSign.PrenatalVisitId <= 0)
        {
            throw new ArgumentException(
                "A valid prenatal visit must be selected.");
        }

        if (vitalSign.SystolicBloodPressure.HasValue !=
            vitalSign.DiastolicBloodPressure.HasValue)
        {
            throw new ArgumentException(
                "Both systolic and diastolic blood pressure must be provided together.");
        }

        if (!vitalSign.SystolicBloodPressure.HasValue &&
            !vitalSign.HeartRate.HasValue &&
            !vitalSign.RespiratoryRate.HasValue &&
            !vitalSign.TemperatureCelsius.HasValue &&
            !vitalSign.OxygenSaturation.HasValue &&
            !vitalSign.WeightKg.HasValue &&
            !vitalSign.FetalHeartRate.HasValue)
        {
            throw new ArgumentException(
                "At least one vital sign measurement is required.");
        }

        if (vitalSign.SystolicBloodPressure <= 0 ||
            vitalSign.DiastolicBloodPressure <= 0)
        {
            throw new ArgumentException(
                "Blood pressure values must be greater than zero.");
        }

        if (vitalSign.SystolicBloodPressure.HasValue &&
            vitalSign.DiastolicBloodPressure.HasValue &&
            vitalSign.SystolicBloodPressure.Value <=
            vitalSign.DiastolicBloodPressure.Value)
        {
            throw new ArgumentException(
                "Systolic blood pressure must be greater than diastolic blood pressure.");
        }

        if (vitalSign.HeartRate <= 0)
        {
            throw new ArgumentException(
                "Heart rate must be greater than zero.");
        }

        if (vitalSign.RespiratoryRate <= 0)
        {
            throw new ArgumentException(
                "Respiratory rate must be greater than zero.");
        }

        if (vitalSign.TemperatureCelsius <= 0)
        {
            throw new ArgumentException(
                "Temperature must be greater than zero.");
        }

        if (vitalSign.OxygenSaturation is <= 0 or > 100)
        {
            throw new ArgumentException(
                "Oxygen saturation must be greater than 0 and cannot exceed 100.");
        }

        if (vitalSign.WeightKg <= 0)
        {
            throw new ArgumentException(
                "Weight must be greater than zero.");
        }

        if (vitalSign.FetalHeartRate <= 0)
        {
            throw new ArgumentException(
                "Fetal heart rate must be greater than zero.");
        }

        if (vitalSign.RecordedAtUtc == default)
        {
            throw new ArgumentException(
                "A valid measurement date and time is required.");
        }

        if (!string.IsNullOrWhiteSpace(vitalSign.Notes) &&
            vitalSign.Notes.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Vital signs notes cannot exceed 2000 characters.");
        }
    }
}
