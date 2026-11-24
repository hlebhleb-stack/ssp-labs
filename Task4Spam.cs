using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

internal static class Task4Spam
{
    public static void Run(MLContext ml)
    {
        // Каждый столбец CSV считывается по индексу из атрибутов EmailData.
        var data = ml.Data.LoadFromTextFile<EmailData>(
            Path.Combine(AppContext.BaseDirectory, "spam.csv"), hasHeader: true, separatorChar: ',');

        // Тема и тело превращаются в два числовых вектора.
        // К ним добавляются два признака 0/1. IsSpam остаётся только целевой меткой.
        var textFeatures = ml.Transforms.CustomMapping<EmailData, IntentFeatures>(MapIntentFeatures, contractName: null)
            .Append(ml.Transforms.Text.FeaturizeText("SubjectFeatures", nameof(EmailData.Subject)))
            .Append(ml.Transforms.Text.FeaturizeText("MessageFeatures", nameof(EmailData.Message)));

        var trainer = new SdcaLogisticRegressionBinaryTrainer.Options
        {
            LabelColumnName = nameof(EmailData.IsSpam),
            MaximumNumberOfIterations = 300,
            L1Regularization = 0,
            L2Regularization = 0.001f,
            NumberOfThreads = 1
        };

        var pipeline = textFeatures
            .Append(ml.Transforms.Concatenate("Features",
                "SubjectFeatures", "MessageFeatures", nameof(IntentFeatures.Intent), nameof(EmailData.SenderKnown), nameof(EmailData.HasAttachments)))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(trainer));

