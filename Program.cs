namespace SSP_Lab2;

class Program
{
    static void Main(string[] args)
    {
        string choice;
        if (args.Length > 0)
        {
            choice = args[0];
        }
        else
        {
            Console.Write("Номер задания (1-7): ");
            choice = Console.ReadLine() ?? "";
        }

        switch (choice)
        {
            case "1": Task1.Run(); break;
            case "2": Task2.Run(); break;
            case "3": Task3.Run(); break;
            case "4": Task4.Run(); break;
            case "5": Task5.Run(); break;
            case "6": Task6.Run(); break;
            case "7": Task7.Run(); break;
            default: Console.WriteLine("Нет такого задания"); break;
        }
    }
}
