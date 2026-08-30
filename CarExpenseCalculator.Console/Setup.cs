using CarExpenseCalculator.Core.Models;

namespace CarExpenseCalculator.Console
{
    internal class Setup
    {
        internal CarInstance SetupCarInstance()
        {
            CarInstance carInstance = new CarInstance();
            List<Fuel> fuelTypes = SetupFuelTypes();
            carInstance.Car = SetupCar(fuelTypes);
            carInstance.Loan = SetupLoan();

            return carInstance;
        }

        private Loan SetupLoan()
        {
            Loan loan = new()
            {
                Amount = 0,
                InterestRate = 0,
                Months = 0
            };

            System.Console.WriteLine("How much is the loan?");
            loan.Amount = GetIntegerFromReadLine();

            if (loan.Amount > 0)
            {
                System.Console.WriteLine("How much is the interest rate?");
                loan.InterestRate = GetDoubleValueFromUser();

                System.Console.WriteLine("How many months is the loan payments?");
                loan.Months = GetIntegerFromReadLine();
            }

            return loan;
        }

        public List<Fuel> SetupFuelTypes()
        {
            List<Fuel> fuelTypes = new List<Fuel>();

            // Petrol (Gasoline) – Swedish price ~15 SEK per liter
            fuelTypes.Add(new Fuel
            {
                Name = "petrol",
                PricePerLiter = 15.00, // Price in SEK
                Eco = false
            });

            // Diesel – Swedish price ~16 SEK per liter
            fuelTypes.Add(new Fuel
            {
                Name = "diesel",
                PricePerLiter = 16.00, // Price in SEK
                Eco = false
            });

            // Electric (EV) – Charging cost depends on provider and region, average around 2.00 SEK per kWh
            fuelTypes.Add(new Fuel
            {
                Name = "electric",
                PricePerLiter = 0, // It's charged by kWh, not liters
                Eco = true
            });

            // Compressed Natural Gas (CNG) – Swedish price ~12 SEK per kg (equivalent to liter)
            fuelTypes.Add(new Fuel
            {
                Name = "CNG",
                PricePerLiter = 12.00, // Price in SEK (approximate equivalent to liter of gasoline)
                Eco = true
            });

            // Liquefied Petroleum Gas (LPG) – Swedish price ~9 SEK per liter
            fuelTypes.Add(new Fuel
            {
                Name = "LPG",
                PricePerLiter = 9.00, // Price in SEK
                Eco = true
            });

            // Ethanol (E85) – Swedish price ~13 SEK per liter
            fuelTypes.Add(new Fuel
            {
                Name = "ethanol",
                PricePerLiter = 13.00, // Price in SEK
                Eco = true
            });

            // Hydrogen (Fuel Cell) – Price for hydrogen in Sweden can vary but is around ~20 SEK per kg (approximately equivalent to 1.2 liters of gasoline).
            fuelTypes.Add(new Fuel
            {
                Name = "hydrogen",
                PricePerLiter = 20.00, // Price in SEK per kg (approximate equivalent to liter)
                Eco = true
            });

            return fuelTypes;
        }

        private Car SetupCar(List<Fuel> fuelTypes)
        {
            Car car = new Car();

            System.Console.WriteLine("Name of the Car");
            car.Name = GetValueFromReadLine();

            System.Console.WriteLine("How much in CarTaxes per year?");
            car.Taxes = GetDoubleValueFromUser();

            System.Console.WriteLine("Price of the car?");
            car.Price = GetDoubleValueFromUser();

            System.Console.WriteLine("Mileage of the car?");
            car.Mileage = GetDoubleValueFromUser();

            System.Console.WriteLine("Kilometers per Liter?");
            car.KilometerPerLiter = GetDoubleValueFromUser();

            List<string> yesStrings = ["y", "yes"];
            System.Console.WriteLine("Does the car have two fuel types/Hybrid?");
            Boolean hybrid = GetMatchFromReadLine(yesStrings);

            PrintAllFuelTypes(fuelTypes);
            System.Console.WriteLine("Fuel Type?");
            int fuelTypeIndex = GetIntegerFromReadLine() - 1;
            car.FuelType = fuelTypes[fuelTypeIndex];

            if (hybrid)
            {
                System.Console.WriteLine("Secondary Fuel type?");
                int secondaryFuelTypeIndex = GetIntegerFromReadLine();
                car.SecondaryFuelType = fuelTypes[secondaryFuelTypeIndex];
            }

            return car;
        }

        private static int GetIntegerFromReadLine()
        {
            while (true)
            {
                if (int.TryParse(GetValueFromReadLine(), out var result))
                {
                    return (result);
                }
            }
        }

        private static string GetValueFromReadLine()
        {
            while (true) {
                var input = System.Console.ReadLine();
                if (input != null && input.Length > 0)
                {
                    return input;
                }
            }
        }

        internal Boolean GetMatchFromReadLine(List<string> matches)
        {
            string input = GetValueFromReadLine();
            return matches.Any(x => x.ToLower().Equals(input.ToLower()));
        }

        private static double GetDoubleValueFromUser()
        {
            double result;
            while (true)
            {
                System.Console.Write("Please enter a number: ");
                if (double.TryParse(System.Console.ReadLine(), out result))
                {
                    return result;
                }
                else
                {
                    System.Console.WriteLine("Error: Input was not a valid number. Try again.");
                }
            }
        }

        internal void PrintAllFuelTypes(List<Fuel> fuelTypes)
        {
            for (int i = 0; i < fuelTypes.Count; i++)
            {
                string fuelType = $"{i + 1}: {fuelTypes[i].Name} Price: {fuelTypes[i].PricePerLiter} Eco: {fuelTypes[i].Eco}";
                System.Console.WriteLine(fuelType);
            }
        }
    }
}