        // ApplyWordEmbedding сопоставляет токены с векторами из учебного русского словаря.
        // Сохраняем базовые признаки, чтобы проверить вклад добавленных векторов отдельно.
        var vectorPath = Path.Combine(AppContext.BaseDirectory, "ru-word-vectors.txt");
        var embeddingPipeline = textFeatures
            // ASCII-маркер сохраняется при нормализации и даёт нулевой вектор для незнакомых слов.
            .Append(ml.Transforms.CustomMapping<EmailData, EmbeddingText>((email, output) =>
            {
                output.EmbeddingSubject = "neutralmarker " + email.Subject;
                output.EmbeddingMessage = "neutralmarker " + email.Message;
            }, contractName: null))
            .Append(ml.Transforms.Text.NormalizeText("NormalizedSubject", nameof(EmbeddingText.EmbeddingSubject)))
            .Append(ml.Transforms.Text.NormalizeText("NormalizedMessage", nameof(EmbeddingText.EmbeddingMessage)))
            .Append(ml.Transforms.Text.TokenizeIntoWords("SubjectTokens", "NormalizedSubject"))
            .Append(ml.Transforms.Text.TokenizeIntoWords("MessageTokens", "NormalizedMessage"))
            .Append(ml.Transforms.Text.ApplyWordEmbedding(outputColumnName: "SubjectEmbedding", inputColumnName: "SubjectTokens",
                customModelFile: vectorPath))
            .Append(ml.Transforms.Text.ApplyWordEmbedding(outputColumnName: "MessageEmbedding", inputColumnName: "MessageTokens",
                customModelFile: vectorPath))
            .Append(ml.Transforms.Concatenate("Features",
                "SubjectFeatures", "MessageFeatures", nameof(IntentFeatures.Intent), "SubjectEmbedding", "MessageEmbedding",
                nameof(EmailData.SenderKnown), nameof(EmailData.HasAttachments)))
            .Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(trainer));

        // Сравниваем модели на одних и тех же тестовых письмах, не передавая их в Fit.
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2);
        var testSet = Materialize(ml, split.TestSet);
        var trainSet = Materialize(ml, split.TrainSet);
        Evaluate(ml, pipeline, trainSet, testSet, "Базовая модель");
        Evaluate(ml, embeddingPipeline, trainSet, testSet, "С ApplyWordEmbedding");

        // Итоговую модель для демонстрации обучаем на всех размеченных письмах.
        var model = pipeline.Fit(data);
        var predictor = ml.Model.CreatePredictionEngine<EmailData, BinaryPrediction>(model);
        var email = new EmailData
        {
            Subject = "Срочно заберите денежный приз",
            Message = "Вы выиграли подарок. Перейдите по ссылке и подтвердите данные карты",
            SenderKnown = 0,
            HasAttachments = 0
        };
        var prediction = predictor.Predict(email);

        Console.WriteLine($"Тема: {email.Subject}");
        Console.WriteLine($"Текст: {email.Message}");
        Console.WriteLine($"Известный отправитель: {email.SenderKnown}. Вложения: {email.HasAttachments}.");
        Console.WriteLine($"Предсказание: {(prediction.Prediction ? "Спам" : "Не спам")}");
        Console.WriteLine($"Вероятность спама: {prediction.Probability:P2}");
    }

    private static IDataView Materialize(MLContext ml, IDataView data)
    {
        var emails = ml.Data.CreateEnumerable<EmailData>(data, reuseRowObject: false).ToArray();
        return ml.Data.LoadFromEnumerable(emails);
    }

    private static void Evaluate(MLContext ml, IEstimator<ITransformer> pipeline,
        IDataView trainSet, IDataView testSet, string name)
    {
        var model = pipeline.Fit(trainSet);
        var predictions = model.Transform(testSet);
        var metrics = ml.BinaryClassification.Evaluate(predictions, labelColumnName: nameof(EmailData.IsSpam));

        // Диагональ матрицы ошибок содержит правильные ответы по обоим классам.
        var counts = metrics.ConfusionMatrix.Counts;
        var correct = counts[0][0] + counts[1][1];
        Console.WriteLine($"{name}: {correct}/{counts.Sum(row => row.Sum())} ({metrics.Accuracy:P2})");

        // Сравниваем вероятности спама с фактическими метками 0/1.
        var rows = ml.Data.CreateEnumerable<SpamEvaluation>(predictions, reuseRowObject: false).ToArray();

        var spamCount = rows.Count(row => row.IsSpam);
        if (spamCount == 0 || spamCount == rows.Length)
        {
            Console.WriteLine("R² по вероятностям спама не определён: в тестовой выборке нет разброса меток.");
            return;
        }

        var meanLabel = (double)spamCount / rows.Length;
        var residualSum = rows.Sum(row => Math.Pow((row.IsSpam ? 1.0 : 0.0) - row.Probability, 2));
        var totalSum = rows.Sum(row => Math.Pow((row.IsSpam ? 1.0 : 0.0) - meanLabel, 2));
        Console.WriteLine($"R² по вероятностям спама: {1 - residualSum / totalSum:F2}");
    }

    private static readonly Regex PromotionalLanguage = new(
        @"\b(?:выигр\w*|приз\w*|розыгрыш\w*|лотере\w*|подар\w*|скид\w*|купон\w*|промокод\w*|бонус\w*|бесплат\w*|займ\w*|кредит\w*|заработ\w*|удво\w*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveRequest = new(
        @"\b(?:введите|отправьте|подтвердите|оплатите|переведите|внесите)\b.{0,55}\b(?:карт\w*|парол\w*|код\w*|комисси\w*|сбор\w*|взнос\w*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void MapIntentFeatures(EmailData email, IntentFeatures output)
    {
        var text = email.Subject + " " + email.Message;
        // Масштабируем два бинарных признака перед объединением с текстовыми векторами.
        output.Intent = [
            PromotionalLanguage.IsMatch(text) ? 5f : 0f,
            SensitiveRequest.IsMatch(text) ? 5f : 0f
        ];
    }

    private sealed class IntentFeatures
    {
        [VectorType(2)] public float[] Intent { get; set; } = Array.Empty<float>();
    }

    private sealed class SpamEvaluation
    {
        public bool IsSpam { get; set; }
        public float Probability { get; set; }
    }

    private sealed class EmbeddingText
    {
        public string EmbeddingSubject { get; set; } = string.Empty;
        public string EmbeddingMessage { get; set; } = string.Empty;
    }

    private sealed class EmailData
    {
        [LoadColumn(0)] public string Subject { get; set; } = string.Empty;
        [LoadColumn(1)] public string Message { get; set; } = string.Empty;
        [LoadColumn(2)] public float SenderKnown { get; set; }
        [LoadColumn(3)] public float HasAttachments { get; set; }
        [LoadColumn(4)] public bool IsSpam { get; set; }
    }
}
