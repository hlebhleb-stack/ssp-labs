using Microsoft.ML.Data;

public sealed class BinaryPrediction
{
    // ML.NET возвращает булев класс в колонке PredictedLabel.
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }
    // Probability — оценка вероятности положительного класса.
    public float Probability { get; set; }
}
