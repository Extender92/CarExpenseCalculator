using CarExpenseCalculator.Api.Contracts.Households;
using CarExpenseCalculator.Api.Mapping;
using Microsoft.AspNetCore.Mvc;
using C = CarExpenseCalculator.Core.Households;

namespace CarExpenseCalculator.Api.Controllers;

[ApiController]
[Route("api/household-calculations")]
public sealed class HouseholdCalculationsController(C.HouseholdCostCalculator calculator) : ControllerBase
{
    [HttpPost("preview")]
    [Consumes("application/json")]
    [ProducesResponseType<HouseholdPreviewResponse>(200)]
    [ProducesResponseType(typeof(HouseholdValidationProblemDetails), 400, "application/problem+json")]
    [ProducesResponseType(typeof(HouseholdProblemDetails), 413, "application/problem+json")]
    public ActionResult<HouseholdPreviewResponse> Preview(HouseholdPreviewRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 120)
            throw HouseholdInputMapper.Error("requestId", "invalidRequestId", "Request ID must contain 1-120 characters.");
        if (request.Vehicles.Count > C.HouseholdInputValidator.MaximumCandidates)
            throw HouseholdInputMapper.Error("vehicles", "tooManyItems", "At most 100 candidates may be previewed together.");
        var registrations = new HashSet<string>(StringComparer.Ordinal);
        var identities = new List<string?>();
        var inputs = new List<C.VehicleCostInput>();
        var reviews = new List<IReadOnlyList<LegacyReviewResponse>>();
        for (var i = 0; i < request.Vehicles.Count; i++)
        {
            var x = request.Vehicles[i];
            var path = $"vehicles[{i}]";
            if (x is null) throw HouseholdInputMapper.Error(path, "missingItem", "A candidate cannot be null.");
            if (x.RegistrationNumber is null && request.Vehicles.Count > 1)
                throw HouseholdInputMapper.Error(path + ".registrationNumber", "registrationRequired", "Multi-car previews require registered identities.");
            var registration = x.RegistrationNumber is null ? null
                : HouseholdStoreMapper.Registration(x.RegistrationNumber, path + ".registrationNumber").Value;
            if (registration is not null && !registrations.Add(registration))
                throw HouseholdInputMapper.Error(path + ".registrationNumber", "duplicateRegistration", "Each registration may appear only once.");
            identities.Add(registration);
            inputs.Add(HouseholdInputMapper.ToCore(x.Input, path + ".input"));
            reviews.Add(HouseholdStoreMapper.ReviewItems(x.UnresolvedLegacyItems, path + ".unresolvedLegacyItems")
                .Select(HouseholdStoreMapper.ToApi).ToArray());
            var input = inputs[^1];
            var usedKeys = new[] { input.Tax, input.Insurance, input.Service, input.Repairs, input.CustomCosts }
                .SelectMany(category => category?.Items ?? []).Select(item => item.Key.Trim())
                .Concat((input.EnergySources ?? []).Select(item => item.Key.Trim()))
                .Concat((input.Lease?.EndFees ?? []).Concat(input.Lease?.OtherPayments ?? []).Select(item => item.Key.Trim()))
                .ToHashSet(StringComparer.Ordinal);
            if (reviews[^1].Any(item => usedKeys.Contains(item.Input.Key)))
                throw HouseholdInputMapper.Error(path + ".unresolvedLegacyItems", "unresolvedLegacyItemIncluded",
                    "An unresolved source cannot also contribute to calculations.");
        }
        var profile = HouseholdInputMapper.ToCore(request.Profile, "profile");
        var result = calculator.Calculate(profile, inputs);
        var vehicles = result.Vehicles.Select((x, i) =>
        {
            var sections = HouseholdReviewCompleteness.Apply(HouseholdResultMapper.ToApi(x), reviews[i]);
            return new HouseholdVehiclePreview(x.CandidateKey, identities[i], HouseholdInputMapper.ToApi(inputs[i]),
                reviews[i], sections.Totals.OwnershipCost.State == CostSectionState.Complete, sections);
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return Ok(new HouseholdPreviewResponse(request.RequestId, HouseholdInputMapper.ToApi(profile),
            result.Currency, result.CalculationVersion, result.ResultSchemaVersion,
            (SensitivityMode)result.ActiveSensitivityMode, result.ProfileErrors.Select(x => HouseholdResultMapper.ToApi(x)).ToArray(), vehicles));
    }
}
