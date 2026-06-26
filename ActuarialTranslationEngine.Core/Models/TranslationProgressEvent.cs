namespace ActuarialTranslationEngine.Core.Models;

public class TranslationProgressEvent
{
    public string Message { get; set; } = string.Empty;
    public int CurrentPartition { get; set; }
    public int TotalPartitions { get; set; }
    public double PercentComplete => TotalPartitions == 0 ? 0 : ((double)CurrentPartition / TotalPartitions) * 100;
}
