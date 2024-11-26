using AutoMapper;
using Castle.Core.Logging;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Data
{
    public class RepositoryFactory
    {
        private readonly IDbContextFactory<DatabaseContext> ContextFactory;
        private readonly IMapper Mapper;
        private readonly IHttpContextAccessor HttpContextAccessor;

        public RepositoryFactory(
            IDbContextFactory<DatabaseContext> contextFactory,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor)
        {
            ContextFactory = contextFactory;
            Mapper = mapper;
            HttpContextAccessor = httpContextAccessor;
        }

        public Repository<T> Create<T>() where T : class, IBaseModel
        {
            return new Repository<T>(ContextFactory, Mapper, HttpContextAccessor);
        }
    }
}
