namespace MobilityLab.Experiment;

sealed class ExperimentHistory
{
    public List<MonthSample> Months { get; } = [];

    public void Record(MonthSample sample) => Months.Add(sample);
}
