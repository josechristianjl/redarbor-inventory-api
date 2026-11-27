using InventoryManagement.Api.Contracts.Categories;
using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Categories.Commands.CreateCategory;
using InventoryManagement.Application.Categories.Commands.DeleteCategory;
using InventoryManagement.Application.Categories.Commands.UpdateCategory;
using InventoryManagement.Application.Categories.Queries.GetCategories;
using InventoryManagement.Application.Categories.Queries.GetCategoryById;
using InventoryManagement.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/categories")]
[ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status401Unauthorized,
    "application/problem+json")]
[ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status403Forbidden,
    "application/problem+json")]
[ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status500InternalServerError,
    "application/problem+json")]
public sealed class CategoriesController : ControllerBase
{
    private readonly IQueryHandler<GetCategoriesQuery,
        PagedResult<CategoryReadModel>> _getCategories;
    private readonly IQueryHandler<GetCategoryByIdQuery,
        CategoryReadModel> _getCategoryById;
    private readonly ICommandHandler<CreateCategoryCommand, int>
        _createCategory;
    private readonly ICommandHandler<UpdateCategoryCommand>
        _updateCategory;
    private readonly ICommandHandler<DeleteCategoryCommand>
        _deleteCategory;

    public CategoriesController(
        IQueryHandler<GetCategoriesQuery,
            PagedResult<CategoryReadModel>> getCategories,
        IQueryHandler<GetCategoryByIdQuery,
            CategoryReadModel> getCategoryById,
        ICommandHandler<CreateCategoryCommand, int> createCategory,
        ICommandHandler<UpdateCategoryCommand> updateCategory,
        ICommandHandler<DeleteCategoryCommand> deleteCategory)
    {
        ArgumentNullException.ThrowIfNull(getCategories);
        ArgumentNullException.ThrowIfNull(getCategoryById);
        ArgumentNullException.ThrowIfNull(createCategory);
        ArgumentNullException.ThrowIfNull(updateCategory);
        ArgumentNullException.ThrowIfNull(deleteCategory);
        _getCategories = getCategories;
        _getCategoryById = getCategoryById;
        _createCategory = createCategory;
        _updateCategory = updateCategory;
        _deleteCategory = deleteCategory;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResult<CategoryReadModel>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    public async Task<ActionResult<PagedResult<CategoryReadModel>>> GetAll(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetCategoriesQuery(
            new PageRequest(page, pageSize));
        var result = await _getCategories.HandleAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(
        typeof(CategoryReadModel),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    public async Task<ActionResult<CategoryReadModel>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var query = new GetCategoryByIdQuery(id);
        var result = await _getCategoryById.HandleAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(CreatedResourceResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status415UnsupportedMediaType,
        "application/problem+json")]
    public async Task<ActionResult<CreatedResourceResponse>> Create(
        [FromBody] CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCategoryCommand(
            request.Name!, request.Description);
        var id = await _createCategory.HandleAsync(
            command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            new CreatedResourceResponse(id));
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status415UnsupportedMediaType,
        "application/problem+json")]
    public async Task<IActionResult> Update(
        [FromRoute] int id,
        [FromBody] CategoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCategoryCommand(
            id, request.Name!, request.Description);
        await _updateCategory.HandleAsync(
            command, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict,
        "application/problem+json")]
    public async Task<IActionResult> Delete(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCategoryCommand(id);
        await _deleteCategory.HandleAsync(
            command, cancellationToken);

        return NoContent();
    }
}
