using FluentValidation;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Validation;

public sealed class CreateStorageConfigRequestValidator : AbstractValidator<CreateStorageConfigRequest>
{
    public CreateStorageConfigRequestValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(200);

        When(x => x.Proveedor == StorageProviderType.S3Compatible, () =>
        {
            RuleFor(x => x.Endpoint).NotEmpty().WithMessage("El endpoint es obligatorio para un almacenamiento S3-compatible.");
            RuleFor(x => x.BucketName).NotEmpty().WithMessage("El bucket es obligatorio para un almacenamiento S3-compatible.");
            RuleFor(x => x.AccessKey).NotEmpty().WithMessage("La access key es obligatoria para un almacenamiento S3-compatible.");
            RuleFor(x => x.SecretKey).NotEmpty().WithMessage("La secret key es obligatoria para un almacenamiento S3-compatible.");
        });

        When(x => x.Proveedor == StorageProviderType.Local, () =>
        {
            RuleFor(x => x.LocalPath).NotEmpty().WithMessage("La ruta local es obligatoria para un almacenamiento local.");
        });
    }
}
