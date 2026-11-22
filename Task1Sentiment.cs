using Microsoft.ML;

internal static class Task1Sentiment
{
    public static void Run(MLContext ml)
    {
        var samples = new[]
        {
            new SentimentData { SentimentText = "This is great!", Label = true },
            new SentimentData { SentimentText = "This is terrible.", Label = false },
            new SentimentData { SentimentText = "I love it!", Label = true },
            new SentimentData { SentimentText = "I hate it.", Label = false }
        };

        var data = ml.Data.LoadFromEnumerable(samples);
        var pipeline = ml.Transforms.Text.FeaturizeText("Features", nameof(SentimentData.SentimentText))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression());

        var model = pipeline.Fit(data);
        var predictor = ml.Model.CreatePredictionEngine<SentimentData, BinaryPrediction>(model);
        var sample = new SentimentData { SentimentText = "This is wonderful!" };
        var prediction = predictor.Predict(sample);

        Console.WriteLine($"Текст: {sample.SentimentText}");
        Console.WriteLine($"Предсказание: {(prediction.Prediction ? "Позитивный" : "Негативный")}");
        Console.WriteLine($"Вероятность позитивного класса: {prediction.Probability:P2}");
    }

    private sealed class SentimentData
    {
        public string SentimentText { get; set; } = string.Empty;
        public bool Label { get; set; }
    }
}
