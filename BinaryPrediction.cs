using Microsoft.ML.Data;

public sealed class BinaryPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }
    public float Probability { get; set; }
}
