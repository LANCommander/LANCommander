using System;

namespace LANCommander.SDK.Models.Manifest;

public class Save : BaseModel
{
    public Guid Id { get; set; }
    public string User { get; set; }
}