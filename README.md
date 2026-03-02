# Video Processing System V2

Sistema independiente de procesamiento de videos para extracción de contenido de TikTok, transcripción y generación de respuestas.

## Estructura del Proyecto

```
ProcesamientoDePublicaciones/
├── Configuration/      # Gestión de configuración
├── Repositories/       # Repositorios de acceso a datos (PocketBase.Framework)
├── Extraction/         # Servicios de extracción de videos
├── Processing/         # Pipeline de procesamiento de videos
├── SystemTray/         # Interfaz de bandeja del sistema
├── Models/             # Modelos de datos
├── SeedData/           # Datos semilla para restauración opcional
└── Services/           # Servicios compartidos
```

## Arquitectura de Acceso a Datos

- La capa legacy `Database/` fue eliminada.
- El acceso a datos ahora usa interfaces e implementaciones de repositorio en `Repositories/`.
- La configuración de inyección de dependencias de repositorios se define en `Program.cs` usando `PocketBase.Framework`.

## Dependencias

- .NET 10.0
- Serilog 4.3.1
- Serilog.Sinks.File 7.0.0
- Newtonsoft.Json 13.0.4
- System.Net.Http.Json 10.0.3

## Configuración

Copia `Configuration/settings.template.json` a `C:\Users\jorge\AppData\Roaming\TikTokManager\settings.json` y actualízalo con tus valores.

## Dependencias Externas

- **yt-dlp**: Para extracción de metadata de videos y descarga directa de audio MP3
- **PocketBase**: Servidor de base de datos
- **AssemblyAI**: Servicio de transcripción (requiere API key)

Nota: FFmpeg NO es requerido; yt-dlp gestiona la extracción de audio internamente.

## Logs

Los logs se almacenan en: `C:\Users\jorge\AppData\Local\TikTokManager\logs\`
- Rotación diaria
- Retención de 30 días
- Formato JSON estructurado

## Compilación

```bash
dotnet build
```

## Ejecución

```bash
dotnet run
```
