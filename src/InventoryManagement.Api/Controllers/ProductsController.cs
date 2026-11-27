using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Api.Contracts.Products;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Models;
using InventoryManagement.Application.Products.Commands.CreateProduct;
using InventoryManagement.Application.Products.Commands.DeleteProduct;
using InventoryManagement.Application.Products.Commands.UpdateProduct;
using InventoryManagement.Application.Products.Queries.GetProductById;
using InventoryManagement.Application.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/products")]
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
public sealed class ProductsController : ControllerBase
{
    private readonly IQueryHandler<GetProductsQuery,
        PagedResult<ProductReadModel>> _getProducts;
    private readonly IQueryHandler<GetProductByIdQuery,
        ProductReadModel> _getProductById;
    private readonly ICommandHandler<CreateProductCommand, int>
        _createProduct;
    private readonly ICommandHandler<UpdateProductCommand>
        _updateProduct;
    private readonly ICommandHandler<DeleteProductCommand>
        _deleteProduct;

    public ProductsController(
        IQueryHandler<GetProductsQuery,
            PagedResult<ProductReadModel>> getProducts,
        IQueryHandler<GetProductByIdQuery,
            ProductReadModel> getProductById,
        ICommandHandler<CreateProductCommand, int> createProduct,
        ICommandHandler<UpdateProductCommand> updateProduct,
        ICommandHandler<DeleteProductCommand> deleteProduct)
    {
        ArgumentNullException.ThrowIfNull(getProducts);
        ArgumentNullException.ThrowIfNull(getProductById);
        ArgumentNullException.ThrowIfNull(createProduct);
        ArgumentNullException.ThrowIfNull(updateProduct);
        ArgumentNullException.ThrowIfNull(deleteProduct);
        _getProducts = getProducts;
        _getProductById = getProductById;
        _createProduct = createProduct;
        _updateProduct = updateProduct;
        _deleteProduct = deleteProduct;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResult<ProductReadModel>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    public async Task<ActionResult<PagedResult<ProductReadModel>>> GetAll(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetProductsQuery(
            new PageRequest(page, pageSize));
        var result = await _getProducts.HandleAsync(
            query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(
        typeof(ProductReadModel),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    public async Task<ActionResult<ProductReadModel>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var query = new GetProductByIdQuery(id);
        var result = await _getProductById.HandleAsync(
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
    public async Task<ActionResult<CreatedResourceResponse>> Create(
        [FromBody] ProductWriteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(
            request.Sku!,
            request.Name!,
            request.CategoryId,
            request.Description);
        var id = await _createProduct.HandleAsync(
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
        [FromBody] ProductWriteRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(
            id,
            request.Sku!,
            request.Name!,
            request.CategoryId,
            request.Description);
        await _updateProduct.HandleAsync(
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
        var command = new DeleteProductCommand(id);
        await _deleteProduct.HandleAsync(
            command, cancellationToken);

        return NoContent();
    }
}
