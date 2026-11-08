using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace ProsegurGDT.Web.Services;

public class AdaptadorValidacionEspanol : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _base = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
    {
        if (attribute.ErrorMessage is null && attribute.ErrorMessageResourceName is null)
        {
            attribute.ErrorMessage = attribute switch
            {
                RequiredAttribute => "El campo {0} es obligatorio.",
                EmailAddressAttribute => "El campo {0} no es un correo electrónico válido.",
                StringLengthAttribute s when s.MinimumLength > 0 => "El campo {0} debe tener entre {2} y {1} caracteres.",
                StringLengthAttribute => "El campo {0} admite como máximo {1} caracteres.",
                MaxLengthAttribute => "El campo {0} admite como máximo {1} caracteres.",
                MinLengthAttribute => "El campo {0} requiere al menos {1} caracteres.",
                RangeAttribute => "El campo {0} debe estar entre {1} y {2}.",
                CompareAttribute => "Los campos {0} y {1} no coinciden.",
                RegularExpressionAttribute => "El campo {0} no tiene un formato válido.",
                PhoneAttribute => "El campo {0} no es un teléfono válido.",
                _ => null
            };
        }
        return _base.GetAttributeAdapter(attribute, stringLocalizer);
    }
}

public class IdentityErrorDescriberEspanol : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = "Ocurrió un error desconocido." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe tener al menos un carácter especial (por ejemplo ! # $ %)." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe tener al menos un número (0-9)." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe tener al menos una letra minúscula (a-z)." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe tener al menos una letra mayúscula (A-Z)." };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"La contraseña debe tener al menos {uniqueChars} caracteres distintos." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "La contraseña actual es incorrecta." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"El usuario '{userName}' ya está registrado." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = $"El correo '{email}' ya está registrado." };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = $"El usuario '{userName}' no es válido." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = $"El correo '{email}' no es válido." };
}