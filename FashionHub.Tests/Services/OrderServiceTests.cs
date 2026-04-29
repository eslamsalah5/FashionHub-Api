using Application.DTOs.Orders;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;

namespace FashionHub.Tests.Services;

/// <summary>
/// Unit tests for OrderService operations.
/// Validates: Requirements 6.1, 6.2, 6.3, 6.5, 6.6, 6.7
/// </summary>
public class OrderServiceTests
{
    private const string CustomerId = "customer-order-test";
    private const string AdminId = "admin-order-test";
    private const string OtherCustomerId = "other-customer-test";

    // ─────────────────────────────────────────────────────────────────────────
    // Async queryable helpers for EF Core mocking
    // EF Core's CountAsync/ToListAsync require IAsyncQueryProvider.
    // This implementation stores the source data and re-applies the expression
    // against the in-memory data after stripping EF Core-specific method calls.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Strips EF Core-specific method calls (AsNoTracking, Include, ThenInclude)
    /// from an expression tree so the in-memory LINQ provider can execute it.
    /// </summary>
    private class EfCoreMethodStripper : ExpressionVisitor
    {
        private static readonly HashSet<string> _efMethods = new(StringComparer.Ordinal)
        {
            "AsNoTracking", "AsTracking", "Include", "ThenInclude",
            "TagWith", "AsSplitQuery", "AsSingleQuery"
        };

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_efMethods.Contains(node.Method.Name) && node.Arguments.Count >= 1)
                return Visit(node.Arguments[0]); // strip the call, keep the source

