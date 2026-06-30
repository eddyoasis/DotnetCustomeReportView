using DataWarehousePower.Models;

namespace DataWarehousePower.Repositories
{
    public interface IDataFileManageRepository
    {
        Task<List<string>> GetSourceDatabaseOptionsAsync();
        Task<List<string>> GetSourceTableOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceStoredProcedureOptionsAsync(string? sourceDatabase);
        Task<List<string>> GetSourceColumnsAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<List<SourceColumnMetadata>> GetSourceColumnMetadataAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<DataFilePreviewResult> GetPreviewDataAsync(DataFilePreviewRequest request);
        Task<List<string>> GetSourceParametersAsync(string? sourceDatabase, string? sourceTable, string? sourceSP);
        Task<List<DataFileDefinition>> GetAllWithColumnsAsync();
        Task<DataFileDefinition?> GetByIdWithColumnsAsync(int id);
        Task<List<DataFileDefinition>> GetAllWithColumnsAsync(string userId, string userDepartment);
        Task<DataFileDefinition> CreateAsync(DataFileDefinition dataFileDefinition, IEnumerable<DataFileColumn> columns);
        Task UpdateAsync(DataFileDefinition dataFileDefinition, IEnumerable<DataFileColumn> columns, IEnumerable<int> deletedColumnIds);
        Task DeleteAsync(int id);
        Task ToggleActiveAsync(int id);
    }
}
