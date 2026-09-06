using System.Buffers;
using System.Data.Common;
using System.Text.Json;
using CarExpenseCalculator.Api.Contracts.Households;
using CarExpenseCalculator.Api.Mapping;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarExpenseCalculator.Api.Households;

// A bounded memory buffer also enforces the limit in TestServer and for chunked
// requests. It never spills financial input to ASP.NET temporary files.
internal sealed class HouseholdHttpBoundary(RequestDelegate next)
{
    public const int MaximumBodyBytes = 2 * 1024 * 1024;
    private static readonly string[] Prefixes = ["/api/household-calculations", "/api/household-profile",
        "/api/vehicle-cost-inputs", "/api/household-transition", "/api/vehicle-draft"];
    public static bool IsHousehold(PathString path) => Prefixes.Any(prefix => path.StartsWithSegments(prefix));

    public async Task InvokeAsync(HttpContext context)
    {
        var household = IsHousehold(context.Request.Path);
        try
        {
            if (!household) { await next(context); return; }
            if (context.Request.ContentLength > MaximumBodyBytes)
            {
                await Write(context, Problem(context, 413, "payloadTooLarge", "Request body exceeds 2 MiB."));
                return;
            }
            var original = context.Request.Body;
            await using var buffer = new MemoryStream();
            var bytes = ArrayPool<byte>.Shared.Rent(16 * 1024);
            try
            {
                int count;
                while ((count = await original.ReadAsync(bytes.AsMemory(0, Math.Min(bytes.Length,
                    MaximumBodyBytes + 1 - (int)buffer.Length)), context.RequestAborted)) > 0)
                {
                    await buffer.WriteAsync(bytes.AsMemory(0, count), context.RequestAborted);
                    if (buffer.Length > MaximumBodyBytes)
                    {
                        await Write(context, Problem(context, 413, "payloadTooLarge", "Request body exceeds 2 MiB."));
                        return;
                    }
                }
                buffer.Position = 0;
                context.Request.Body = buffer;
                await next(context);
            }
            finally
            {
                context.Request.Body = original;
                ArrayPool<byte>.Shared.Return(bytes, clearArray: true);
            }
        }
        catch (HouseholdInputValidationException e) when (household)
        {
            var preview = context.Request.Path.StartsWithSegments("/api/household-calculations");
            await Write(context, Validation(context, e.Errors.Select(x => preview ? HouseholdResultMapper.ToApi(x)
                : new Contracts.Households.HouseholdInputError(x.Path, x.Code, x.Message)).ToArray()));
        }
        catch (SavedListingRequestMappingException e) when (household)
        {
            await Write(context, Validation(context, e.Errors.Select(x =>
                new Contracts.Households.HouseholdInputError("input.listing." + x.Path, "invalidListing", x.Message)).ToArray()));
        }
        catch (ListingValidationException e) when (household)
        {
            await Write(context, Validation(context, e.Errors.Select(x =>
                new Contracts.Households.HouseholdInputError("input.listing." + x.Path, "invalidListing", x.Message)).ToArray()));
        }
        catch (HouseholdStoreException e) when (household || context.Request.Path.StartsWithSegments("/api/saved-cost-scenarios")
            || context.Request.Path.StartsWithSegments("/api/saved-listings"))
        {
            var status = e.Code switch
            {
                "profileNotFound" or "vehicleNotFound" or "draftEmpty" => 404,
                "payloadTooLarge" => 413,
                "invalidVehicleWrite" or "invalidLegacyDecisions" or "invalidLegacyTarget" or "unresolvedLegacyItemIncluded"
                    or "legacyDecisionsRequired" or "invalidTransitionSet" or "invalidDraft" => 400,
                _ => 409,
            };
            if (status == 400)
            {
                var path = context.Request.Path.StartsWithSegments("/api/household-transition") ? "vehicles"
                    : context.Request.Path.StartsWithSegments("/api/vehicle-draft") ? "input" : "cost";
                await Write(context, Validation(context, [new(path, e.Code, e.Message)]));
                return;
            }
            await Write(context, new HouseholdProblemDetails
            {
                Type = "about:blank", Status = status, Title = e.Message, Instance = context.Request.Path,
                Code = e.Code, VehicleId = e.VehicleId, ExpectedRevision = e.ExpectedRevision, ActualRevision = e.ActualRevision,
                RecoveryRoute = e.Code == "householdTransitionRequired"
                    ? context.Request.Path.StartsWithSegments("/api/saved-cost-scenarios") && e.VehicleId is { } id
                        ? $"/api/vehicle-cost-inputs/{id}" : "/api/household-transition" : null,
            });
        }
        catch (Exception e) when (household && e is DbException or DbUpdateException or JsonException
            or InvalidOperationException { InnerException: DbException })
        {
            context.RequestAborted.ThrowIfCancellationRequested();
            await Write(context, Problem(context, 503, "householdStorageUnavailable", "Household storage is unavailable or its input cannot be read."));
        }
    }

    internal static HouseholdValidationProblemDetails Validation(HttpContext context,
        IReadOnlyList<Contracts.Households.HouseholdInputError> errors) => new()
    {
        Type = "about:blank", Status = 400, Title = "Household input is invalid.", Instance = context.Request.Path,
        Code = "invalidHouseholdInput", FieldErrors = errors,
        Errors = errors.GroupBy(x => x.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray()),
    };
    private static HouseholdProblemDetails Problem(HttpContext context, int status, string code, string title) => new()
    { Type = "about:blank", Status = status, Code = code, Title = title, Instance = context.Request.Path };

    private static Task Write(HttpContext context, ProblemDetails problem)
    {
        context.RequestAborted.ThrowIfCancellationRequested();
        context.Response.StatusCode = problem.Status!.Value;
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
