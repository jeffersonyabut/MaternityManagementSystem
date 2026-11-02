
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
            Gravida = pregnancies.Count,
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

            // Current pregnancies count toward Gravida,
            // but are not yet classified as completed outcomes.
            if (pregnancy.Status == "Current")
            {
                continue;
            }

            if (pregnancy.Status != "Completed")
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
                int days = pregnancy.GestationalAgeAtEndDays ?? 0;

                if (weeks < 20)
                {
                    bool isPregnancyLoss =
                        pregnancy.PregnancyOutcome == "Miscarriage" ||
                        pregnancy.PregnancyOutcome == "Ectopic Pregnancy" ||
                        pregnancy.PregnancyOutcome == "Molar Pregnancy";

                    if (isPregnancyLoss)
                    {
                        summary.Abortion++;
                    }
                    else
                    {
                        summary.UnclassifiedPregnancies++;
                    }
                }
                else if (weeks >= 37)
                {
                    summary.Term++;
                }
                else if (weeks >= 20 && weeks < 37)
                {
                    summary.Preterm++;
                }
                else
                {
                    summary.UnclassifiedPregnancies++;
                }
            }

            if (!pregnancy.LivingChildrenCount.HasValue)
            {
                hasIncompleteLivingChildrenData = true;
            }
            else
            {
                totalLivingChildren += pregnancy.LivingChildrenCount.Value;
            }
        }

        summary.LivingChildrenDisplay =
            hasIncompleteLivingChildrenData
                ? "Incomplete data"
                : totalLivingChildren.ToString();

        return summary;
    }
}
