namespace CarExpenseCalculator.Console
{
    internal class DisplayInstances
    {
        private double CalculateTotalLoanAmount(Loan loan)
        {
            // Ensure the loan object has valid data
            double principal = loan.Amount;  // Loan amount
            int months = loan.Months;         // Loan term in months
            double annualInterestRate = loan.InterestRate;  // Annual interest rate in decimal form

            // Convert annual interest rate to monthly interest rate
            double monthlyInterestRate = annualInterestRate / 12;

            // Calculate the monthly payment using the amortization formula
            double monthlyPayment = 0;
            if (monthlyInterestRate > 0)
            {
                monthlyPayment = (principal * monthlyInterestRate * Math.Pow(1 + monthlyInterestRate, months)) /
                                 (Math.Pow(1 + monthlyInterestRate, months) - 1);
            }
            else
            {
                // If the interest rate is 0%, just divide the principal by the number of months
                monthlyPayment = principal / months;
            }

            // Total amount paid over the life of the loan
            double totalAmount = monthlyPayment * months;

            return totalAmount;
        }

        internal void PrintAllCarInstances(List<CarInstance> carInstances)
        {
            System.Console.Clear();
            int calculatedYears = 10;
            int averageMilagePerYear = 10000; // in Kilometers

            foreach (CarInstance carInstance in carInstances)
            {
                string car = $"{carInstance.Car.Name} {carInstance.Car.Price} {carInstance.Car.Mileage} {carInstance.Loan.Amount} {carInstance.Loan.InterestRate}";
                System.Console.WriteLine(car);

                // Calculate costs for each year
                for (int year = 0; year < calculatedYears; year++)
                {
                    // Calculate total mileage for the current year
                    int mileage = averageMilagePerYear * (year + 1); // Year 0 is no mileage, so we start from year 1 with mileage for first year

                    // Calculate fuel cost based on mileage and fuel efficiency
                    double fuelCost = carInstance.Car.FuelType.PricePerLiter * (mileage / carInstance.Car.KilometerPerLiter);

                    // Loan cost: We assume it's just the total loan amount added each year (no interest or monthly payments here)
                    double loanCost = CalculateTotalLoanAmount(carInstance.Loan); // Use the total loan amount directly

                    // Taxes
                    double taxesCost = carInstance.Car.Taxes * (year + 1);

                    // Total cost: Car price + taxes + fuel cost + loan amount (for each year)
                    double totalCost = fuelCost + loanCost + taxesCost + (carInstance.Car.Price - carInstance.Loan.Amount);

                    // Output the cost breakdown for each year
                    System.Console.WriteLine($"Year: {year + 1}, km Traveled: {mileage}, Taxes: {taxesCost}, Fuel cost: {fuelCost} kr, Loan cost: {loanCost} kr, Total Cost: {totalCost} kr");
                }
                System.Console.WriteLine();
            }
        }
    }
}
