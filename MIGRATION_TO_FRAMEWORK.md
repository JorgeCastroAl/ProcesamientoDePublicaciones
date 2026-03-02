# Migración a PocketBase.Framework

## Cambios Realizados

### 1. Framework Creado
Se creó un proyecto independiente `PocketBase.Framework` con:
- Patrón Repository genérico
- Sincronización automática de esquemas
- Configuración centralizada tipo Entity Framework
- Atributos reutilizables

### 2. Modelos Actualizados
Todos los modelos ahora usan atributos del framework:

```csharp
// Antes
using VideoProcessingSystemV2.Database.Attributes;

// Después
using PocketBase.Framework.Attributes;
```

### 3. Repositorios Actualizados
Los repositorios ahora heredan del framework y reciben `PocketBaseOptions`:

```csharp
// Antes
[CollectionName("video")]
public class VideoRepo : BaseRepository<VideoRecord>
{
    public VideoRepo() : base("video") { }
}

// Después
[CollectionName("video")]
public class VideoRepo : BaseRepository<VideoRecord>
{
    public VideoRepo(PocketBaseOptions options) : base(options) { }
}
```

### 4. Configuración en Program.cs
Ahora se usa `PocketBaseOptions` para configurar:

```csharp
var pbOptions = new PocketBaseOptions
{
    Url = "http://127.0.0.1:8090",
    AdminEmail = "jorge777_21@hotmail.com",
    AdminPassword = "PocketKarlos555",
    EnableAutoSync = true,
    TimeoutSeconds = 30
};
```

### 5. Sincronización de Esquemas
Usa el nuevo servicio del framework:

```csharp
var syncService = new SchemaSyncService(pbOptions);
await syncService.SyncAsync(Assembly.GetExecutingAssembly());
```

## Archivos Eliminados

Los siguientes archivos locales fueron eliminados (ahora están en el framework):
- `VideoProcessingSystemV2/Repositories/*Repo.cs`
- `VideoProcessingSystemV2/Repositories/I*Repo.cs`
- `VideoProcessingSystemV2/Database/Attributes/` (carpeta completa)
- `VideoProcessingSystemV2/Database/SchemaSync/` (carpeta completa)

## Ventajas de la Migración

✓ **Reutilizable** - El framework puede usarse en otros proyectos
✓ **Mantenible** - Actualizaciones centralizadas en el framework
✓ **Configurable** - Configuración tipo EF Core con `PocketBaseOptions`
✓ **Versionable** - Framework independiente con su propio versionado
✓ **Testeable** - Framework separado facilita unit testing
✓ **Profesional** - Arquitectura limpia y escalable

## Uso en Otros Proyectos

Para usar el framework en un nuevo proyecto:

```bash
# 1. Agregar referencia
dotnet add reference ../PocketBase.Framework/PocketBase.Framework.csproj

# 2. Configurar
var options = new PocketBaseOptions
{
    Url = "http://127.0.0.1:8090",
    AdminEmail = "admin@example.com",
    AdminPassword = "password"
};

# 3. Crear repositorios
[CollectionName("users")]
public class UserRepo : BaseRepository<User>
{
    public UserRepo(PocketBaseOptions options) : base(options) { }
}

# 4. Usar
var repo = new UserRepo(options);
var users = await repo.GetAllAsync();
```

## Configuración desde appsettings.json (Futuro)

Puedes configurar desde archivo JSON:

```json
{
  "PocketBase": {
    "Url": "http://127.0.0.1:8090",
    "AdminEmail": "admin@example.com",
    "AdminPassword": "password",
    "EnableAutoSync": true,
    "TimeoutSeconds": 30
  }
}
```

```csharp
var pbOptions = configuration.GetSection("PocketBase").Get<PocketBaseOptions>();
```

## Compatibilidad

La capa legacy fue retirada:
- `IPocketBaseClient` eliminado del proyecto
- Servicios migrados a repositorios (`IVideoRepo`, `ICommentRepo`, `IResponseRepo`, `IAccountToFollowRepo`, `IBotAccountRepo`)
- DI actualizado para usar únicamente `PocketBase.Framework`

## Próximos Pasos

1. ✓ Framework creado y compilado
2. ✓ Proyecto migrado al framework
3. ✓ Compilación exitosa
4. ⏳ Probar la aplicación
5. ⏳ Documentar casos de uso adicionales
6. ⏳ Crear NuGet package (opcional)
