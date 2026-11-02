
using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services.Reports;

public class IndividualPatientRecordReportService
{
    private readonly PatientService _patientService;
    private readonly ObstetricHistoryService _obstetricHistoryService = new();

    public IndividualPatientRecordReportService(
        PatientService patientService)
    {
        ArgumentNullException.ThrowIfNull(patientService);

        _patientService = patientService;
    }

    public async Task<IndividualPatientRecordReport>
        GenerateIndividualPatientRecordAsync(
            int patientId,
            CancellationToken cancellationToken = default)
    {
        if (patientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patientId));
        }

        IndividualPatientRecordReport? report =
            await _patientService.GetIndividualPatientRecordReportAsync(
                patientId,
                cancellationToken);

        if (report is null)
        {
            throw new InvalidOperationException(
                "The patient record could not be found.");
        }
        report.ObstetricHistory =
        _obstetricHistoryService.Calculate(report.Pregnancies);

        return report;
    }
}
