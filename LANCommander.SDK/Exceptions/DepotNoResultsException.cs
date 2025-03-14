using System;

namespace LANCommander.SDK.Exceptions;

public class DepotNoResultsException : Exception
{
    public DepotNoResultsException(string message) : base(message)
    {
        
    }
}