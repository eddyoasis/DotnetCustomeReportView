using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories;

public sealed class ScheduledReportJobRepository(AppDbContext dbContext) : IScheduledReportJobRepository
{
    public async Task<List<ScheduledReportJob>> GetAllAsync()
    {
        return await dbContext.ScheduledReportJobs
            .AsNoTracking()
            .Include(job => job.ReportDefinition)
            .OrderByDescending(job => job.CreatedUtc)
            .ToListAsync();
    }

    public async Task<ScheduledReportJob?> GetByIdAsync(int id)
    {
        return await dbContext.ScheduledReportJobs
            .AsNoTracking()
            .Include(job => job.ReportDefinition)
            .FirstOrDefaultAsync(job => job.Id == id);
    }

    public async Task<ScheduledReportJob?> GetByIdForUpdateAsync(int id)
    {
        return await dbContext.ScheduledReportJobs
            .Include(job => job.ReportDefinition)
            .FirstOrDefaultAsync(job => job.Id == id);
    }

    public async Task AddAsync(ScheduledReportJob entity)
    {
        dbContext.ScheduledReportJobs.Add(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(ScheduledReportJob entity)
    {
        dbContext.ScheduledReportJobs.Update(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(ScheduledReportJob entity)
    {
        dbContext.ScheduledReportJobs.Remove(entity);
        await dbContext.SaveChangesAsync();
    }
}
