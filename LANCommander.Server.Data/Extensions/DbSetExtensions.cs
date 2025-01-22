using AutoMapper.QueryableExtensions;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Data.Extensions
{
    public static class QueryableExtensions
    {
        public static async Task<T> FindAsync<T>(this IQueryable<T> queryable, Guid id) where T : class, IBaseModel
        {
            return await queryable.FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
