namespace GestorOT.Client.Services;

/// <summary>
/// Guarda los filtros de cada pantalla mientras dura la sesión: al volver a la página
/// se encuentran como se dejaron. Se pierde al recargar el navegador, a propósito.
/// Lo usan RemoteTable y LocalTable (StateKey) para su TableQuery.
/// </summary>
public sealed class FilterState
{
    private readonly Dictionary<string, object> _byPage = new();

    public T For<T>(string page) where T : new()
    {
        if (_byPage.TryGetValue(page, out var existing) && existing is T typed)
            return typed;
        var created = new T();
        _byPage[page] = created;
        return created;
    }
}
