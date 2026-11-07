using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pragma.Application.Common;
using Pragma.Application.Contracts;
using Pragma.Application.DTOs.Modules;
using Pragma.Domain.Entities;

namespace Pragma.Application.Services;

public class ModuleService : IModuleService
{
    private readonly IAppDbContext _context;
    private readonly ICourseAccessService _accessService;
    private readonly IValidator<CreateModuleDto> _createValidator;

    public ModuleService(
        IAppDbContext context,
        ICourseAccessService accessService,
        IValidator<CreateModuleDto> createValidator)
    {
        _context = context;
        _accessService = accessService;
        _createValidator = createValidator;
    }

    public async Task<Result<Guid>> CreateModuleAsync(Guid courseId, CreateModuleDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<Guid>.Failure(Error.Validation("Module.Validation", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var courseExists = await _context.Courses.AnyAsync(c => c.Id == courseId, cancellationToken);
        if (!courseExists)
        {
            return Result<Guid>.Failure(Error.NotFound("Course.NotFound", "Kurs tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(courseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result<Guid>.Failure(Error.Forbidden("Course.Forbidden", "Bu kursa modul əlavə etmək icazəniz yoxdur."));
        }

        var module = new Module
        {
            CourseId = courseId,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            OrderIndex = dto.OrderIndex
        };

        _context.Modules.Add(module);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(module.Id);
    }

    public async Task<Result> UpdateModuleAsync(Guid moduleId, UpdateModuleDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var module = await _context.Modules.FirstOrDefaultAsync(m => m.Id == moduleId, cancellationToken);
        if (module == null)
        {
            return Result.Failure(Error.NotFound("Module.NotFound", "Modul tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(module.CourseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu modulu redaktə etmək icazəniz yoxdur."));
        }

        module.Title = dto.Title.Trim();
        module.Description = dto.Description;
        module.OrderIndex = dto.OrderIndex;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteModuleAsync(Guid moduleId, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var module = await _context.Modules.FirstOrDefaultAsync(m => m.Id == moduleId, cancellationToken);
        if (module == null)
        {
            return Result.Failure(Error.NotFound("Module.NotFound", "Modul tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(module.CourseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu modulu silmək icazəniz yoxdur."));
        }

        module.MarkDeleted();
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReorderLessonsAsync(Guid moduleId, ReorderItemsRequestDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var module = await _context.Modules.FirstOrDefaultAsync(m => m.Id == moduleId, cancellationToken);
        if (module == null)
        {
            return Result.Failure(Error.NotFound("Module.NotFound", "Modul tapılmadı."));
        }

        var canManage = await _accessService.CanManageCourseAsync(module.CourseId, userId, isAdmin, cancellationToken);
        if (!canManage)
        {
            return Result.Failure(Error.Forbidden("Course.Forbidden", "Bu modulun dərslərini nizamlamaq icazəniz yoxdur."));
        }

        var lessons = await _context.Lessons.Where(l => l.ModuleId == moduleId).ToListAsync(cancellationToken);
        foreach (var item in dto.Items)
        {
            var lesson = lessons.FirstOrDefault(l => l.Id == item.Id);
            if (lesson != null)
            {
                lesson.OrderIndex = item.OrderIndex;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
