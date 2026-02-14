using System.Text;
using Dapper;

public class OrderItemRepository(UnitOfWork unitOfWork) : IOrderItemRepository
{
    public async Task<V1OrderItemDal[]> BulkInsert(V1OrderItemDal[] models, CancellationToken token)
    {
        // пишем sql
        // после from можно увидеть unnest(@Orders) - это и есть механизм композитных типов
        var sql = @"
            insert into order_items
            (
             order_id,
             product_id,
             quantity,
             product_title,
             product_url,
             price_cents,
             price_currency,
             created_at,
             updated_at
             )
            select 
             order_id,
             product_id,
             quantity,
             product_title,
             product_url,
             price_cents,
             price_currency,
             created_at,
             updated_at
            from unnest(@OrderItems)
            returning 
             id,
             order_id,
             product_id,
             quantity,
             product_title,
             product_url,
             price_cents,
             price_currency,
             created_at,
             updated_at;
        ";

        // из unitOfWork получаем соединение
        var conn = await unitOfWork.GetConnection(token);
        // выполняем запрос на query, потому что после 
        // bulk-insert-a мы захотели returning заинсерченных строк.
        // new {Orders = model} - это динамический тип данных
        // Dapper просто возьмет название поля и заменит в sql-запросе @Orders на наши модели
        var res = await conn.QueryAsync<V1OrderItemDal>(new CommandDefinition(
            sql, new {OrderItems = models}, cancellationToken: token));
        
        return res.ToArray();
    }

    public async Task<V1OrderItemDal[]> Query(QueryOrderItemsDalModel model, CancellationToken token)
    {
        var sql = new StringBuilder(@"
            select 
                 id,
                 order_id,
                 product_id,
                 quantity,
                 product_title,
                 product_url,
                 price_cents,
                 price_currency,
                 created_at,
                 updated_at
            from order_items
        ");
        
        // тот же динамический тип данных 
        var param = new DynamicParameters();
        
        // собираем условия для where
        var conditions = new List<string>();

        if (model.Ids?.Length > 0)
        {
            // добавляем в динамический тип данные по айдишкам
            param.Add("Ids", model.Ids);
            conditions.Add("id = ANY(@Ids)");
        }
        
        if (model.OrderIds?.Length > 0)
        {
            param.Add("OrderIds", model.OrderIds);
            conditions.Add("customer_id = ANY(@CustomerIds)");
        }

        if (conditions.Count > 0)
        {
            // если условия есть, то добавляем в sql
            sql.Append(" where " + string.Join(" and ", conditions));
        }

        if (model.Limit > 0)
        {
            sql.Append(" limit @Limit");
            param.Add("Limit", model.Limit);
        }

        if (model.Offset > 0)
        {
            sql.Append(" offset @Offset");
            param.Add("Offset", model.Offset);
        }
        
        var conn = await unitOfWork.GetConnection(token);
        var res = await conn.QueryAsync<V1OrderItemDal>(new CommandDefinition(
            sql.ToString(), param, cancellationToken: token));
        
        return res.ToArray();
    }
}