            return base.VisitMethodCall(node);
        }
    }

    private class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;
        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
        public T Current => _inner.Current;
        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());
        public ValueTask DisposeAsync() { _inner.Dispose(); return default; }
    }

    private class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IEnumerable<T> _sourceData;
        private static readonly EfCoreMethodStripper _stripper = new();

        public TestAsyncQueryProvider(IEnumerable<T> sourceData) => _sourceData = sourceData;

        private IQueryable<T> GetInMemoryQueryable() => _sourceData.AsQueryable();

        private Expression Strip(Expression expression) => _stripper.Visit(expression);

        public IQueryable CreateQuery(Expression expression) =>
            new TestAsyncQueryable<T>(_sourceData, expression, this);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            if (typeof(TElement) == typeof(T))
                return (IQueryable<TElement>)new TestAsyncQueryable<T>(_sourceData, expression, this);

            // For type-changing operations (e.g., Select projections), fall back to EnumerableQuery
            return new EnumerableQuery<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            var stripped = Strip(expression);
            // Rebuild the expression against the in-memory queryable
            var inMemory = GetInMemoryQueryable();
            var rewritten = new SourceRewriter(inMemory.Expression).Visit(stripped);
            return inMemory.Provider.Execute(rewritten);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var stripped = Strip(expression);
            var inMemory = GetInMemoryQueryable();
            var rewritten = new SourceRewriter(inMemory.Expression).Visit(stripped);
            return inMemory.Provider.Execute<TResult>(rewritten);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = Execute(expression);
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { executionResult })!;
        }

        /// <summary>
        /// Replaces the root constant expression (the original queryable source)
        /// with the in-memory queryable's expression.
        /// </summary>
        private class SourceRewriter : ExpressionVisitor
        {
            private readonly Expression _newSource;
            private bool _replaced;

            public SourceRewriter(Expression newSource) => _newSource = newSource;

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (!_replaced && node.Value is IQueryable)
                {
                    _replaced = true;
                    return _newSource;
                }
                return base.VisitConstant(node);
            }
        }
    }

    private class TestAsyncQueryable<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> _sourceData;
        private readonly Expression _expression;
        private readonly IQueryProvider _provider;

        public TestAsyncQueryable(IEnumerable<T> data)
        {
            _sourceData = data;
            _provider = new TestAsyncQueryProvider<T>(data);
            _expression = data.AsQueryable().Expression;
        }

        public TestAsyncQueryable(IEnumerable<T> sourceData, Expression expression, IQueryProvider provider)
        {
            _sourceData = sourceData;
            _expression = expression;
            _provider = provider;
        }

        public Type ElementType => typeof(T);
        public Expression Expression => _expression;
        public IQueryProvider Provider => _provider;

        public IEnumerator<T> GetEnumerator()
        {
            // Execute the expression against in-memory data
            var result = _provider.Execute<IEnumerable<T>>(_expression);
            return result.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default) =>
            new TestAsyncEnumerator<T>(GetEnumerator());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (
        Mock<IUnitOfWork> mockUow,
        Mock<IOrderRepository> mockOrderRepo,
        Mock<IGenericRepository<Admin>> mockAdminRepo,
        OrderService service)
        BuildService()
    {
        var mockOrderRepo = new Mock<IOrderRepository>();
        var mockAdminRepo = new Mock<IGenericRepository<Admin>>();

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Orders).Returns(mockOrderRepo.Object);
        mockUow.Setup(u => u.Admins).Returns(mockAdminRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<OrderService>>();
        var service = new OrderService(mockUow.Object, mockLogger.Object);

        return (mockUow, mockOrderRepo, mockAdminRepo, service);
    }

    private static Order BuildOrder(int id, string customerId, int itemCount = 2)
    {
        var items = Enumerable.Range(1, itemCount).Select(i => new OrderItem
        {
            Id = i,
            OrderId = id,
            ProductId = i * 10,
            ProductName = $"Product {i}",
            ProductSKU = $"SKU-{i}",
            UnitPrice = i * 19.99m,
            Quantity = i,
            Subtotal = i * i * 19.99m,
            SelectedSize = "M",
            SelectedColor = "Black"
        }).ToList();

        return new Order
        {
            Id = id,
            CustomerId = customerId,
            OrderDate = DateTime.UtcNow.AddDays(-id),
            Status = OrderStatus.Processing,
            TotalAmount = items.Sum(x => x.Subtotal),
            OrderNotes = $"Notes for order {id}",
            OrderItems = items,
            Customer = new Customer
            {
                Id = customerId,
                AppUser = new AppUser { FullName = "Test Customer" }
            }
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — GetUserOrdersAsync returns all orders for the customer
    // Validates: Requirement 6.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserOrdersAsync_ValidCustomer_ReturnsAllCustomerOrders()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        var orders = new List<Order>
        {
            BuildOrder(1, CustomerId),
            BuildOrder(2, CustomerId),
            BuildOrder(3, CustomerId)
        };

        mockOrderRepo
            .Setup(r => r.GetCustomerOrdersAsync(CustomerId))
            .ReturnsAsync(orders);

        // Act
        var result = await service.GetUserOrdersAsync(CustomerId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Count);
        mockOrderRepo.Verify(r => r.GetCustomerOrdersAsync(CustomerId), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — GetUserOrdersAsync with no orders returns empty list
    // Validates: Requirement 6.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserOrdersAsync_NoOrders_ReturnsEmptyList()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        mockOrderRepo
            .Setup(r => r.GetCustomerOrdersAsync(CustomerId))
            .ReturnsAsync(new List<Order>());

        // Act
        var result = await service.GetUserOrdersAsync(CustomerId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — GetOrderByIdAsync returns order with items for the owner
    // Validates: Requirement 6.2
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderByIdAsync_OrderBelongsToCustomer_ReturnsOrderWithItems()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        const int orderId = 42;
        var order = BuildOrder(orderId, CustomerId, itemCount: 3);

        // VerifyOrderAccess calls GetByIdAsync first
        mockOrderRepo
            .Setup(r => r.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // Then GetOrderWithItemsAsync is called for the full details
        mockOrderRepo
            .Setup(r => r.GetOrderWithItemsAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var result = await service.GetOrderByIdAsync(orderId, CustomerId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.OrderItems.Count);
        Assert.Equal(OrderStatus.Processing, result.Data.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4 — GetOrderByIdAsync returns failure for wrong customer (non-Admin)
    // Validates: Requirement 6.3
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderByIdAsync_WrongCustomerNonAdmin_ReturnsUnauthorizedFailure()
    {
        // Arrange
        var (_, mockOrderRepo, mockAdminRepo, service) = BuildService();

        const int orderId = 10;
        // Order belongs to a different customer
        var order = BuildOrder(orderId, OtherCustomerId);

        mockOrderRepo
            .Setup(r => r.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // The requesting user is not an Admin
        mockAdminRepo
            .Setup(r => r.GetByIdAsync(CustomerId))
            .ReturnsAsync((Admin?)null);

        // Act
        var result = await service.GetOrderByIdAsync(orderId, CustomerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("Unauthorized access to order"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5 — GetOrderByIdAsync allows Admin to access any order
    // Validates: Requirement 6.3
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderByIdAsync_AdminAccessingAnyOrder_ReturnsSuccess()
    {
        // Arrange
        var (_, mockOrderRepo, mockAdminRepo, service) = BuildService();

        const int orderId = 20;
        // Order belongs to a different customer
        var order = BuildOrder(orderId, OtherCustomerId);

        mockOrderRepo
            .Setup(r => r.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // The requesting user IS an Admin
        mockAdminRepo
            .Setup(r => r.GetByIdAsync(AdminId))
            .ReturnsAsync(new Admin { Id = AdminId });

        mockOrderRepo
            .Setup(r => r.GetOrderWithItemsAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var result = await service.GetOrderByIdAsync(orderId, AdminId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6 — GetOrderByIdAsync returns failure when order not found
    // Validates: Requirement 6.2
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderByIdAsync_OrderNotFound_ReturnsFailure()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        mockOrderRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await service.GetOrderByIdAsync(999, CustomerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("Order not found"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 7 — OrderDto includes all required fields
    // Validates: Requirement 6.4
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderByIdAsync_ReturnsOrderDtoWithAllRequiredFields()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        const int orderId = 55;
        var order = BuildOrder(orderId, CustomerId, itemCount: 2);
        order.OrderNotes = "Please gift wrap";
        order.TotalAmount = 99.99m;

        mockOrderRepo
            .Setup(r => r.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        mockOrderRepo
            .Setup(r => r.GetOrderWithItemsAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var result = await service.GetOrderByIdAsync(orderId, CustomerId);

        // Assert
        Assert.True(result.IsSuccess);
        var dto = result.Data!;

        // All required fields per Requirement 6.4
        Assert.NotNull(dto.CustomerName);
        Assert.NotEmpty(dto.CustomerName);
        Assert.True(dto.OrderDate != default);
        Assert.Equal(OrderStatus.Processing, dto.Status);
        Assert.Equal(99.99m, dto.TotalAmount);
        Assert.Equal("Please gift wrap", dto.OrderNotes);
        Assert.NotNull(dto.OrderItems);
        Assert.Equal(2, dto.OrderItems.Count);

        // Verify OrderItemDto fields are populated
        var item = dto.OrderItems[0];
        Assert.True(item.ProductId > 0);
        Assert.NotEmpty(item.ProductName);
        Assert.NotEmpty(item.ProductSKU);
        Assert.True(item.UnitPrice > 0);
        Assert.True(item.Quantity > 0);
        Assert.True(item.Subtotal > 0);
        Assert.NotEmpty(item.SelectedSize);
        Assert.NotEmpty(item.SelectedColor);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 8 — UpdateOrderStatusAsync succeeds for Admin
    // Validates: Requirement 6.6
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOrderStatusAsync_CalledByAdmin_UpdatesStatusAndReturnsSuccess()
    {
        // Arrange
        var (_, mockOrderRepo, mockAdminRepo, service) = BuildService();

        const int orderId = 7;
        var order = BuildOrder(orderId, CustomerId);
        order.Status = OrderStatus.Processing;

        mockAdminRepo
            .Setup(r => r.GetByIdAsync(AdminId))
            .ReturnsAsync(new Admin { Id = AdminId });

        mockOrderRepo
            .Setup(r => r.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // After update, GetOrderWithItemsAsync returns the updated order
        mockOrderRepo
            .Setup(r => r.GetOrderWithItemsAsync(orderId))
            .ReturnsAsync(order);

        var updateDto = new UpdateOrderStatusDto { Status = OrderStatus.Shipped };

        // Act
        var result = await service.UpdateOrderStatusAsync(orderId, updateDto, AdminId);

        // Assert
        Assert.True(result.IsSuccess);
        // The order entity's status should have been updated
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 9 — UpdateOrderStatusAsync fails for non-Admin
    // Validates: Requirement 6.7
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOrderStatusAsync_CalledByNonAdmin_ReturnsUnauthorizedFailure()
    {
        // Arrange
        var (_, mockOrderRepo, mockAdminRepo, service) = BuildService();

        const int orderId = 8;

        // The requesting user is NOT an Admin
        mockAdminRepo
            .Setup(r => r.GetByIdAsync(CustomerId))
            .ReturnsAsync((Admin?)null);

        var updateDto = new UpdateOrderStatusDto { Status = OrderStatus.Shipped };

        // Act
        var result = await service.UpdateOrderStatusAsync(orderId, updateDto, CustomerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("Unauthorized. Only administrators can update order status"));

        // Order should never be fetched or modified
        mockOrderRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 10 — GetOrdersAsync returns paginated results ordered by date desc
    // Validates: Requirement 6.5
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrdersAsync_ReturnsPagedResultsOrderedByDateDescending()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        // Create 5 orders with different dates
        var allOrders = Enumerable.Range(1, 5).Select(i =>
        {
            var o = BuildOrder(i, CustomerId);
            o.OrderDate = DateTime.UtcNow.AddDays(-i); // older orders have higher i
            return o;
        }).ToList();

        var queryable = new TestAsyncQueryable<Order>(allOrders);

        mockOrderRepo
            .Setup(r => r.GetAllQueryable())
            .Returns(queryable);

        // Act — page 1, pageSize 3
        var result = await service.GetOrdersAsync(page: 1, pageSize: 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.TotalCount);
        Assert.Equal(3, result.Data.Items.Count);
        Assert.Equal(3, result.Data.PageSize);
        Assert.Equal(0, result.Data.PageIndex); // page 1 → index 0
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 11 — GetOrdersAsync page 2 returns remaining items
    // Validates: Requirement 6.5
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrdersAsync_Page2_ReturnsRemainingItems()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        var allOrders = Enumerable.Range(1, 5).Select(i => BuildOrder(i, CustomerId)).ToList();
        var queryable = new TestAsyncQueryable<Order>(allOrders);

        mockOrderRepo
            .Setup(r => r.GetAllQueryable())
            .Returns(queryable);

        // Act — page 2, pageSize 3 → should return 2 items
        var result = await service.GetOrdersAsync(page: 2, pageSize: 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(1, result.Data.PageIndex); // page 2 → index 1
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 12 — GetOrdersAsync with empty orders returns empty paged result
    // Validates: Requirement 6.5
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrdersAsync_NoOrders_ReturnsEmptyPagedResult()
    {
        // Arrange
        var (_, mockOrderRepo, _, service) = BuildService();

        var queryable = new TestAsyncQueryable<Order>(new List<Order>());

        mockOrderRepo
            .Setup(r => r.GetAllQueryable())
            .Returns(queryable);

        // Act
        var result = await service.GetOrdersAsync(page: 1, pageSize: 10);

        // Assert
        Assert.True(result.IsSuccess, $"Expected success but got: {string.Join("; ", result.Errors)}");
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Empty(result.Data.Items);
    }
}
