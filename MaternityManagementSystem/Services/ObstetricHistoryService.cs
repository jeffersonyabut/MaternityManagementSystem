
using System;
using System.Collections.Generic;
using MaternityManagementSystem.Models;
using MaternityManagementSystem.Models.Reports;

namespace MaternityManagementSystem.Services;

public class ObstetricHistoryService
{
    public ObstetricHistorySummary Calculate(
        IReadOnlyCollection<Pregnancy> pregnancies)
    {
        ArgumentNullException.ThrowIfNull(pregnancies);

        var summary = new ObstetricHistorySummary
        {
            LivingChildrenDisplay = "Not recorded"
        };

        int totalLivingChildren = 0;
        bool hasIncompleteLivingChildrenData = false;

        foreach (var pregnancy in pregnancies)
        {
            if (pregnancy is null)
            {
                continue;
            }

            summary.Gravida++;

            // Current pregnancies count toward Gravida,
            // but are not classified as completed outcomes.
            if (string.Equals(
                pregnancy.Status,
                "Current",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(
                pregnancy.Status,
                "Completed",
                StringComparison.OrdinalIgnoreCase))
            {
                summary.UnclassifiedPregnancies++;
                hasIncompleteLivingChildrenData = true;
                continue;
            }

            if (!pregnancy.GestationalAgeAtEndWeeks.HasValue)
            {
                summary.UnclassifiedPregnancies++;
            }
            else
            {
                int weeks = pregnancy.GestationalAgeAtEndWeeks.Value;

                if (weeks is < 0 or > 45)
                {
                    summary.UnclassifiedPregnancies++;
                }
                else if (weeks < 20)
                {
                    bool isPregnancyLoss =
                        string.Equals(
                            pregnancy.PregnancyOutcome,
                            "Miscarriage",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            pregnancy.PregnancyOutcome,
                            "Ectopic Pregnancy",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            pregnancy.PregnancyOutcome,
                            "Molar Pregnancy",
                            StringComparison.OrdinalIgnoreCase);

                    if (isPregnancyLoss)
                    {
                        summary.Abortion++;
                    }
                    else
                    {
                        summary.UnclassifiedPregnancies++;
                    }
                }
                else if (weeks < 37)
                {
                    summary.Preterm++;
                }
                else
                {
                    summary.Term++;
                }
            }

            // LivingChildrenCount represents the number of
            // children from this particular pregnancy who
            // are currently alive.
            if (!pregnancy.LivingChildrenCount.HasValue ||
                pregnancy.LivingChildrenCount.Value < 0)
            {
                hasIncompleteLivingChildrenData = true;
            }
            else
            {
                totalLivingChildren +=
                    pregnancy.LivingChildrenCount.Value;
            }
        }

        summary.LivingChildrenDisplay =
            hasIncompleteLivingChildrenData
                ? "Incomplete data"
                : totalLivingChildren.ToString();

        return summary;
    }
}
