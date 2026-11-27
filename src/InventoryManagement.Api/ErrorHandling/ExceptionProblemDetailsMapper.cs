using FluentValidation;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace InventoryManagement.Api.ErrorHandling;

internal static class ExceptionProblemDetailsMapper
{
    internal static ProblemDetails Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            ValidationException validationException =>
                MapValidationException(validationException),
            PersistenceRuleException persistenceException =>
                MapPersistenceException(persistenceException),
            DomainRuleException domainException =>
                MapDomainException(domainException),
            _ => CreateInternalServerError()
        };
    }

    private static ProblemDetails MapValidationException(
        ValidationException exception)
    {
        var problem = CreateProblem(
            StatusCodes.Status400BadRequest,
            "ValidationFailed",
            "One or more validation errors occurred.");
        problem.Extensions["errors"] = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage)
                    .ToArray());

        return problem;
    }

    private static ProblemDetails MapPersistenceException(
        PersistenceRuleException exception)
    {
        return exception.Code switch
        {
            PersistenceRuleCode.CategoryNotFound =>
                CreateNotFound("CategoryNotFound",
                    "The active category was not found."),
            PersistenceRuleCode.ProductNotFound =>
                CreateNotFound("ProductNotFound",
                    "The active product was not found."),
            PersistenceRuleCode.DuplicateCategoryName =>
                CreateConflict("DuplicateCategoryName",
                    "The category name is already reserved."),
            PersistenceRuleCode.DuplicateSku =>
                CreateConflict("DuplicateSku",
                    "The product SKU is already reserved."),
            PersistenceRuleCode.CategoryHasActiveProducts =>
                CreateConflict("CategoryHasActiveProducts",
                    "The category contains active products."),
            PersistenceRuleCode.ConcurrentProductChange =>
                CreateConflict("ConcurrentProductChange",
                    "The product was modified concurrently."),
            _ => CreateInternalServerError()
        };
    }

    private static ProblemDetails MapDomainException(
        DomainRuleException exception)
    {
        return exception.Code switch
        {
            DomainRuleCode.InactiveCategory =>
                CreateNotFound("InactiveCategory",
                    "The category is inactive."),
            DomainRuleCode.InactiveProduct =>
                CreateNotFound("InactiveProduct",
                    "The product is inactive."),
            DomainRuleCode.InsufficientStock =>
                CreateConflict("InsufficientStock",
                    "Available stock is insufficient."),
            DomainRuleCode.StockLimitExceeded =>
                CreateConflict("StockLimitExceeded",
                    "The movement exceeds the maximum stock limit."),
            DomainRuleCode.ProductHasStock =>
                CreateConflict("ProductHasStock",
                    "A product with available stock cannot be deleted."),
            _ => CreateInternalServerError()
        };
    }

    private static ProblemDetails CreateNotFound(
        string code,
        string detail)
    {
        return CreateProblem(
            StatusCodes.Status404NotFound, code, detail);
    }

    private static ProblemDetails CreateConflict(
        string code,
        string detail)
    {
        return CreateProblem(
            StatusCodes.Status409Conflict, code, detail);
    }

    private static ProblemDetails CreateInternalServerError()
    {
        return CreateProblem(
            StatusCodes.Status500InternalServerError,
            "InternalServerError",
            "An unexpected error occurred.");
    }

    private static ProblemDetails CreateProblem(
        int status,
        string code,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = detail
        };
        problem.Extensions["code"] = code;

        return problem;
    }
}
