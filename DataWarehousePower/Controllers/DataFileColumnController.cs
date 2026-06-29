using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataWarehousePower.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = DepartmentAuthorizationPolicies.ReportManageAccess)]
    public class DataFileColumnController : ControllerBase
    {
        private readonly IDataFileColumnService _service;
        private readonly ILogger<DataFileColumnController> _logger;

        public DataFileColumnController(
            IDataFileColumnService service,
            ILogger<DataFileColumnController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<DataFileColumn>>> GetAll([FromQuery] int? dataFileDefinitionId = null)
        {
            if (dataFileDefinitionId.HasValue)
            {
                return Ok(await _service.GetByDataFileDefinitionIdAsync(dataFileDefinitionId.Value));
            }

            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<DataFileColumn>> GetById(int id)
        {
            DataFileColumn? dataFileColumn = await _service.GetByIdAsync(id);
            if (dataFileColumn is null)
            {
                return NotFound();
            }

            return Ok(dataFileColumn);
        }

        [HttpPost]
        public async Task<ActionResult<DataFileColumn>> Create([FromBody] DataFileColumn dataFileColumn)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                DataFileColumn created = await _service.CreateAsync(dataFileColumn);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating data file column");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the data file column.");
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] DataFileColumn dataFileColumn)
        {
            if (id != dataFileColumn.Id)
            {
                return BadRequest("Route id does not match payload id.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            DataFileColumn? existing = await _service.GetByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }

            try
            {
                await _service.UpdateAsync(dataFileColumn);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data file column {DataFileColumnId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the data file column.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            DataFileColumn? existing = await _service.GetByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }

            try
            {
                await _service.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data file column {DataFileColumnId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the data file column.");
            }
        }
    }
}