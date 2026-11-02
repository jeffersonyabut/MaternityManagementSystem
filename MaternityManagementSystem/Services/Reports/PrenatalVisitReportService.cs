using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services.Reports;

public class PrenatalVisitReportService
{
    private readonly PrenatalVisitService _prenatalVisitService;
    private readonly LaboratoryResultService _laboratoryResultService;

    public PrenatalVisitReportService(
        PrenatalVisitService prenatalVisitService,
        LaboratoryResultService laboratoryResultService)
    {
        ArgumentNullException.ThrowIfNull(prenatalVisitService);
        ArgumentNullException.ThrowIfNull(laboratoryResultService);

        _prenatalVisitService = prenatalVisitService;
        _laboratoryResultService = laboratoryResultService;
    }

    public async Task<PrenatalVisitReport> GeneratePrenatalVisitReportAsync(
        int visitId,
        CancellationToken cancellationToken = default)
    {
        if (visitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitId));
        }

        var visit = await _prenatalVisitService.GetForReportByIdAsync(
            visitId,
            cancellationToken);

        if (visit is null)
        {
            throw new InvalidOperationException(
                "The selected prenatal visit could not be found.");
        }

        if (visit.Pregnancy?.Patient is null)
        {
            throw new InvalidOperationException(
                "The patient or pregnancy associated with this visit could not be found.");
        }

        var laboratoryResults =
            await _laboratoryResultService.GetByPrenatalVisitIdAsync(
                visitId,
                cancellationToken);

        return new PrenatalVisitReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Patient = visit.Pregnancy.Patient,
            Pregnancy = visit.Pregnancy,
            PrenatalVisit = visit,
            VitalSigns = visit.VitalSigns.ToList(),
            LaboratoryResults = laboratoryResults
        };
    }
}
