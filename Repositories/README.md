# Repositorios - Patrón Repository

Sistema de repositorios para acceso a datos de PocketBase con arquitectura limpia y centralizada.

## Estructura

```
Repositories/
├── BaseRepository.cs          # Clase base con CRUD común
├── RepositoryConfig.cs        # Configuración centralizada
├── CollectionNameAttribute.cs # Atributo para nombres de colección
├── IRepository.cs            # Interface genérica
├── VideoRepo.cs              # Repositorio de videos
├── CommentRepo.cs            # Repositorio de comentarios
├── ResponseRepo.cs           # Repositorio de respuestas
├── BotAccountRepo.cs         # Repositorio de cuentas bot
└── AccountToFollowRepo.cs    # Repositorio de cuentas a seguir
```

## Uso Básico

### 1. Configuración (automática en Program.cs)

```csharp
// Se configura automáticamente al iniciar la aplicación
RepositoryConfig.PocketBaseUrl = "http://127.0.0.1:8090";
```

### 2. Inyección de Dependencias

```csharp
public class MiServicio
{
    private readonly IVideoRepo _videoRepo;
    private readonly ICommentRepo _commentRepo;

    public MiServicio(IVideoRepo videoRepo, ICommentRepo commentRepo)
    {
        _videoRepo = videoRepo;
        _commentRepo = commentRepo;
    }
}
```

### 3. Operaciones CRUD

```csharp
// Crear
var video = new VideoRecord { TiktokVideoId = "123", ... };
await _videoRepo.CreateAsync(video);

// Leer por ID
var video = await _videoRepo.GetByIdAsync("abc123");

// Leer todos
var videos = await _videoRepo.GetAllAsync();

// Actualizar
video.Status = "completed";
await _videoRepo.UpdateAsync(video.Id, video);

// Eliminar
await _videoRepo.DeleteAsync(video.Id);
```

### 4. Consultas Específicas

Cada repositorio tiene métodos específicos para su entidad:

```csharp
// VideoRepo
var pendingVideos = await _videoRepo.GetPendingVideosAsync();
var userVideos = await _videoRepo.GetByAccountUsernameAsync("usuario");
var exists = await _videoRepo.ExistsByTikTokIdAsync("123456");

// CommentRepo
var comments = await _commentRepo.GetByVideoIdAsync("video_id");
var exists = await _commentRepo.ExistsByCommentIdAsync("comment_id");

// ResponseRepo
var responses = await _responseRepo.GetByVideoIdAsync("video_id");
var unposted = await _responseRepo.GetUnpostedAsync();
await _responseRepo.MarkAsPostedAsync("response_id");

// BotAccountRepo
var activeAccounts = await _botAccountRepo.GetActiveAccountsAsync();
var account = await _botAccountRepo.GetByUsernameAsync("bot_user");

// AccountToFollowRepo
var account = await _accountToFollowRepo.GetByUsernameAsync("tiktok_user");
```

## Crear Nuevo Repositorio

### 1. Crear Interface

```csharp
public interface IMiNuevoRepo : IRepository<MiModelo>
{
    Task<List<MiModelo>> GetByCustomFieldAsync(string value);
}
```

### 2. Crear Implementación

```csharp
[CollectionName("mi_coleccion")]
public class MiNuevoRepo : BaseRepository<MiModelo>, IMiNuevoRepo
{
    public async Task<List<MiModelo>> GetByCustomFieldAsync(string value) =>
        await GetByFilterAsync($"custom_field='{value}'");
}
```

### 3. Registrar en Program.cs

```csharp
services.AddSingleton<IMiNuevoRepo, MiNuevoRepo>();
```

## Métodos Protegidos de BaseRepository

Disponibles para usar en repositorios hijos:

```csharp
// Filtrar por condición
protected async Task<List<T>> GetByFilterAsync(string filter)

// Verificar existencia por condición
protected async Task<bool> ExistsByFilterAsync(string filter)
```

**Ejemplos de filtros:**
```csharp
// Igualdad
await GetByFilterAsync("status='pending'");

// Comparación
await GetByFilterAsync("created>'2024-01-01'");

// AND
await GetByFilterAsync("status='pending' && priority>5");

// OR
await GetByFilterAsync("status='pending' || status='processing'");
```

## Ventajas del Patrón

✓ **Separación de responsabilidades** - Cada repositorio maneja su entidad
✓ **Código reutilizable** - CRUD común en BaseRepository
✓ **Fácil testing** - Interfaces permiten mocks
✓ **Configuración centralizada** - URL en un solo lugar
✓ **Sin constructores repetitivos** - Atributo `[CollectionName]` automático
✓ **Type-safe** - Genéricos garantizan tipos correctos

## Migración desde IPocketBaseClient

La capa `IPocketBaseClient` fue eliminada. Usa repositorios directamente:

**Antes:**
```csharp
var videos = await _dbClient.GetPendingVideosAsync();
```

**Después:**
```csharp
var videos = await _videoRepo.GetPendingVideosAsync();
```
