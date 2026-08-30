namespace CarExpenseCalculator.Console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Setup setup = new Setup();
            DisplayInstances displayInstances = new DisplayInstances();
            
            List<CarInstance> carInstances = new List<CarInstance>();

            while (true)
            {
                CarInstance carInstance = setup.SetupCarInstance();
                if (carInstance == null || carInstance.Loan == null || carInstance.Car == null)
                {
                    continue;
                }

                List<string> yesStrings = ["y", "yes"];
                string carString = $"{carInstance.Car.Name} {carInstance.Car.Price}kr\nLoan:{carInstance.Loan.Amount}kr {carInstance.Loan.InterestRate}% {carInstance.Loan.Months} months";

                System.Console.WriteLine("Do you want to save this car?");
                System.Console.WriteLine(carString);
                Boolean save = setup.GetMatchFromReadLine(yesStrings);

                if (save)
                {
                    carInstances.Add(carInstance);
                }

                System.Console.WriteLine("Do you want to add another car?");
                Boolean newCar = setup.GetMatchFromReadLine(yesStrings);

                if (!newCar)
                {
                    break;
                }
            }
            displayInstances.PrintAllCarInstances(carInstances);
        }
    }
}
