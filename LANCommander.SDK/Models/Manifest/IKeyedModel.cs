using System;

namespace LANCommander.SDK.Models.Manifest;

public interface IKeyedModel
{
    public Guid Id { get; set; }
}