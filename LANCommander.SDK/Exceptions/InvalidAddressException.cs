using System;

namespace LANCommander.SDK.Exceptions;

public class InvalidAddressException(string message) : Exception(message)
{
    
}