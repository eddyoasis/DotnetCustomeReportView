using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DataFileManageService : IDataFileManageService
    {
        private readonly IDataFileManageRepository _repository;

        public DataFileManageService(IDataFileManageRepository repository)
        {
            _repository = repository;
        }

        public async Task<DataFileManageListViewModel> GetListViewModelAsync(string userId, string userDepartment, DataFileManageFilterViewModel? filter = null)
        {
            var dataFiles = await _repository.GetAllWithColumnsAsync(userId, userDepartment);
            filter ??= new DataFileManageFilterViewModel();

            string? search = string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : filter.Search.Trim();

            if (!string.IsNullOrWhiteSpace(search))
            {
                dataFiles = dataFiles.Where(dataFile =>
                        ContainsIgnoreCase(dataFile.DataFileName, search) ||
                        ContainsIgnoreCase(dataFile.SourceDatabase, search) ||
                        ContainsIgnoreCase(dataFile.SourceTable, search) ||
                        ContainsIgnoreCase(dataFile.SourceSP, search) ||
                        ContainsIgnoreCase(dataFile.Departments, search) ||
                        dataFile.Columns.Any(column =>
                            ContainsIgnoreCase(column.PropertyName, search) ||
                            ContainsIgnoreCase(column.DefaultLabel, search)))
                    .ToList();
            }

            if (filter.IsActive.HasValue)
            {
                dataFiles = dataFiles.Where(dataFile => dataFile.IsActive == filter.IsActive.Value).ToList();
            }

            return new DataFileManageListViewModel
            {
                Filter = filter,
                DataFiles = dataFiles
            };
        }

        public async Task<DataFileManageListViewModel> GetListViewModelAsync(DataFileManageFilterViewModel? filter = null)
        {
            var dataFiles = await _repository.GetAllWithColumnsAsync();
            filter ??= new DataFileManageFilterViewModel();

            string? search = string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : filter.Search.Trim();

            if (!string.IsNullOrWhiteSpace(search))
            {
                dataFiles = dataFiles.Where(dataFile =>
                        ContainsIgnoreCase(dataFile.DataFileName, search) ||
                        ContainsIgnoreCase(dataFile.SourceDatabase, search) ||
                        ContainsIgnoreCase(dataFile.SourceTable, search) ||
                        ContainsIgnoreCase(dataFile.SourceSP, search) ||
                        ContainsIgnoreCase(dataFile.Departments, search) ||
                        dataFile.Columns.Any(column =>
                            ContainsIgnoreCase(column.PropertyName, search) ||
                            ContainsIgnoreCase(column.DefaultLabel, search)))
                    .ToList();
            }

            if (filter.IsActive.HasValue)
            {
                dataFiles = dataFiles.Where(dataFile => dataFile.IsActive == filter.IsActive.Value).ToList();
            }

            return new DataFileManageListViewModel
            {
                Filter = filter,
                DataFiles = dataFiles
            };
        }

        public Task<List<string>> GetSourceDatabaseAsync(string dbConnectionString)
            => _repository.GetSourceDatabaseAsync(dbConnectionString);

        public Task<List<string>> GetSourceDatabaseOptionsAsync(string dbConnectionString)
            => _repository.GetSourceDatabaseOptionsAsync(dbConnectionString);

        public Task<List<string>> GetSourceDatabaseOptionsAsync()
            => _repository.GetSourceDatabaseOptionsAsync();

        public Task<List<string>> GetSourceTableOptionsAsync(string dbConnectionString, string? sourceDatabase)
            => _repository.GetSourceTableOptionsAsync(dbConnectionString, sourceDatabase);

        public Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase)
            => _repository.GetSourceTableOptionsAsync(sourceDatabase);

        public Task<List<string>> GetSourceStoredProcedureOptionsAsync(string dbConnectionString, string? sourceDatabase)
            => _repository.GetSourceStoredProcedureOptionsAsync(dbConnectionString, sourceDatabase);

        public Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase)
            => _repository.GetSourceStoredProcedureOptionsAsync(sourceDatabase);

        public Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
            => _repository.GetSourceColumnsAsync(sourceDatabase, sourceTable, sourceSP);

        public Task<List<SourceColumnMetadata>> GetSourceColumnMetadataAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
            => _repository.GetSourceColumnMetadataAsync(sourceDatabase, sourceTable, sourceSP);

        public Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request, DateTime? dateFrom = null, DateTime? dateTo = null)
            => _repository.GetPreviewDataAsync(request, dateFrom ?? request.DateFrom, dateTo ?? request.DateTo);

        public Task<List<string>> GetSourceParametersAsync(string? sourceDatabase, string? sourceTable, string? sourceSP)
            => _repository.GetSourceParametersAsync(sourceDatabase, sourceTable, sourceSP);

        public async Task<DataFileManageFormViewModel> GetFormViewModelAsync(int id)
        {
            DataFileDefinition? dataFile = await _repository.GetByIdWithColumnsAsync(id);
            if (dataFile is null)
            {
                throw new InvalidOperationException($"Data file {id} not found.");
            }

            return MapToForm(dataFile);
        }

        public async Task<DataFileDefinition> CreateDataFileAsync(DataFileManageFormViewModel form)
        {
            var (dataFile, columns, _) = MapFromForm(form);
            return await _repository.CreateAsync(dataFile, columns);
        }

        public async Task UpdateDataFileAsync(DataFileManageFormViewModel form)
        {
            var (dataFile, columns, deletedIds) = MapFromForm(form);
            await _repository.UpdateAsync(dataFile, columns, deletedIds);
        }

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);

        public Task ToggleActiveAsync(int id)
            => _repository.ToggleActiveAsync(id);

        private static bool ContainsIgnoreCase(string? value, string search)
            => !string.IsNullOrWhiteSpace(value) &&
               value.Contains(search, StringComparison.OrdinalIgnoreCase);

        private static DataFileManageFormViewModel MapToForm(DataFileDefinition dataFile)
            => new()
            {
                Id = dataFile.Id,
                DataFileName = dataFile.DataFileName,
                SourceDatabase = dataFile.SourceDatabase,
                SourceTable = dataFile.SourceTable,
                SourceSP = dataFile.SourceSP,
                Parameters = dataFile.Parameters,
                IsActive = dataFile.IsActive,
                Departments = dataFile.Departments,
                Columns = dataFile.Columns.Select(column => new DataFileColumnFormModel
                {
                    Id = column.Id,
                    PropertyName = column.PropertyName,
                    DefaultLabel = column.DefaultLabel,
                    MappingParameter = column.MappingParameter,
                    MappingParameterFilter = column.MappingParameterFilter,
                    DisplayOrder = column.DisplayOrder
                }).ToList()
            };

        private static (DataFileDefinition dataFile, List<DataFileColumn> columns, List<int> deletedIds) MapFromForm(DataFileManageFormViewModel form)
        {
            var dataFile = new DataFileDefinition
            {
                Id = form.Id,
                DataFileName = form.DataFileName.Trim(),
                SourceDatabase = string.IsNullOrWhiteSpace(form.SourceDatabase) ? null : form.SourceDatabase.Trim(),
                SourceTable = string.IsNullOrWhiteSpace(form.SourceTable) ? null : form.SourceTable.Trim(),
                SourceSP = string.IsNullOrWhiteSpace(form.SourceSP) ? null : form.SourceSP.Trim(),
                Parameters = string.IsNullOrWhiteSpace(form.Parameters) ? null : form.Parameters.Trim(),
                IsActive = form.IsActive,
                Departments = string.IsNullOrWhiteSpace(form.Departments) ? null : form.Departments.Trim(),
                UserId = string.IsNullOrWhiteSpace(form.UserId) ? null : form.UserId.Trim(),
                CreatedBy = string.IsNullOrWhiteSpace(form.UserId) ? null : form.UserId.Trim(),
                CreatedAt = DateTimeHelper.GetCurrentLocalTime(),
                ModifiedBy = string.IsNullOrWhiteSpace(form.UserId) ? null : form.UserId.Trim(),
                ModifiedAt = DateTimeHelper.GetCurrentLocalTime(),
            };

            var activeColumns = form.Columns
                .Where(column => !column.IsDeleted)
                .Select(column => new DataFileColumn
                {
                    Id = column.Id,
                    PropertyName = column.PropertyName.Trim(),
                    DefaultLabel = column.DefaultLabel.Trim(),
                    MappingParameter = string.IsNullOrWhiteSpace(column.MappingParameter) ? null : column.MappingParameter.Trim(),
                    MappingParameterFilter = string.IsNullOrWhiteSpace(column.MappingParameterFilter) ? null : column.MappingParameterFilter.Trim(),
                    DisplayOrder = column.DisplayOrder
                })
                .ToList();

            var deletedIds = form.Columns
                .Where(column => column.IsDeleted && column.Id > 0)
                .Select(column => column.Id)
                .ToList();

            return (dataFile, activeColumns, deletedIds);
        }
    }
}
