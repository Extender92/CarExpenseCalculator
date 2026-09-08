using System.Buffers;
using System.Data.Common;
using System.Text.Json;
using CarExpenseCalculator.Api.Contracts.Comparisons;
using CarExpenseCalculator.Core.Comparisons;
using CarExpenseCalculator.Core.Households;
using CarExpenseCalculator.Infrastructure.Persistence.Comparisons;
using CarExpenseCalculator.Infrastructure.Persistence.Households;
using CarExpenseCalculator.Infrastructure.Persistence.SavedListings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

namespace CarExpenseCalculator.Api.Comparisons;

internal sealed class ComparisonHttpBoundary(RequestDelegate next, CompleteComparisonLimits limits, TimeProvider timeProvider)
{
    public const int MaximumBodyBytes = 2 * 1024 * 1024;
    public static bool IsComparison(PathString path) => path.StartsWithSegments("/api/comparisons") ||
        path.StartsWithSegments("/api/rule-profile") || path.StartsWithSegments("/api/vehicle-facts");
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsComparison(context.Request.Path)) { await next(context); return; }
        var complete = string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/comparisons/preview-all", StringComparison.OrdinalIgnoreCase);
        var maximum = complete ? limits.MaximumRequestBytes : MaximumBodyBytes;
        var originalCancellation = context.RequestAborted;
        if (complete && !limits.Slots.Wait(0))
        {
            await Write(context, new ComparisonProblemDetails { Status = 503, Code = "comparisonBusy", Title = "Two complete comparisons are already running." });
            return;
        }
        using var deadline = complete ? new CancellationTokenSource(TimeSpan.FromSeconds(120), timeProvider) : null;
        using var linked = complete ? CancellationTokenSource.CreateLinkedTokenSource(originalCancellation, deadline!.Token) : null;
        if (linked is not null) context.RequestAborted = linked.Token;
        try
        {
            if (complete && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
                // Kestrel counts chunk framing in its wire limit. Our bounded reader
                // below owns the exact decoded UTF-8 limit, independent of framing.
                size.MaxRequestBodySize = context.Request.Headers.TransferEncoding.Count > 0 ? null : maximum;
            if (context.Request.ContentLength > maximum) { await TooLarge(context, maximum); return; }
            var original = context.Request.Body;
            await using var buffer = new MemoryStream();
            var bytes = ArrayPool<byte>.Shared.Rent(16 * 1024);
            try
            {
                int count;
                while ((count = await original.ReadAsync(bytes.AsMemory(0, Math.Min(bytes.Length,
                    maximum + 1 - (int)buffer.Length)), context.RequestAborted)) > 0)
                {
                    await buffer.WriteAsync(bytes.AsMemory(0, count), context.RequestAborted);
                    if (buffer.Length > maximum) { await TooLarge(context, maximum); return; }
                }
                buffer.Position = 0; context.Request.Body = buffer;
                await next(context);
            }
            finally { context.Request.Body = original; ArrayPool<byte>.Shared.Return(bytes, clearArray: true); }
        }
        catch (OperationCanceledException) when (deadline?.IsCancellationRequested == true && !originalCancellation.IsCancellationRequested)
        {
            context.RequestAborted = originalCancellation;
            if (context.Response.HasStarted) context.Abort();
            else await Write(context, new ComparisonProblemDetails { Status = 503, Code = "comparisonTimedOut", Title = "The complete comparison exceeded 120 seconds." });
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        { await TooLarge(context, maximum); }
        catch (ComparisonInputValidationException e)
        { await Write(context, Validation(context, e.Errors.Select(x => new ComparisonFieldError(x.Path, x.Code, x.Message)).ToArray())); }
        catch (VehicleFactsValidationException e)
        { await Write(context, Validation(context, e.Errors.Select(x => new ComparisonFieldError("input.edits." + x.Path, x.Code, x.Message)).ToArray())); }
        catch (HouseholdInputValidationException e)
        { await Write(context, Validation(context, e.Errors.Select(x => new ComparisonFieldError(x.Path, x.Code, x.Message)).ToArray())); }
        catch (ComparisonStoreException e)
        {
            var status = e.Code switch
            {
                "ruleProfileNotFound" or "vehicleNotFound" => 404, "payloadTooLarge" => 413,
                "comparisonStorageUnavailable" => 503, _ => 409,
            };
            await Write(context, new ComparisonProblemDetails { Type = "about:blank", Status = status, Title = e.Message,
                Instance = context.Request.Path, Code = e.Code, VehicleId = e.VehicleId,
                ExpectedRevision = e.ExpectedRevision, ActualRevision = e.ActualRevision, ActualBaselineToken = e.ActualBaselineToken });
        }
        catch (UnsupportedSavedListingVersionException)
        {
            await Write(context, new ComparisonProblemDetails { Type = "about:blank", Status = 409,
                Instance = context.Request.Path, Code = "unsupportedSavedListingVersion", Title = "The saved listing version is not supported." });
        }
        catch (HouseholdStoreException e)
        {
            var validation = e.Code is "invalidLegacyDecisions" or "invalidLegacyTarget" or "unresolvedLegacyItemIncluded" or "legacyDecisionsRequired";
            if (validation) await Write(context, Validation(context, [new("candidates", e.Code, e.Message)]));
            else await Write(context, new ComparisonProblemDetails { Type = "about:blank", Status = e.Code == "payloadTooLarge" ? 413 : 409,
                Title = e.Message, Instance = context.Request.Path, Code = e.Code, VehicleId = e.VehicleId,
                ExpectedRevision = e.ExpectedRevision, ActualRevision = e.ActualRevision });
        }
        catch (Exception e) when (e is DbException or DbUpdateException or JsonException or InvalidDataException
            or InvalidOperationException { InnerException: DbException })
        {
            context.RequestAborted.ThrowIfCancellationRequested();
            await Write(context, new ComparisonProblemDetails { Type = "about:blank", Status = 503, Instance = context.Request.Path,
                Code = "comparisonStorageUnavailable", Title = "Comparison storage is unavailable or its input cannot be read." });
        }
        finally
        {
            context.RequestAborted = originalCancellation;
            if (complete) limits.Slots.Release();
        }
    }
    internal static ComparisonValidationProblemDetails Validation(HttpContext context, IReadOnlyList<ComparisonFieldError> errors) => new()
    {
        Type = "about:blank", Status = 400, Title = "Comparison input is invalid.", Instance = context.Request.Path,
        Code = "invalidComparisonInput", FieldErrors = errors,
        Errors = errors.GroupBy(x => x.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray()),
    };
    private static Task TooLarge(HttpContext context, int maximum) => Write(context, new ComparisonProblemDetails
    { Type = "about:blank", Status = 413, Code = "payloadTooLarge", Title = "Request body exceeds the byte limit.",
        MaximumRequestBytes = maximum, Instance = context.Request.Path });
    private static Task Write(HttpContext context, ProblemDetails problem)
    {
        context.RequestAborted.ThrowIfCancellationRequested();
        if (context.Response.HasStarted) { context.Abort(); return Task.CompletedTask; }
        context.Response.StatusCode = problem.Status!.Value;
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
