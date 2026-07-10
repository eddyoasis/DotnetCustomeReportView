using DataWarehousePower.Models;
using DataWarehousePower.Repositories;

namespace DataWarehousePower.Services
{
    public class DWSchemeService : IDWSchemeService
    {
        private readonly IDWSchemeRepository _repository;

        public DWSchemeService(IDWSchemeRepository repository)
        {
            _repository = repository;
        }

        public Task<List<DWScheme>> GetAllAsync()
            => _repository.GetAllAsync();

        public Task<DWScheme?> GetByIdAsync(int id)
            => _repository.GetByIdAsync(id);

        public Task<DWScheme> CreateAsync(DWScheme dwScheme)
        {
            Normalize(dwScheme);
            return _repository.CreateAsync(dwScheme);
        }

        public Task UpdateAsync(DWScheme dwScheme)
        {
            Normalize(dwScheme);
            return _repository.UpdateAsync(dwScheme);
        }

        public Task DeleteAsync(int id)
            => _repository.DeleteAsync(id);

        private static void Normalize(DWScheme dwScheme)
        {
            dwScheme.SP = dwScheme.SP.Trim();
            dwScheme.Display = dwScheme.Display.Trim();
            dwScheme.FilterDateColumnName = dwScheme.FilterDateColumnName.Trim();
        }
    }
}
