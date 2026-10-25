using MediatR;

namespace BuildingBlock.Cqrs;

public interface IQuery<out TResponse> : IRequest<TResponse>
    where TResponse : notnull
{
}