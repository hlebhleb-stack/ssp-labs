using Microsoft.ML;

// Общий контекст ML.NET задаёт начальное состояние генератора для разбиения выборок.
var ml = new MLContext(seed: 1);

// Аргумент командной строки выбирает пример или индивидуальное задание.
switch (args.FirstOrDefault())
{
    case "1":
        Task1Sentiment.Run(ml);
        break;
    case "2":
        Task2Regression.Run(ml);
        break;
    case "4":
        Task4Spam.Run(ml);
        break;
    default:
        Console.WriteLine("dotnet run -- 1   Пример из методички: тональность текста");
        Console.WriteLine("dotnet run -- 2   Пример из методички: прогноз балла");
        Console.WriteLine("dotnet run -- 4   Вариант 4: спам или не спам?");
        break;
}
