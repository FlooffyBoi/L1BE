namespace L1BE.BLL;
public class OrderService(UnitOfWork unitOfWork, IOrderRepository orderRepository, IOrderItemRepository orderItemRepository)
{
    /// <summary>
    /// Метод создания заказов
    /// </summary>
    public async Task<OrderUnit[]> BatchInsert(OrderUnit[] orderUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);

        try
        {
            // тут ваш бизнес код по инсерту данных в БД
            // нужно положить в БД заказы(orders), а потом их позиции (orderItems)
            // помните, что каждый orderItem содержит ссылку на order (столбец order_id)
            // OrderItem-ов может быть несколько

            V1OrderDal[] orders = orderUnits.Select(u => new V1OrderDal
            {
                CustomerId = u.CustomerId,
                DeliveryAddress = u.DeliveryAddress,
                TotalPriceCents = u.TotalPriceCents,
                TotalPriceCurrency = u.TotalPriceCurrency,
                CreatedAt = now,
                UpdatedAt = now,
                
            }).ToArray();

            var insertedOrders = await orderRepository.BulkInsert(orders, token);
            var orderItems = new List<V1OrderItemDal>();
            for (int i = 0; i < orders.Length; i++)
            {
                var orderId = insertedOrders[i].Id;
                var items = orderUnits[i].OrderItems.Select(item => new V1OrderItemDal
                {
                    OrderId = orderId, // Связываем позицию с созданным заказом
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    ProductTitle = item.ProductTitle,
                    ProductUrl = item.ProductUrl,
                    PriceCents = item.PriceCents,
                    PriceCurrency = item.PriceCurrency,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                orderItems.AddRange(items);
            }
            var insertedItems = await orderItemRepository.BulkInsert(orderItems.ToArray(), token);
            await transaction.CommitAsync(token);
            var itemsLookup = insertedItems.ToLookup(x => x.OrderId);
            return Map(insertedOrders, itemsLookup);
        }
        catch (Exception e) 
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }
    
    /// <summary>
    /// Метод получения заказов
    /// </summary>
    public async Task<OrderUnit[]> GetOrders(QueryOrderItemsModel model, CancellationToken token)
    {
        var orders = await orderRepository.Query(new QueryOrdersDalModel
        {
            Ids = model.Ids,
            CustomerIds = model.CustomerIds,
            Limit = model.PageSize,
            Offset = (model.Page - 1) * model.PageSize
        }, token);

        if (orders.Length is 0)
        {
            return [];
        }
        
        ILookup<long, V1OrderItemDal> orderItemLookup = null;
        if (model.IncludeOrderItems)
        {
            var orderItems = await orderItemRepository.Query(new QueryOrderItemsDalModel
            {
                OrderIds = orders.Select(x => x.Id).ToArray(),
            }, token);

            orderItemLookup = orderItems.ToLookup(x => x.OrderId);
        }

        return Map(orders, orderItemLookup);
    }
    
    private OrderUnit[] Map(V1OrderDal[] orders, ILookup<long, V1OrderItemDal> orderItemLookup = null)
    {
        return orders.Select(x => new OrderUnit
        {
            Id = x.Id,
            CustomerId = x.CustomerId,
            DeliveryAddress = x.DeliveryAddress,
            TotalPriceCents = x.TotalPriceCents,
            TotalPriceCurrency = x.TotalPriceCurrency,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            OrderItems = orderItemLookup?[x.Id].Select(o => new OrderItemUnit
            {
                Id = o.Id,
                OrderId = o.OrderId,
                ProductId = o.ProductId,
                Quantity = o.Quantity,
                ProductTitle = o.ProductTitle,
                ProductUrl = o.ProductUrl,
                PriceCents = o.PriceCents,
                PriceCurrency = o.PriceCurrency,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            }).ToArray() ?? []
        }).ToArray();
    }
}