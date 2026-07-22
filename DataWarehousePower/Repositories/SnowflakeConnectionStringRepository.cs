using DataWarehousePower.Data;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class SnowflakeConnectionStringRepository : ISnowflakeConnectionStringRepository
    {
        private readonly AppDbContext _context;

        public SnowflakeConnectionStringRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SnowflakeConnectionString>> GetAllAsync()
            => await _context.SnowflakeConnectionStrings
                .AsNoTracking()
                .OrderBy(connection => connection.Name)
                .ThenBy(connection => connection.Id)
                .ToListAsync();

        public async Task<SnowflakeConnectionString?> GetByIdAsync(int id)
            => await _context.SnowflakeConnectionStrings
                .AsNoTracking()
                .FirstOrDefaultAsync(connection => connection.Id == id);

        public async Task<SnowflakeConnectionString> CreateAsync(SnowflakeConnectionString snowflakeConnectionString)
        {
            _context.SnowflakeConnectionStrings.Add(snowflakeConnectionString);
            await _context.SaveChangesAsync();
            return snowflakeConnectionString;
        }

        public async Task UpdateAsync(SnowflakeConnectionString request)
        {
            SnowflakeConnectionString? connection = await _context.SnowflakeConnectionStrings
                .FirstOrDefaultAsync(item => item.Id == request.Id);

            if (connection is null)
            {
                return;
            }

            connection.Name = request.Name;
            connection.Description = request.Description;
            connection.ConnectionString = request.ConnectionString;
            connection.ModifiedBy = request.ModifiedBy;
            connection.ModifiedAt = DateTimeHelper.GetCurrentLocalTime();

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            SnowflakeConnectionString? connection = await _context.SnowflakeConnectionStrings
                .FirstOrDefaultAsync(item => item.Id == id);

            if (connection is null)
            {
                return;
            }

            _context.SnowflakeConnectionStrings.Remove(connection);
            await _context.SaveChangesAsync();
        }
    }
}