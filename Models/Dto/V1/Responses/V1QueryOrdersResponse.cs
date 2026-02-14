using Models.Dto.Common;

namespace Models.Dto.V1.Responses;

public class V1CreateOrderResponse
{
    public OrderUnit[] Orders { get; set; }
}
