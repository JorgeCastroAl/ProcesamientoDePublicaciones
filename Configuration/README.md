# Configuración Centralizada

Toda la configuración de la aplicación se maneja a través de un único archivo JSON ubicado en:

```
<carpeta_del_ejecutable>\settings.json
```

Ubicación efectiva por entorno:
- Desarrollo: `<carpeta_del_ejecutable>\settings.json`
- Producción (instalada):

```
%LocalAppData%\TikTokManager\settings.json
```

## Archivo de Plantilla

Usa `settings.example.json` en la raíz del proyecto como plantilla para crear tu archivo de configuración.

## Estructura del Archivo de Configuración

```json
{
  "pocketbase_url": "http://127.0.0.1:8090",
  "pocketbase_admin_email": "jorge777_21@hotmail.com",
  "pocketbase_admin_password": "PocketKarlos555",
  "assemblyai_api_key": "YOUR_ASSEMBLYAI_API_KEY_HERE",
  "response_api_url": "http://localhost:9090/api/opinion/generate-comment",
  "modify_comment_api_url": "http://localhost:9090/api/opinion/modify-comment",
  "ffmpeg_path": "ffmpeg.exe",
  "extraction_interval_minutes": 60,
  "processing_retry_count": 3,
  "processing_poll_interval_seconds": 30,
  "temp_directory": "C:\\Users\\YOUR_USERNAME\\AppData\\Local\\TikTokManager\\temp",
  "comments_extraction_limit": 12,
  "skip_transcription": false
}
```

## Configuraciones Disponibles

### PocketBase
- **pocketbase_url**: URL del servidor PocketBase (default: `http://127.0.0.1:8090`)
- **pocketbase_admin_email**: Email del administrador para sincronización de esquemas
- **pocketbase_admin_password**: Contraseña del administrador

### APIs Externas
- **assemblyai_api_key**: API key para el servicio de transcripción AssemblyAI
- **response_api_url**: URL del servicio de generación de respuestas/opiniones (default: `http://localhost:9090/api/opinion/generate-comment`)
- **modify_comment_api_url**: URL del servicio de modificación/pulido de comentarios (default: `http://localhost:9090/api/opinion/modify-comment`)

### Herramientas
- **ffmpeg_path**: Ruta al ejecutable de FFmpeg (default: `ffmpeg.exe`)

### Intervalos y Límites
- **extraction_interval_minutes**: Intervalo en minutos entre ciclos de extracción (default: 60)
- **processing_retry_count**: Número de reintentos para operaciones fallidas (default: 3)
- **processing_poll_interval_seconds**: Intervalo en segundos para polling de videos pendientes (default: 30)
- **comments_extraction_limit**: Límite de comentarios a extraer por video (default: 12)

### Directorios
- **temp_directory**: Directorio para archivos temporales

### Opciones
- **skip_transcription**: Si es `true`, omite el paso de transcripción (default: false)

## Recarga Automática

El sistema detecta automáticamente cambios en el archivo de configuración y recarga los valores sin necesidad de reiniciar la aplicación.

## Uso en Código

```csharp
// Inyectar IConfigurationManager
public MyService(IConfigurationManager config)
{
    var apiKey = config.AssemblyAIApiKey;
    var url = config.PocketBaseUrl;
}
```

## Seguridad

⚠️ **IMPORTANTE**: El archivo `settings.json` contiene credenciales sensibles. Asegúrate de:
- No compartir este archivo
- No subirlo a repositorios públicos
- Mantener permisos restrictivos en el archivo
