namespace BuildingBlock.Exceptions;

public class SearchIndexUnavailableException : ServiceUnavailableException
{
    public SearchIndexUnavailableException(string message) : base(message)
    {
    }

    public SearchIndexUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
