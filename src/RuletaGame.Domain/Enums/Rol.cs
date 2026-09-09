namespace RuletaGame.Domain.Enums;

public static class Rol
{
    public const string SuperAdmin = "SUPERADMIN";
    public const string Admin      = "ADMIN";
    public const string Operador   = "OPERADOR";

    public static readonly string[] Todos = [SuperAdmin, Admin, Operador];

    public static bool EsValido(string? rol) => rol is not null && Todos.Contains(rol);
}
