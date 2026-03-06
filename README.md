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

Copia `settings.example.json` como `settings.json` en la misma carpeta donde se ejecuta `FluxAnswer.exe` y actualízalo con tus valores.
Si esa carpeta no tiene permisos de escritura (por ejemplo en `Program Files`), la app usa `%LocalAppData%\TikTokManager\settings.json` automáticamente.
Comportamiento de ruta de configuración:
- Desarrollo: usa `<carpeta_del_ejecutable>\settings.json`
- Producción (instalada): usa `%LocalAppData%\TikTokManager\settings.json`

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

## Instalacion automatica (Windows)

Se incluye un instalador en PowerShell para preparar prerequisitos y dependencias:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-fluxanswer.ps1
```

Tambien tienes una version visual (GUI):

```powershell
powershell -ExecutionPolicy Bypass -File .\install-fluxanswer-visual.ps1
```

O doble clic en:

`install-fluxanswer-visual.cmd`

Si necesitas distribuirlo como `.exe`, genera el instalador ejecutable con:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer-exe.ps1 -ForceRebuild
```

Salida esperada:

- `FluxAnswer\dist\FluxAnswerInstaller.exe`

El instalador configura:

- .NET SDK 10
- yt-dlp
- ffmpeg (requerido para conversion a MP3)
- PocketBase
- `settings.json` (si no existe, lo crea desde `settings.example.json`)
- `dotnet restore` y `dotnet build`
- Acceso directo en escritorio `FluxAnswer.lnk` con icono descargado

Nota: despues de instalar herramientas puede ser necesario abrir una nueva terminal para refrescar PATH.

## Desinstalacion

Modo CLI:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall-fluxanswer.ps1
```

Desinstalacion completa (incluye datos locales, logs, sesiones y `settings.json`):

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall-fluxanswer.ps1 -RemoveAllData
```

Modo visual:

- Abre `install-fluxanswer-visual.ps1` o `install-fluxanswer-visual.cmd`
- Usa el boton `Desinstalar`
- Marca `Desinstalacion completa` si deseas borrado total local

## Ejecución

```bash
dotnet run
```
