using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private const int MaxPageSize = 200;
        private readonly AppDbContext _context;

        public AuditLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<AuditLog?> GetByIdAsync(long id)
            => _context.AuditLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task<(List<AuditLog> Logs, int TotalCount)> SearchAsync(AuditLogFilterViewModel filter)
        {
            IQueryable<AuditLog> query = _context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.UserId))
            {
                string userValue = filter.UserId.Trim();
                query = query.Where(a => a.UserId == userValue || a.Username == userValue);
            }

            if (!string.IsNullOrWhiteSpace(filter.ActionType))
            {
                string actionType = filter.ActionType.Trim();
                query = query.Where(a => a.ActionType == actionType);
            }

            if (!string.IsNullOrWhiteSpace(filter.EntityName))
            {
                string entityName = filter.EntityName.Trim();
                query = query.Where(a => a.EntityName == entityName);
            }

            if (!string.IsNullOrWhiteSpace(filter.IpAddress))
            {
                string ipAddress = filter.IpAddress.Trim();
                query = query.Where(a => a.IpAddress != null && EF.Functions.Like(a.IpAddress, $"%{ipAddress}%"));
            }

            if (!string.IsNullOrWhiteSpace(filter.Host))
            {
                string host = filter.Host.Trim();
                query = query.Where(a => a.Host != null && EF.Functions.Like(a.Host, $"%{host}%"));
            }

            if (filter.DateFromUtc.HasValue)
            {
                DateTime start = filter.DateFromUtc.Value.Date;
                query = query.Where(a => a.TimestampUtc >= start);
            }

            if (filter.DateToUtc.HasValue)
            {
                DateTime endExclusive = filter.DateToUtc.Value.Date.AddDays(1);
                query = query.Where(a => a.TimestampUtc < endExclusive);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string keyword = filter.Search.Trim();
                query = query.Where(a =>
                    EF.Functions.Like(a.UserId, $"%{keyword}%") ||
                    EF.Functions.Like(a.Username, $"%{keyword}%") ||
                    EF.Functions.Like(a.ActionType, $"%{keyword}%") ||
                    EF.Functions.Like(a.Description, $"%{keyword}%") ||
                    EF.Functions.Like(a.EntityName, $"%{keyword}%") ||
                    (a.EntityId != null && EF.Functions.Like(a.EntityId, $"%{keyword}%")) ||
                        (a.IpAddress != null && EF.Functions.Like(a.IpAddress, $"%{keyword}%")) ||
                        (a.Host != null && EF.Functions.Like(a.Host, $"%{keyword}%")) ||
                        (a.RequestPath != null && EF.Functions.Like(a.RequestPath, $"%{keyword}%")) ||
                        (a.RequestMethod != null && EF.Functions.Like(a.RequestMethod, $"%{keyword}%")) ||
                    (a.Metadata != null && EF.Functions.Like(a.Metadata, $"%{keyword}%")));
            }

            int totalCount = await query.CountAsync();

            int pageSize = filter.PageSize <= 0 ? 25 : Math.Min(filter.PageSize, MaxPageSize);
            int pageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber;
            int skip = (pageNumber - 1) * pageSize;

            List<AuditLog> logs = await query
                .OrderByDescending(a => a.TimestampUtc)
                .ThenByDescending(a => a.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            return (logs, totalCount);
        }

        public async Task<List<string>> GetDistinctUsersAsync()
            => await _context.AuditLogs
                .AsNoTracking()
                .Select(a => string.IsNullOrWhiteSpace(a.Username) ? a.UserId : a.Username)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

        public async Task<List<string>> GetDistinctActionTypesAsync()
            => await _context.AuditLogs
                .AsNoTracking()
                .Select(a => a.ActionType)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

        public async Task<List<string>> GetDistinctEntityNamesAsync()
            => await _context.AuditLogs
                .AsNoTracking()
                .Select(a => a.EntityName)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
    }
}
