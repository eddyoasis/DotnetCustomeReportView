using DataWarehousePower.Data;
using DataWarehousePower.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWarehousePower.Repositories
{
    public class DataFileColumnRepository : IDataFileColumnRepository
    {
        private readonly AppDbContext _context;

        public DataFileColumnRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<DataFileColumn>> GetAllAsync()
            => await _context.DataFileColumns
                .AsNoTracking()
                .Include(column => column.DataFile)
                .OrderBy(column => column.DataFileDefinitionId)
                .ThenBy(column => column.DisplayOrder)
                .ThenBy(column => column.Id)
                .ToListAsync();

        public async Task<List<DataFileColumn>> GetByDataFileDefinitionIdAsync(int DataFileDefinitionId)
            => await _context.DataFileColumns
                .AsNoTracking()
                .Where(column => column.DataFileDefinitionId == DataFileDefinitionId)
                .Include(column => column.DataFile)
                .OrderBy(column => column.DisplayOrder)
                .ThenBy(column => column.Id)
                .ToListAsync();

        public async Task<DataFileColumn?> GetByIdAsync(int id)
            => await _context.DataFileColumns
                .AsNoTracking()
                .Include(column => column.DataFile)
                .FirstOrDefaultAsync(column => column.Id == id);

        public async Task<bool> DataFileDefinitionExistsAsync(int DataFileDefinitionId)
            => await _context.DataFileColumns
                .AsNoTracking()
                .AnyAsync(DataFile => DataFile.Id == DataFileDefinitionId);

        public async Task<DataFileColumn> CreateAsync(DataFileColumn dataFileColumn)
        {
            _context.DataFileColumns.Add(dataFileColumn);
            await _context.SaveChangesAsync();
            return dataFileColumn;
        }

        public async Task UpdateAsync(DataFileColumn dataFileColumnRequest)
        {
            DataFileColumn? dataFileColumn = await _context.DataFileColumns
                .FirstOrDefaultAsync(column => column.Id == dataFileColumnRequest.Id);

            if (dataFileColumn is null)
            {
                return;
            }

            dataFileColumn.DataFileDefinitionId = dataFileColumnRequest.DataFileDefinitionId;
            dataFileColumn.PropertyName = dataFileColumnRequest.PropertyName;
            dataFileColumn.DefaultLabel = dataFileColumnRequest.DefaultLabel;
            dataFileColumn.MappingParameter = dataFileColumnRequest.MappingParameter;
            dataFileColumn.MappingParameterFilter = dataFileColumnRequest.MappingParameterFilter;
            dataFileColumn.DisplayOrder = dataFileColumnRequest.DisplayOrder;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            DataFileColumn? dataFileColumn = await _context.DataFileColumns
                .FirstOrDefaultAsync(column => column.Id == id);

            if (dataFileColumn is null)
            {
                return;
            }

            _context.DataFileColumns.Remove(dataFileColumn);
            await _context.SaveChangesAsync();
        }
    }
}