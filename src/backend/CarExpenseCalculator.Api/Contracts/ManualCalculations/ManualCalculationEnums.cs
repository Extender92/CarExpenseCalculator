using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarExpenseCalculator.Api.Contracts.ManualCalculations;

[JsonConverter(typeof(StrictStringEnumConverter<EnergyUnit>))]
public enum EnergyUnit
{
    Litre,
    KilowattHour,
    Kilogram,
}

[JsonConverter(typeof(StrictStringEnumConverter<RecurringCostCadence>))]
public enum RecurringCostCadence
{
    Monthly,
    Annual,
}

[JsonConverter(typeof(StrictStringEnumConverter<MissingCategory>))]
public enum MissingCategory
{
    VehicleTax,
    Insurance,
    MaintenanceAndRepairs,
    ResidualValue,
}

public sealed class StrictStringEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where TEnum : struct, Enum;
