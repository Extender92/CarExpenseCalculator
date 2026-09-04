using System.Text.Json.Serialization;
using CarExpenseCalculator.Api.Contracts.ManualCalculations;

namespace CarExpenseCalculator.Api.Contracts.ListingAnalyses;

[JsonConverter(typeof(StrictStringEnumConverter<ListingAnalysisStatus>))]
public enum ListingAnalysisStatus
{
    Complete,
    Partial,
    Unavailable,
}

[JsonConverter(typeof(StrictStringEnumConverter<FieldOrigin>))]
public enum FieldOrigin
{
    Listing,
    User,
    Registry,
}

[JsonConverter(typeof(StrictStringEnumConverter<ExtractionMethod>))]
public enum ExtractionMethod
{
    Ai,
    Manual,
}

[JsonConverter(typeof(StrictStringEnumConverter<VerificationStatus>))]
public enum VerificationStatus
{
    Unverified,
    UserConfirmed,
    RegistryVerified,
}

[JsonConverter(typeof(StrictStringEnumConverter<SellerType>))]
public enum SellerType
{
    Private,
    Dealer,
}

[JsonConverter(typeof(StrictStringEnumConverter<FuelType>))]
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

[JsonConverter(typeof(StrictStringEnumConverter<Transmission>))]
public enum Transmission
{
    Manual,
    Automatic,
}

[JsonConverter(typeof(StrictStringEnumConverter<Drivetrain>))]
public enum Drivetrain
{
    FrontWheelDrive,
    RearWheelDrive,
    AllWheelDrive,
}

[JsonConverter(typeof(StrictStringEnumConverter<BodyType>))]
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

[JsonConverter(typeof(StrictStringEnumConverter<ListingFieldCode>))]
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
    Locality,
    County,
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
