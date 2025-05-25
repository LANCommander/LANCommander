using System;

namespace LANCommander.SDK.Models
{
    public class GenericGuidsRequest
    {
        public static GenericGuidsRequest Empty => new();

        public Guid[] Guids { get; set; } = [];
    }
}
