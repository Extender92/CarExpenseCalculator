namespace CarExpenseCalculator.Core.Listings;

public enum ListingAnalysisStatus
{
    Complete,
    Partial,
    Unavailable,
}

public enum FieldOrigin
{
    Listing,
    User,
    Registry,
}

public enum ExtractionMethod
{
    Ai,
    Manual,
}

public enum VerificationStatus
{
    Unverified,
    UserConfirmed,
    RegistryVerified,
}

public enum SellerType
{
    Private,
    Dealer,
}

public enum FuelType
{
    Petrol,
    Diesel,
    Electricity,
    Ethanol,
    Biogas,
    NaturalGas,
    LiquefiedPetroleumGas,
    Hydrogen,
    Other,
}

public enum Transmission
{
    Manual,
    Automatic,
}

public enum Drivetrain
{
    FrontWheelDrive,
    RearWheelDrive,
    AllWheelDrive,
}

public enum BodyType
{
    Sedan,
    Hatchback,
    Wagon,
    Suv,
    Coupe,
    Convertible,
    Minivan,
    Pickup,
    Van,
    Other,
}

public enum ListingFieldCode
{
    RegistrationNumber,
    Make,
    Model,
    Variant,
    ModelYear,
    Vin,
    PriceSek,
    OdometerKilometres,
    SellerType,
    Location,
    PublishedDate,
    UpdatedDate,
    ImageCount,
    FuelTypes,
    Transmission,
    Drivetrain,
    BodyType,
    Colour,
    Horsepower,
    EngineDisplacementCubicCentimetres,
    EnergyConsumptions,
    AnnualVehicleTaxSek,
    OwnerCount,
    FirstRegistrationDate,
    LastInspectionDate,
    NextInspectionDate,
    TowBar,
    Equipment,
    SellerClaims,
    ConditionNotes,
}
