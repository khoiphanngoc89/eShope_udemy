using MediatR;

namespace BuildingBlock.Abstractions.Cqrs;

public interface IQuery<out TResponse> : IRequest<TResponse>
    where TResponse : notnull
{
}