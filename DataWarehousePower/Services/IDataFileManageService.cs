using DataWarehousePower.Models;

namespace DataWarehousePower.Services
{
    public interface IDataFileManageService
    {
        Task<List<string>> GetSourceDatabaseAsync(string dbConnectionString);
        Task<List<string>> GetSourceDatabaseOptionsAsync(string dbConnectionString);
        Task<List<string>> GetSourceDatabaseOptionsAsync();
        Task<List<string>> GetSourceTableOptionsAsync(string dbConnectionString, string? sourceDatabase);
        Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceStoredProcedureOptionsAsync(string dbConnectionString, string? sourceDatabase);
        Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<List<SourceColumnMetadata>> GetSourceColumnMetadataAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<List<string>> GetDistinctColumnValuesAsync(string? userId, string? sourceDatabase, string? sourceTable, string? sourceSP, string? columnName, string? search = null, int take = 50);
        Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request, DateTime? dateFrom = null, DateTime? dateTo = null);
        Task<List<string>> GetSourceParametersAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<List<DataFileColumn>> SyncDataFileColumnsAsync(int dataFileDefinitionId, IReadOnlyList<SourceColumnMetadata> sourceColumns);
        Task<DataFileManageListViewModel> GetListViewModelAsync(DataFileManageFilterViewModel? filter = null);
        Task<DataFileManageListViewModel> GetListViewModelAsync(string userId, DataFileManageFilterViewModel? filter = null);
        Task<DataFileManageListViewModel> GetListViewModelAsync(string userId, string userDepartment, DataFileManageFilterViewModel? filter = null);
        Task<DataFileManageFormViewModel> GetFormViewModelAsync(int id);
        Task<DataFileDefinition> CreateDataFileAsync(DataFileManageFormViewModel form);
        Task UpdateDataFileAsync(DataFileManageFormViewModel form);
        Task DeleteAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
