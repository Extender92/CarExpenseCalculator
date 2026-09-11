using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
namespace CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
internal static class SavedListingDetailsJson
{
    public static string? Serialize(ListingDetails? value) => value is null ? null
        : HouseholdJson.Serialize(DraftListingDetails.FromCore(value));
    public static ListingDetails? Deserialize(string? json) => json is null ? null
        : HouseholdJson.Decode(() => HouseholdJson.Deserialize<DraftListingDetails>(json).ToCore());
}
