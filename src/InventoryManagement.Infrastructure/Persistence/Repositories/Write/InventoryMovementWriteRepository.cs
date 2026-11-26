using System.Data;
using Dapper;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Infrastructure.Persistence.Connections;
using Microsoft.Data.SqlClient;

namespace InventoryManagement.Infrastructure.Persistence.Repositories.Write;

public sealed class InventoryMovementWriteRepository
    : IInventoryMovementWriteRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public InventoryMovementWriteRepository(
        SqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async Task<int> RegisterAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken)
    {
        ValidateNewMovement(movement);
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
        var product = await ProductLockReader.GetActiveAsync(
            transaction, movement.ProductId, cancellationToken);
        ApplyMovement(product, movement);
        await PersistStockAsync(transaction, product, cancellationToken);
        var id = await InsertMovementAsync(
            transaction, movement, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return id;
    }

    private static void ValidateNewMovement(InventoryMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        if (movement.Id != 0)
        {
            throw new ArgumentException(
                "Only new inventory movements can be registered.",
                nameof(movement));
        }
    }

    private static void ApplyMovement(
        Product product,
        InventoryMovement movement)
    {
        switch (movement.Type)
        {
            case MovementType.Entry:
                product.RegisterEntry(movement.Quantity);
                break;
            case MovementType.Exit:
                product.RegisterExit(movement.Quantity);
                break;
            default:
                throw new InvalidOperationException(
                    "The inventory movement type is invalid.");
        }
    }

    private static async Task PersistStockAsync(
        SqlTransaction transaction,
        Product product,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Products
            SET Stock = @Stock
            WHERE Id = @Id;
            """;
        var connection = transaction.Connection!;
        var affectedRows = await connection.ExecuteAsync(
            new CommandDefinition(sql,
                new { product.Id, product.Stock },
                transaction, cancellationToken: cancellationToken));
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                "The product stock could not be updated.");
        }
    }

    private static async Task<int> InsertMovementAsync(
        SqlTransaction transaction,
        InventoryMovement movement,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.InventoryMovements
                (ProductId, Type, Quantity, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@ProductId, @Type, @Quantity, @CreatedAt);
            """;
        var connection = transaction.Connection!;
        var parameters = new DynamicParameters();
        parameters.Add("ProductId", movement.ProductId, DbType.Int32);
        parameters.Add("Type", (byte)movement.Type, DbType.Byte);
        parameters.Add("Quantity", movement.Quantity, DbType.Int32);
        parameters.Add("CreatedAt", movement.CreatedAt,
            DbType.DateTime2, scale: 7);

        return await connection.QuerySingleAsync<int>(
            new CommandDefinition(sql, parameters,
                transaction, cancellationToken: cancellationToken));
    }
}
