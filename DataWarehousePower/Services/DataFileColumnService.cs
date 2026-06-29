using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DataFileColumnService : IDataFileColumnService
    {
        private readonly IDataFileColumnRepository _repository;

        public DataFileColumnService(IDataFileColumnRepository repository)
        {
            _repository = repository;
        }

        public Task<List<DataFileColumn>> GetAllAsync()
            => _repository.GetAllAsync();

        public Task<List<DataFileColumn>> GetByDataFileDefinitionIdAsync(int dataFileDefinitionId)
            => _repository.GetByDataFileDefinitionIdAsync(dataFileDefinitionId);

        public Task<DataFileColumn?> GetByIdAsync(int id)
            => _repository.GetByIdAsync(id);

        public async Task<DataFileColumn> CreateAsync(DataFileColumn dataFileColumn)
        {
            await ValidateDataFileDefinitionAsync(dataFileColumn.DataFileDefinitionId);
            Normalize(dataFileColumn);
            return await _repository.CreateAsync(dataFileColumn);
        }

        public async Task UpdateAsync(DataFileColumn dataFileColumn)
        {
            await ValidateDataFileDefinitionAsync(dataFileColumn.DataFileDefinitionId);
            Normalize(dataFileColumn);
            await _repository.UpdateAsync(dataFileColumn);
        }

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);

        private async Task ValidateDataFileDefinitionAsync(int dataFileDefinitionId)
        {
            if (dataFileDefinitionId <= 0)
            {
                throw new InvalidOperationException("DataFileDefinitionId must be greater than zero.");
            }

            bool exists = await _repository.DataFileDefinitionExistsAsync(dataFileDefinitionId);
            if (!exists)
            {
                throw new InvalidOperationException($"DataFile definition {dataFileDefinitionId} was not found.");
            }
        }

        private static void Normalize(DataFileColumn dataFileColumn)
        {
            dataFileColumn.PropertyName = dataFileColumn.PropertyName.Trim();
            dataFileColumn.DefaultLabel = dataFileColumn.DefaultLabel.Trim();
            dataFileColumn.MappingParameter = string.IsNullOrWhiteSpace(dataFileColumn.MappingParameter)
                ? null
                : dataFileColumn.MappingParameter.Trim();
        }
    }
}