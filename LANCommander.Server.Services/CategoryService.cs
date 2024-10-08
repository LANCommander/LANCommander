﻿using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services
{
    public class CategoryService : BaseDatabaseService<Category>
    {
        public CategoryService(
            ILogger<CategoryService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }
    }
}