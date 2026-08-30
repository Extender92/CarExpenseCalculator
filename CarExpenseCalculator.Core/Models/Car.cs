namespace CarExpenseCalculator.Core.Models
{
    public class Car
    {
        public string Name { get; set; }
        public double Price { get; set; }
        public double Mileage { get; set; }
        public double KilometerPerLiter { get; set; }
        public double Taxes { get; set; }
        public Fuel FuelType { get; set; }
        public Fuel SecondaryFuelType { get; set; }
    }
}
